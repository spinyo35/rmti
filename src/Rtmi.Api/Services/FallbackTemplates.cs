using Rtmi.Api.Models;

namespace Rtmi.Api.Services;

public static class FallbackTemplates
{
    public static Insight CreateNarration(MatchEvent matchEvent, MatchState state, Persona persona)
    {
        var text = matchEvent.Type switch
        {
            EventType.Goal => persona == Persona.Kid
                ? $"Goal! {matchEvent.Player} scores for {TeamName(matchEvent.Team)}."
                : $"Goal for {TeamName(matchEvent.Team)}! {matchEvent.Player} finds the net.",
            EventType.RedCard => $"{matchEvent.Player} is sent off for {TeamName(matchEvent.Team)}.",
            EventType.YellowCard => $"{matchEvent.Player} is shown a yellow card.",
            EventType.Shot => persona == Persona.StatsNerd && matchEvent.Xg is not null
                ? $"{matchEvent.Player} tries one for {TeamName(matchEvent.Team)} — listed at {matchEvent.Xg:0.00} xG."
                : $"{matchEvent.Player} has a shot for {TeamName(matchEvent.Team)}.",
            EventType.MomentumShift => $"{TeamName(matchEvent.Team)} are edging the momentum right now.",
            _ => $"{readableEvent(matchEvent.Type)} for {TeamName(matchEvent.Team)}."
        };

        if (persona == Persona.Tactician && matchEvent.Type is EventType.Goal or EventType.Shot)
            text = $"{text} The attempt came from {matchEvent.X}% upfield.";
        if (persona == Persona.Casual && matchEvent.Type == EventType.Goal)
            text = $"What a moment — {matchEvent.Player} puts {TeamName(matchEvent.Team)} in front!";
        if (persona == Persona.Kid && matchEvent.Type == EventType.Goal)
            text = $"Goal! {matchEvent.Player} scores for {TeamName(matchEvent.Team)}!";

        return new Insight
        {
            Id = $"ins_{Guid.NewGuid():N}",
            Kind = InsightKind.Narration,
            Persona = persona,
            Source = "Fallback",
            Text = text,
            Why = $"Based on {matchEvent.Type} at {matchEvent.Minute}:{matchEvent.Second:D2} (score {state.Home.Score}-{state.Away.Score}).",
            EvidenceEventIds = [matchEvent.Id],
            Confidence = 0.5,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static DeskTake CreateDeskTake(string role, MatchEvent matchEvent, MatchState state)
    {
        var teamName = TeamName(matchEvent.Team);
        var tacticalTake = matchEvent.Type is EventType.MomentumShift or EventType.KickOff or EventType.HalfTime or EventType.FullTime
            ? ($"{ReadableEvent(matchEvent.Type)} recorded for {teamName}.",
                $"The event stream records {matchEvent.Type} for {teamName} at {matchEvent.Minute}:{matchEvent.Second:D2}.")
            : ($"{matchEvent.Type} by {teamName}; the supplied event places it at {matchEvent.X}% upfield.",
                $"The event record gives the action location as x={matchEvent.X}, y={matchEvent.Y}.");
        var (text, why) = role switch
        {
            "Tactics" => tacticalTake,
            "Stats" => ($"{teamEventCount(matchEvent, state)} recorded for {teamName} at {matchEvent.Minute}'.",
                $"This take uses the supplied event type and current score of {state.Home.Score}-{state.Away.Score}."),
            _ => ($"{ReadableEvent(matchEvent.Type)} gives {teamName} a moment to build on.",
                $"The supplied timeline records {matchEvent.Type} for {teamName} at {matchEvent.Minute}:{matchEvent.Second:D2}.")
        };
        return new DeskTake
        {
            Role = role,
            Text = text,
            Why = why,
            EvidenceEventIds = [matchEvent.Id],
            Confidence = 0.5
        };
    }

    public static Insight CreateDeskVerdict(
        MatchEvent matchEvent,
        MatchState state,
        Persona persona,
        IReadOnlyCollection<DeskTake> takes)
    {
        var text = $"{ReadableEvent(matchEvent.Type)} for {TeamName(matchEvent.Team)} is the defining detail; the score remains {state.Home.Score}-{state.Away.Score}.";
        if (persona == Persona.Kid)
            text = $"The {ReadableEvent(matchEvent.Type).ToLowerInvariant()} matters, but the score is still {state.Home.Score}-{state.Away.Score}.";

        return new Insight
        {
            Id = $"ins_{Guid.NewGuid():N}",
            Kind = InsightKind.Verdict,
            Persona = persona,
            Source = "Fallback",
            Text = text,
            Why = $"The panel's template synthesis is grounded in the event and all {takes.Count} generated desk perspectives.",
            EvidenceEventIds = [matchEvent.Id],
            Confidence = 0.5,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static string TeamName(Team team) => team == Team.HOME ? "Harare Lions" : "Bulawayo Eagles";

    private static string teamEventCount(MatchEvent matchEvent, MatchState state)
    {
        var team = matchEvent.Team == Team.HOME ? state.Home : state.Away;
        return $"{team.Shots} shots";
    }

    private static string readableEvent(EventType type) => ReadableEvent(type);

    private static string ReadableEvent(EventType type) => type.ToString().Replace("Card", " card");
}
