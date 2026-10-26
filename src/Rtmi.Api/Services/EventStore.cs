using Rtmi.Api.Models;

namespace Rtmi.Api.Services;

public sealed class EventStore
{
    private readonly object _gate = new();
    private readonly List<MatchEvent> _events = [];
    private int _nextId;

    public int Count { get { lock (_gate) return _events.Count; } }

    public void Add(MatchEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        lock (_gate)
        {
            if (string.IsNullOrEmpty(e.Id))
                e.Id = $"evt_{++_nextId:D4}";
            else if (e.Id.StartsWith("evt_", StringComparison.Ordinal)
                && int.TryParse(e.Id.AsSpan(4), out var id))
                _nextId = Math.Max(_nextId, id);
            _events.Add(e);
        }
    }

    public List<MatchEvent> GetAll()
    {
        lock (_gate) return [.. _events];
    }

    public List<MatchEvent> GetSince(string? id)
    {
        lock (_gate)
        {
            if (string.IsNullOrEmpty(id)) return [.. _events];
            var index = _events.FindIndex(e => e.Id == id);
            return index < 0 ? [] : _events.Skip(index + 1).ToList();
        }
    }

    public List<MatchEvent> Query(EventType? type, int? fromMinute, int? toMinute, int take)
    {
        lock (_gate)
        {
            return _events
                .Where(e => type is null || e.Type == type)
                .Where(e => fromMinute is null || e.Minute >= fromMinute)
                .Where(e => toMinute is null || e.Minute <= toMinute)
                .Take(Math.Clamp(take, 0, 5000))
                .ToList();
        }
    }

    public bool Exists(string id)
    {
        lock (_gate) return _events.Any(e => e.Id == id);
    }

    public List<MatchEvent> GetRecent(int n)
    {
        lock (_gate) return _events.TakeLast(Math.Max(0, n)).ToList();
    }

    public void Clear()
    {
        lock (_gate)
        {
            _events.Clear();
            _nextId = 0;
        }
    }
}
