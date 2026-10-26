using System.Text.Json;
using Rtmi.Api.Models;

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
}
