using Rtmi.Api.Models;

namespace Rtmi.Api.Services;

public sealed class StatsEngine
{
    private readonly object _gate = new();
    private MatchState _state = CreateInitialState();
    private int _homePasses;
    private int _awayPasses;

    public void Apply(MatchEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        lock (_gate)
        {
            _state.Minute = Math.Max(_state.Minute, e.Minute);
            if (_state.Status == MatchStatus.HalfTime && e.Minute > 45 && e.Type != EventType.FullTime)
                _state.Status = MatchStatus.Live;
            var team = e.Team == Team.HOME ? _state.Home : _state.Away;
            switch (e.Type)
            {
                case EventType.Goal:
                    team.Score++;
                    AddShot(team, e.Xg);
                    break;
                case EventType.Shot:
                    AddShot(team, e.Xg);
                    break;
                case EventType.Foul:
                case EventType.YellowCard:
                case EventType.RedCard:
                    team.Fouls++;
                    break;
                case EventType.Pass:
                    if (e.Team == Team.HOME) _homePasses++;
                    else _awayPasses++;
                    var passes = _homePasses + _awayPasses;
                    _state.Home.Possession = passes == 0 ? 0 : (int)Math.Round(100d * _homePasses / passes);
                    _state.Away.Possession = passes == 0 ? 0 : 100 - _state.Home.Possession;
                    break;
                case EventType.KickOff:
                    _state.Status = MatchStatus.Live;
                    break;
                case EventType.HalfTime:
                    _state.Status = MatchStatus.HalfTime;
                    break;
                case EventType.FullTime:
                    _state.Status = MatchStatus.FullTime;
                    break;
            }
        }
    }

    public MatchState Snapshot()
    {
        lock (_gate)
        {
            return new MatchState
            {
                MatchId = _state.MatchId,
                Minute = _state.Minute,
                Status = _state.Status,
                Home = Clone(_state.Home),
                Away = Clone(_state.Away),
                Momentum = new Momentum { Home = _state.Momentum.Home, Away = _state.Momentum.Away }
            };
        }
    }

    public void SetStatus(MatchStatus status)
    {
        lock (_gate) _state.Status = status;
    }

    public void SetMomentum(Momentum momentum)
    {
        lock (_gate)
        {
            _state.Momentum.Home = momentum.Home;
            _state.Momentum.Away = momentum.Away;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _state = CreateInitialState();
            _homePasses = 0;
            _awayPasses = 0;
        }
    }

    private static void AddShot(TeamState team, double? xg)
    {
        team.Shots++;
        team.Xg += xg ?? 0;
    }

    private static TeamState Clone(TeamState team) => new()
    {
        Name = team.Name,
        Score = team.Score,
        Shots = team.Shots,
        Xg = team.Xg,
        Possession = team.Possession,
        Fouls = team.Fouls
    };

    private static MatchState CreateInitialState() => new()
    {
        MatchId = "m1",
        Minute = 0,
        Status = MatchStatus.NotStarted,
        Home = new TeamState { Name = "Harare Lions" },
        Away = new TeamState { Name = "Bulawayo Eagles" },
        Momentum = new Momentum { Home = 0.5, Away = 0.5 }
    };
}
