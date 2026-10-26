using System.Text.Json.Serialization;

namespace Rtmi.Api.Models;

public sealed class MatchEvent
{
    public string Id { get; set; } = "";
    public string MatchId { get; set; } = "m1";
    public int Minute { get; set; }
    public int Second { get; set; }
    public EventType Type { get; set; }
    public Team Team { get; set; }
    public string Player { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public string? Outcome { get; set; }
    public double? Xg { get; set; }
    public bool IsKey { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? RelatedEventIds { get; set; }
}
