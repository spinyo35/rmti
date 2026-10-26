using Rtmi.Api.Models;
using Rtmi.Api.Services;

namespace Rtmi.Api.Tests;

public sealed class StatsEngineTests
{
    [Fact]
    public void GoalIncrementsScoreAndCountsAsShot()
    {
        var engine = new StatsEngine();
        engine.Apply(new MatchEvent { Type = EventType.Goal, Team = Team.HOME, Xg = 0.42 });

        var state = engine.Snapshot();
        Assert.Equal(1, state.Home.Score);
        Assert.Equal(1, state.Home.Shots);
        Assert.Equal(0.42, state.Home.Xg, 5);
    }

    [Fact]
    public void ShotAddsShotAndExpectedGoals()
    {
        var engine = new StatsEngine();
        engine.Apply(new MatchEvent { Type = EventType.Shot, Team = Team.AWAY, Xg = 0.21 });

        Assert.Equal(1, engine.Snapshot().Away.Shots);
        Assert.Equal(0.21, engine.Snapshot().Away.Xg, 5);
    }

    [Fact]
    public void FoulIncrementsCommittingTeam()
    {
        var engine = new StatsEngine();
        engine.Apply(new MatchEvent { Type = EventType.Foul, Team = Team.HOME });

        Assert.Equal(1, engine.Snapshot().Home.Fouls);
    }

    [Fact]
    public void PassesUpdatePossessionToAValidPair()
    {
        var engine = new StatsEngine();
        engine.Apply(new MatchEvent { Type = EventType.Pass, Team = Team.HOME });
        engine.Apply(new MatchEvent { Type = EventType.Pass, Team = Team.AWAY });
        engine.Apply(new MatchEvent { Type = EventType.Pass, Team = Team.HOME });

        var state = engine.Snapshot();
        Assert.InRange(state.Home.Possession, 0, 100);
        Assert.InRange(state.Away.Possession, 0, 100);
        Assert.Equal(100, state.Home.Possession + state.Away.Possession);
    }

    [Fact]
    public void SnapshotIsIndependentFromEngineState()
    {
        var engine = new StatsEngine();
        var snapshot = engine.Snapshot();
        snapshot.Home.Name = "Changed";
        snapshot.Momentum.Home = 0;

        var actual = engine.Snapshot();
        Assert.Equal("Harare Lions", actual.Home.Name);
        Assert.Equal(0.5, actual.Momentum.Home);
    }
}
