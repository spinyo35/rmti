using Rtmi.Api.Models;
using Rtmi.Api.Services;

namespace Rtmi.Api.Tests;

public sealed class InsightValidatorTests
{
    [Fact]
    public void AcceptsInsightWithExistingEvidenceAndValidConfidence()
    {
        var events = new EventStore();
        events.Add(new MatchEvent { Id = "evt_0001", Type = EventType.Goal, Team = Team.HOME });
        var validator = new InsightValidator(events);
        var insight = ValidInsight();

        Assert.True(validator.TryValidate(insight, out var error));
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("", "A reason", 0.5, "Insight text is required.")]
    [InlineData("A claim", "", 0.5, "An explanation is required.")]
    [InlineData("A claim", "A reason", -0.1, "Confidence must be a finite number between 0 and 1.")]
    [InlineData("A claim", "A reason", 1.1, "Confidence must be a finite number between 0 and 1.")]
    public void RejectsInvalidInsight(string text, string why, double confidence, string expectedError)
    {
        var events = new EventStore();
        events.Add(new MatchEvent { Id = "evt_0001" });
        var validator = new InsightValidator(events);
        var insight = ValidInsight();
        insight.Text = text;
        insight.Why = why;
        insight.Confidence = confidence;

        Assert.False(validator.TryValidate(insight, out var error));
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public void RejectsMissingOrUnknownEvidence()
    {
        var validator = new InsightValidator(new EventStore());
        var insight = ValidInsight();

        Assert.False(validator.TryValidate(insight, out var error));
        Assert.Equal("Every evidence event ID must exist in the event store.", error);
    }

    [Fact]
    public void RejectsInsightWithoutEvidence()
    {
        var events = new EventStore();
        var validator = new InsightValidator(events);
        var insight = ValidInsight();
        insight.EvidenceEventIds = [];

        Assert.False(validator.TryValidate(insight, out var error));
        Assert.Equal("At least one evidence event is required.", error);
    }

    [Fact]
    public void ValidatesDeskRoleEvidenceAndConfidence()
    {
        var events = new EventStore();
        events.Add(new MatchEvent { Id = "evt_0001" });
        var validator = new InsightValidator(events);
        var take = new DeskTake
        {
            Role = "Tactics",
            Text = "The event occurred in the attacking half.",
            Why = "The event location is x=70.",
            EvidenceEventIds = ["evt_0001"],
            Confidence = 0.7
        };

        Assert.True(validator.TryValidate(take, "Tactics", out var error));
        Assert.Empty(error);
        Assert.False(validator.TryValidate(take, "Stats", out error));
        Assert.Equal("Desk role must be 'Stats'.", error);
    }

    [Fact]
    public void RejectsRefereeEvidenceOutsideSuppliedFrames()
    {
        var validator = new InsightValidator(new EventStore());
        var verdict = new RefereeVerdict
        {
            Decision = RefereeDecision.Offside,
            Text = "The goal is under review.",
            Why = "The attacker appears beyond the defender at the pass.",
            PersonaText = "The goal may not count.",
            EvidenceFrameIndexes = [3],
            Confidence = 0.8
        };

        Assert.False(validator.TryValidate(verdict, 3, out var error));
        Assert.Equal("Referee evidence frame indexes must be unique and within the supplied frames.", error);
    }

    [Fact]
    public void RejectsExistingEvidenceNotIncludedInTheAgentRequest()
    {
        var validator = new InsightValidator(new EventStore());

        Assert.False(validator.TryValidateEvidenceSubset(["evt_0002"], ["evt_0001"], out var error));
        Assert.Equal("Evidence event IDs must be explicitly supplied in the request context.", error);
    }

    private static Insight ValidInsight() => new()
    {
        Text = "A goal has been scored.",
        Why = "The event is recorded in the match timeline.",
        EvidenceEventIds = ["evt_0001"],
        Confidence = 0.8
    };
}
