using Rtmi.Api.Models;

namespace Rtmi.Api.Services;

public sealed class InsightStore
{
    private readonly object _gate = new();
    private readonly List<Insight> _insights = [];

    public void Add(Insight insight)
    {
        ArgumentNullException.ThrowIfNull(insight);
        lock (_gate) _insights.Add(insight);
    }

    public List<Insight> GetAll()
    {
        lock (_gate) return [.. _insights];
    }

    public List<Insight> GetByPersona(Persona persona)
    {
        lock (_gate) return _insights.Where(i => i.Persona == persona).ToList();
    }

    public void Clear()
    {
        lock (_gate) _insights.Clear();
    }
}
