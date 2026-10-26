namespace Rtmi.Api.Models;

public sealed class RefereeVerdict
{
    public string Id { get; set; } = "";
    public RefereeMode Mode { get; set; }
    public RefereeDecision Decision { get; set; }
    public string Text { get; set; } = "";
    public string Why { get; set; } = "";
    public List<int> EvidenceFrameIndexes { get; set; } = [];
    public List<string> EvidenceEventIds { get; set; } = [];
    public double Confidence { get; set; }
    public string PersonaText { get; set; } = "";
    public string Source { get; set; } = "Fallback";
    public DateTime CreatedAt { get; set; }
}
