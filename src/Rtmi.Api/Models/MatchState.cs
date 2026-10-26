namespace Rtmi.Api.Models;

public sealed class MatchState
{
    public string MatchId { get; set; } = "m1";
    public int Minute { get; set; }
    public MatchStatus Status { get; set; }
    public TeamState Home { get; set; } = new();
    public TeamState Away { get; set; } = new();
    public Momentum Momentum { get; set; } = new();
}

public sealed class TeamState
{
    public string Name { get; set; } = "";
    public int Score { get; set; }
    public int Shots { get; set; }
    public double Xg { get; set; }
    public int Possession { get; set; }
    public int Fouls { get; set; }
}

public sealed class Momentum
{
    public double Home { get; set; } = 0.5;
    public double Away { get; set; } = 0.5;
}
