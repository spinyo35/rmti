namespace Rtmi.Api.Models;

public sealed class Insight
{
    public string Id { get; set; } = "";
    public InsightKind Kind { get; set; }
    public Persona Persona { get; set; }
    public string Source { get; set; } = "";
    public string Text { get; set; } = "";
    public string Why { get; set; } = "";
    public List<string> EvidenceEventIds { get; set; } = [];
    public double Confidence { get; set; }
    public DateTime CreatedAt { get; set; }
}
