using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Rtmi.Api.Hubs;
using Rtmi.Api.Models;
using Rtmi.Api.Services;
using Rtmi.Api.Simulation;

namespace Rtmi.Api.Tests;

public sealed class MatchSimulatorScriptTests
{
    [Fact]
    public void ScriptContainsExpectedKeyBeats()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Simulation", "ScenarioScript.json");
        var script = JsonDocument.Parse(File.ReadAllText(path));
        var beats = script.RootElement.GetProperty("keyBeats")
            .EnumerateArray()
            .Select(beat => new
            {
                Type = Enum.Parse<EventType>(beat.GetProperty("type").GetString()!),
                Minute = beat.GetProperty("minute").GetInt32(),
                IsKey = beat.GetProperty("isKey").GetBoolean()
            })
            .ToList();

        Assert.Contains(beats, beat => beat.Type == EventType.Goal && beat.IsKey);
        Assert.Contains(beats, beat => beat.Type == EventType.RedCard);
        Assert.Contains(beats, beat => beat.Type == EventType.Goal && beat.Minute >= 80);
        Assert.DoesNotContain(beats, beat => beat.Type == EventType.MomentumShift);
    }

    [Fact]
    public void DemoScheduleIsRepeatable()
    {
        var simulator = new MatchSimulator(
            new ConfigurationBuilder().Build(),
            new EventStore(),
            new EventBus(),
            new StatsEngine(),
            new MomentumCalculator(),
            new InsightStore(),
            new NullMatchBroadcaster(),
            NullLogger<MatchSimulator>.Instance);

        var firstSchedule = simulator.BuildSchedule(demoOnly: true);
        var first = JsonSerializer.Serialize(firstSchedule);
        var second = JsonSerializer.Serialize(simulator.BuildSchedule(demoOnly: true));

        Assert.Equal(first, second);
        Assert.Contains(firstSchedule, scheduled =>
            scheduled.Evt.Type == EventType.KickOff && scheduled.Evt.IsKey);
    }

    private sealed class NullMatchBroadcaster : IMatchBroadcaster
    {
        public Task EventReceived(MatchEvent e) => Task.CompletedTask;
        public Task StateUpdated(MatchState state) => Task.CompletedTask;
        public Task InsightCreated(Insight insight) => Task.CompletedTask;
        public Task DeskReady(AnalystDeskResult result) => Task.CompletedTask;
        public Task RefereeReady(RefereeVerdict verdict) => Task.CompletedTask;
    }
}
