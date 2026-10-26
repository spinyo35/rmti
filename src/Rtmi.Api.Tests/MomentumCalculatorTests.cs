using Rtmi.Api.Models;
using Rtmi.Api.Services;

namespace Rtmi.Api.Tests;

public sealed class MomentumCalculatorTests
{
    [Fact]
    public void HomePressureEmitsMomentumShiftAndResetRestoresEvenMomentum()
    {
        var calculator = new MomentumCalculator();
        var state = new StatsEngine().Snapshot();
        var emitted = false;

        foreach (var shot in Enumerable.Range(1, 3).Select(i => new MatchEvent
        {
            Type = EventType.Shot,
            Team = Team.HOME,
            Minute = i
        }).Append(new MatchEvent { Type = EventType.Goal, Team = Team.HOME, Minute = 3 }))
        {
            emitted |= calculator.Apply(shot, state);
        }

        Assert.True(emitted);
        Assert.True(state.Momentum.Home > state.Momentum.Away);

        calculator.Reset();
        state.Momentum = new Momentum();
        Assert.False(calculator.Apply(new MatchEvent { Type = EventType.Pass, Minute = 4 }, state));
        Assert.Equal(0.5, state.Momentum.Home);
        Assert.Equal(0.5, state.Momentum.Away);
    }
}
