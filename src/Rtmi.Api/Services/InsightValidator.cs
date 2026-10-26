using Rtmi.Api.Models;

namespace Rtmi.Api.Services;

public sealed class InsightValidator(EventStore events)
{
    public bool TryValidate(Insight insight, out string error)
    {
        ArgumentNullException.ThrowIfNull(insight);
        if (string.IsNullOrWhiteSpace(insight.Text))
            return Fail("Insight text is required.", out error);
        if (string.IsNullOrWhiteSpace(insight.Why))
            return Fail("An explanation is required.", out error);
        if (insight.EvidenceEventIds is null || insight.EvidenceEventIds.Count == 0)
            return Fail("At least one evidence event is required.", out error);
        if (insight.EvidenceEventIds.Any(id => string.IsNullOrWhiteSpace(id) || !events.Exists(id)))
            return Fail("Every evidence event ID must exist in the event store.", out error);
        if (!double.IsFinite(insight.Confidence) || insight.Confidence is < 0 or > 1)
            return Fail("Confidence must be a finite number between 0 and 1.", out error);

        error = "";
        return true;
    }

    public bool TryValidateEvidenceSubset(
        IReadOnlyCollection<string> evidenceEventIds,
        IReadOnlyCollection<string> allowedEventIds,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(evidenceEventIds);
        ArgumentNullException.ThrowIfNull(allowedEventIds);
        var allowed = allowedEventIds.ToHashSet(StringComparer.Ordinal);
        if (evidenceEventIds.Any(id => !allowed.Contains(id)))
            return Fail("Evidence event IDs must be explicitly supplied in the request context.", out error);

        error = "";
        return true;
    }

    public bool TryValidate(DeskTake take, string expectedRole, out string error)
    {
        ArgumentNullException.ThrowIfNull(take);
        if (!string.Equals(take.Role, expectedRole, StringComparison.Ordinal))
            return Fail($"Desk role must be '{expectedRole}'.", out error);
        if (string.IsNullOrWhiteSpace(take.Text))
            return Fail("Desk take text is required.", out error);
        if (string.IsNullOrWhiteSpace(take.Why))
            return Fail("A desk take explanation is required.", out error);
        if (take.EvidenceEventIds is null || take.EvidenceEventIds.Count == 0)
            return Fail("At least one evidence event is required.", out error);
        if (take.EvidenceEventIds.Any(id => string.IsNullOrWhiteSpace(id) || !events.Exists(id)))
            return Fail("Every evidence event ID must exist in the event store.", out error);
        if (!double.IsFinite(take.Confidence) || take.Confidence is < 0 or > 1)
            return Fail("Confidence must be a finite number between 0 and 1.", out error);

        error = "";
        return true;
    }

    public bool TryValidate(RefereeVerdict verdict, int frameCount, out string error)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        if (!Enum.IsDefined(verdict.Mode) || !Enum.IsDefined(verdict.Decision))
            return Fail("The referee decision or mode is invalid.", out error);
        if (string.IsNullOrWhiteSpace(verdict.Text))
            return Fail("Referee decision text is required.", out error);
        if (string.IsNullOrWhiteSpace(verdict.Why))
            return Fail("A referee explanation is required.", out error);
        if (string.IsNullOrWhiteSpace(verdict.PersonaText))
            return Fail("Persona-specific referee text is required.", out error);
        if (frameCount < 1 || verdict.EvidenceFrameIndexes is null
            || verdict.EvidenceFrameIndexes.Any(index => index < 0 || index >= frameCount)
            || verdict.EvidenceFrameIndexes.Distinct().Count() != verdict.EvidenceFrameIndexes.Count)
            return Fail("Referee evidence frame indexes must be unique and within the supplied frames.", out error);
        if (verdict.EvidenceEventIds is null
            || verdict.EvidenceEventIds.Any(id => string.IsNullOrWhiteSpace(id) || !events.Exists(id))
            || verdict.EvidenceEventIds.Distinct(StringComparer.Ordinal).Count() != verdict.EvidenceEventIds.Count)
            return Fail("Every referee evidence event ID must exist and be unique.", out error);
        if (!double.IsFinite(verdict.Confidence) || verdict.Confidence is < 0 or > 1)
            return Fail("Confidence must be a finite number between 0 and 1.", out error);

        error = "";
        return true;
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
