using Rtmi.Api.Models;
using Rtmi.Api.Services;

namespace Rtmi.Api.Tests;

public sealed class FallbackTemplatesTests
{
    [Theory]
    [InlineData(Persona.Casual)]
    [InlineData(Persona.StatsNerd)]
    [InlineData(Persona.Kid)]
    [InlineData(Persona.Tactician)]
    public void NarrationFallbackIsExplainableForEveryPersona(Persona persona)
    {
        var matchEvent = new MatchEvent { Id = "evt_0001", Minute = 33, Type = EventType.Goal, Team = Team.HOME, Player = "J. Moyo" };
        var insight = FallbackTemplates.CreateNarration(matchEvent, new StatsEngine().Snapshot(), persona);

        Assert.Equal(persona, insight.Persona);
        Assert.Equal("Fallback", insight.Source);
        Assert.Equal(["evt_0001"], insight.EvidenceEventIds);
        Assert.Equal(0.5, insight.Confidence);
        Assert.Contains("J. Moyo", insight.Text);
        Assert.Contains("33:00", insight.Why);
    }

    [Fact]
    public void DeskFallbackProvidesThreeRolesAndAnEvidenceBasedVerdict()
    {
        var matchEvent = new MatchEvent { Id = "evt_0002", Minute = 58, Type = EventType.RedCard, Team = Team.AWAY, Player = "S. Dube" };
        var state = new StatsEngine().Snapshot();
        var takes = new[]
        {
            FallbackTemplates.CreateDeskTake("Tactics", matchEvent, state),
            FallbackTemplates.CreateDeskTake("Stats", matchEvent, state),
            FallbackTemplates.CreateDeskTake("Fan", matchEvent, state)
        };
        var verdict = FallbackTemplates.CreateDeskVerdict(matchEvent, state, Persona.Casual, takes);

        Assert.Equal(["Tactics", "Stats", "Fan"], takes.Select(take => take.Role));
        Assert.All(takes, take => Assert.Equal(["evt_0002"], take.EvidenceEventIds));
        Assert.Equal(InsightKind.Verdict, verdict.Kind);
        Assert.Equal(["evt_0002"], verdict.EvidenceEventIds);
    }
}
