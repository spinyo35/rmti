using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Rtmi.Api.Hubs;
using Rtmi.Api.Models;
using Rtmi.Api.Services;

namespace Rtmi.Api.Simulation;

public sealed class MatchSimulator : BackgroundService, IMatchControl
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _startSignal = new(0);
    private readonly EventStore _eventStore;
    private readonly EventBus _eventBus;
    private readonly StatsEngine _stats;
    private readonly MomentumCalculator _momentum;
    private readonly InsightStore _insights;
    private readonly IMatchBroadcaster _broadcaster;
    private readonly ILogger<MatchSimulator> _logger;
    private readonly Scenario _scenario;
    private CancellationTokenSource? _runCancellation;
    private List<ScheduledEvent> _schedule = [];
    private int _speed = 30;
    private bool _isRunning;
    private bool _paused;
    private bool _demoOnly;
    private string _status = "NotStarted";

    public MatchSimulator(
        IConfiguration configuration,
        EventStore eventStore,
        EventBus eventBus,
        StatsEngine stats,
        MomentumCalculator momentum,
        InsightStore insights,
        IMatchBroadcaster broadcaster,
        ILogger<MatchSimulator> logger)
    {
        _eventStore = eventStore;
        _eventBus = eventBus;
        _stats = stats;
        _momentum = momentum;
        _insights = insights;
        _broadcaster = broadcaster;
        _logger = logger;
        _speed = Math.Clamp(configuration.GetValue("Simulation:DefaultSpeed", 30), 1, 120);
        var scenarioPath = configuration["Simulation:Scenario"] ?? "Simulation/ScenarioScript.json";
        var fullPath = Path.Combine(AppContext.BaseDirectory, scenarioPath);
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        _scenario = JsonSerializer.Deserialize<Scenario>(File.ReadAllText(fullPath), jsonOptions)
            ?? throw new InvalidDataException($"Unable to load scenario script from '{fullPath}'.");
    }

    public string Status { get { lock (_gate) return _status; } }

    public void Start(int speed) => StartInternal(speed, demoOnly: false);

    public void Demo()
    {
        Reset();
        StartInternal(_speed, demoOnly: true);
    }

    public void Pause()
    {
        MatchState state;
        lock (_gate)
        {
            if (!_isRunning) return;
            _paused = !_paused;
            _status = _paused ? "Paused" : "Running";
            _stats.SetStatus(_paused ? MatchStatus.Paused : MatchStatus.Live);
            state = _stats.Snapshot();
        }
        _broadcaster.StateUpdated(state).GetAwaiter().GetResult();
    }

    public void Reset()
    {
        MatchState state;
        lock (_gate)
        {
            _runCancellation?.Cancel();
            _runCancellation = null;
            _isRunning = false;
            _paused = false;
            _status = "NotStarted";
            _eventStore.Clear();
            _insights.Clear();
            _stats.Reset();
            _momentum.Reset();
            _eventBus.Reset();
            state = _stats.Snapshot();
        }
        _broadcaster.StateUpdated(state).GetAwaiter().GetResult();
    }

    public List<ScheduledEvent> BuildSchedule(bool demoOnly)
    {
        var random = new Random(demoOnly ? _scenario.FillerSeed : Environment.TickCount);
        var events = new List<MatchEvent>
        {
            CreateAutoEvent(0, EventType.KickOff, Team.HOME, ""),
            CreateAutoEvent(45, EventType.HalfTime, Team.HOME, ""),
            CreateAutoEvent(90, EventType.FullTime, Team.HOME, ""),
            CreateAutoEvent(46, EventType.Substitution, Team.HOME, _scenario.HomeRoster[^1]),
            CreateAutoEvent(63, EventType.Substitution, Team.AWAY, _scenario.AwayRoster[^1])
        };

        foreach (var beat in _scenario.KeyBeats)
        {
            events.Add(new MatchEvent
            {
                MatchId = _scenario.MatchId,
                Minute = beat.Minute,
                Second = beat.Second,
                Type = beat.Type,
                Team = beat.Team,
                Player = beat.Player,
                X = beat.X,
                Y = beat.Y,
                Outcome = beat.Outcome,
                Xg = beat.Xg,
                IsKey = beat.IsKey
            });
            if (beat.Type == EventType.Foul && beat.Outcome == "Yellow card")
            {
                var yellowMinute = beat.Minute + (beat.Second == 59 ? 1 : 0);
                var yellowSecond = beat.Second == 59 ? 0 : beat.Second + 1;
                events.Add(new MatchEvent
                {
                    MatchId = _scenario.MatchId,
                    Minute = yellowMinute,
                    Second = yellowSecond,
                    Type = EventType.YellowCard,
                    Team = beat.Team,
                    Player = beat.Player,
                    X = beat.X,
                    Y = beat.Y,
                    Outcome = "Yellow card",
                    IsKey = true
                });
            }
        }

        var coveredMinutes = events.Select(e => e.Minute).ToHashSet();
        for (var minute = 1; minute <= 89; minute++)
        {
            if (coveredMinutes.Contains(minute)) continue;
            for (var i = 0; i < _scenario.FillerEventsPerMinute; i++)
                events.Add(CreateFillerEvent(minute, random));
        }

        var delay = Math.Max(50, 30000 / Math.Clamp(_speed, 1, 120));
        return events
            .OrderBy(e => e.Minute)
            .ThenBy(e => e.Second)
            .Select(e => new ScheduledEvent(delay, e))
            .ToList();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _startSignal.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            CancellationToken runToken;
            List<ScheduledEvent> schedule;
            lock (_gate)
            {
                if (!_isRunning || _runCancellation is null) continue;
                runToken = _runCancellation.Token;
                schedule = _schedule;
            }

            try
            {
                foreach (var scheduled in schedule)
                {
                    await Task.Delay(scheduled.DelayMs, runToken);
                    while (true)
                    {
                        lock (_gate)
                        {
                            if (!_paused) break;
                        }
                        await Task.Delay(100, runToken);
                    }

                    await PublishEvent(scheduled.Evt);
                    if (scheduled.Evt.Type == EventType.FullTime) break;
                }
            }
            catch (OperationCanceledException) when (runToken.IsCancellationRequested)
            {
            }
            finally
            {
                lock (_gate)
                {
                    if (_runCancellation?.Token == runToken)
                    {
                        _isRunning = false;
                        _paused = false;
                        _status = _stats.Snapshot().Status == MatchStatus.FullTime ? "Finished" : "NotStarted";
                        _runCancellation.Dispose();
                        _runCancellation = null;
                    }
                }
            }
        }
    }

    private void StartInternal(int speed, bool demoOnly)
    {
        lock (_gate)
        {
            if (_isRunning || _stats.Snapshot().Status == MatchStatus.FullTime) return;
            if (_stats.Snapshot().Status == MatchStatus.NotStarted)
            {
                _eventStore.Clear();
                _insights.Clear();
                _stats.Reset();
                _momentum.Reset();
            }
            _speed = Math.Clamp(speed, 1, 120);
            _demoOnly = demoOnly;
            _schedule = BuildSchedule(_demoOnly);
            _runCancellation?.Dispose();
            _runCancellation = new CancellationTokenSource();
            _isRunning = true;
            _paused = false;
            _status = "Running";
            _startSignal.Release();
        }
    }

    private async Task PublishEvent(MatchEvent e)
    {
        _eventStore.Add(e);
        _stats.Apply(e);
        var state = _stats.Snapshot();
        var emittedShift = _momentum.Apply(e, state);
        _stats.SetMomentum(state.Momentum);

        await _broadcaster.EventReceived(e);
        await _broadcaster.StateUpdated(_stats.Snapshot());
        _eventBus.Publish(e);

        if (!emittedShift) return;
        var evidence = _eventStore.GetAll()
            .Where(item => item.Minute >= e.Minute - 5 && item.Type != EventType.Pass && item.Type != EventType.MomentumShift)
            .TakeLast(3)
            .Select(item => item.Id)
            .ToList();
        var momentum = _stats.Snapshot().Momentum;
        var shift = new MatchEvent
        {
            MatchId = e.MatchId,
            Minute = e.Minute,
            Second = e.Second,
            Type = EventType.MomentumShift,
            Team = momentum.Home > momentum.Away ? Team.HOME : Team.AWAY,
            Player = "",
            Outcome = "Momentum shift",
            IsKey = true,
            RelatedEventIds = evidence
        };
        _eventStore.Add(shift);
        await _broadcaster.EventReceived(shift);
        await _broadcaster.StateUpdated(_stats.Snapshot());
        _eventBus.Publish(shift);
    }

    private MatchEvent CreateAutoEvent(int minute, EventType type, Team team, string player) => new()
    {
        MatchId = _scenario.MatchId,
        Minute = minute,
        Type = type,
        Team = team,
        Player = player
    };

    private MatchEvent CreateFillerEvent(int minute, Random random)
    {
        var typeRoll = random.NextDouble();
        var type = typeRoll switch
        {
            < 0.65 => EventType.Pass,
            < 0.75 => EventType.Shot,
            < 0.83 => EventType.Foul,
            < 0.88 => EventType.Corner,
            _ => EventType.Offside
        };
        var team = random.NextDouble() < 0.52 ? Team.HOME : Team.AWAY;
        var roster = team == Team.HOME ? _scenario.HomeRoster : _scenario.AwayRoster;
        var shot = type == EventType.Shot;
        return new MatchEvent
        {
            MatchId = _scenario.MatchId,
            Minute = minute,
            Second = random.Next(0, 60),
            Type = type,
            Team = team,
            Player = roster[random.Next(roster.Length)],
            X = random.Next(5, 96),
            Y = random.Next(5, 96),
            Outcome = shot ? "Saved" : null,
            Xg = shot ? Math.Round(0.03 + random.NextDouble() * 0.09, 2) : null,
            IsKey = shot
        };
    }

    public sealed record ScheduledEvent(int DelayMs, MatchEvent Evt);

    private sealed class Scenario
    {
        public string MatchId { get; set; } = "m1";
        public string[] HomeRoster { get; set; } = [];
        public string[] AwayRoster { get; set; } = [];
        public int FillerSeed { get; set; }
        public int FillerEventsPerMinute { get; set; }
        public List<KeyBeat> KeyBeats { get; set; } = [];
    }

    private sealed class KeyBeat
    {
        public int Minute { get; set; }
        public int Second { get; set; }
        public EventType Type { get; set; }
        public Team Team { get; set; }
        public string Player { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public string? Outcome { get; set; }
        public double? Xg { get; set; }
        public bool IsKey { get; set; }
    }
}
