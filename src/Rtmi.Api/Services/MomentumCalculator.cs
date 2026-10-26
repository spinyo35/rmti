using Rtmi.Api.Models;

namespace Rtmi.Api.Services;

public sealed class MomentumCalculator
{
    private readonly List<(int Minute, Team Team, double Weight)> _events = [];
    private double _lastEmittedHomeMomentum = 0.5;

    public bool Apply(MatchEvent e, MatchState state)
    {
        var weight = e.Type switch
        {
            EventType.Goal => 3d,
            EventType.Shot => 1d,
            EventType.Corner or EventType.Save => 0.5d,
            _ => 0d
        };
        if (weight > 0) _events.Add((e.Minute, e.Team, weight));
        _events.RemoveAll(item => item.Minute < e.Minute - 5);

        var home = _events.Where(item => item.Team == Team.HOME).Sum(item => item.Weight);
        var away = _events.Where(item => item.Team == Team.AWAY).Sum(item => item.Weight);
        var total = Math.Max(home + away, 0.0001);
        state.Momentum.Home = home + away == 0 ? 0.5 : home / total;
        state.Momentum.Away = 1 - state.Momentum.Home;

        if (Math.Abs(state.Momentum.Home - _lastEmittedHomeMomentum) <= 0.25)
            return false;
        _lastEmittedHomeMomentum = state.Momentum.Home;
        return true;
    }

    public void Reset()
    {
        _events.Clear();
        _lastEmittedHomeMomentum = 0.5;
    }
}
