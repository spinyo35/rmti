namespace Rtmi.Api.Models;

public sealed class AnalystDeskResult
{
    public string EventId { get; set; } = "";
    public Persona Persona { get; set; }
    public List<DeskTake> Takes { get; set; } = [];
    public Insight Verdict { get; set; } = new();
}

public sealed class DeskTake
{
    public string Role { get; set; } = "";
    public string Text { get; set; } = "";
    public string Why { get; set; } = "";
    public List<string> EvidenceEventIds { get; set; } = [];
    public double Confidence { get; set; }
    public string Source { get; set; } = "Fallback";
}
