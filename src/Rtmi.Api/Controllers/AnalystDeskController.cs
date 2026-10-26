using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Rtmi.Api.Hubs;
using Rtmi.Api.Models;
using Rtmi.Api.Services;

namespace Rtmi.Api.Controllers;

[ApiController]
[Route("api/analyst-desk")]
public sealed class AnalystDeskController(
    EventStore events,
    StatsEngine stats,
    IMatchBroadcaster broadcaster,
    InsightStore insights,
    InsightValidator validator,
    IFoundryAgentInvoker agentInvoker,
    IConfiguration configuration,
    ILogger<AnalystDeskController> logger) : ControllerBase
{
    private static readonly string[] DeskRoles = ["Tactics", "Stats", "Fan"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost]
    public async Task<ActionResult<AnalystDeskResult>> Create(
        [FromBody] AnalystDeskRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Persona))
            return BadRequest(new ApiResult<AnalystDeskResult>(false, null, "Unknown persona."));

        var matchEvent = string.IsNullOrWhiteSpace(request.EventId)
            ? events.GetRecent(100).LastOrDefault(item => item.IsKey)
            : events.GetAll().FirstOrDefault(item => item.Id == request.EventId);
        if (matchEvent is null)
            return Conflict(new ApiResult<AnalystDeskResult>(false, null, "No matching key event is available for the analyst desk."));

        var snapshot = stats.Snapshot();
        var recentEvents = events.GetRecent(10);
        var takes = await Task.WhenAll(DeskRoles.Select(role =>
            CreateTakeAsync(role, request.Persona, matchEvent, recentEvents, snapshot, cancellationToken)));
        var verdict = await CreateVerdictAsync(
            request.Persona, matchEvent, recentEvents, snapshot, takes, cancellationToken);
        var result = new AnalystDeskResult
        {
            EventId = matchEvent.Id,
            Persona = request.Persona,
            Takes = takes.ToList(),
            Verdict = verdict
        };
        insights.Add(verdict);
        await broadcaster.DeskReady(result);
        return Ok(result);
    }

    private async Task<DeskTake> CreateTakeAsync(
        string role,
        Persona persona,
        MatchEvent matchEvent,
        IReadOnlyList<MatchEvent> recentEvents,
        MatchState snapshot,
        CancellationToken cancellationToken)
    {
        if (UseFallbackOnly())
            return FallbackTemplates.CreateDeskTake(role, matchEvent, snapshot);

        try
        {
            var input = JsonSerializer.Serialize(new { persona, @event = matchEvent, recentEvents, state = snapshot }, JsonOptions);
            var output = await agentInvoker.InvokeTextAsync(AgentName(role), input, cancellationToken);
            var parsed = AgentJsonResponseParser.Parse<DeskTakeAgentOutput>(output);
            var take = new DeskTake
            {
                Role = parsed.Role ?? "",
                Text = parsed.Text ?? "",
                Why = parsed.Why ?? "",
                EvidenceEventIds = parsed.EvidenceEventIds ?? [],
                Confidence = parsed.Confidence ?? double.NaN,
                Source = "Foundry"
            };
            var allowedEventIds = recentEvents.Append(matchEvent).Select(item => item.Id).Distinct(StringComparer.Ordinal).ToArray();
            if (validator.TryValidate(take, role, out var error)
                && validator.TryValidateEvidenceSubset(take.EvidenceEventIds, allowedEventIds, out error))
                return take;

            logger.LogWarning("Foundry {Role} take for {EventId} failed validation: {ValidationError}", role, matchEvent.Id, error);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Foundry {Role} agent failed for {EventId}; using a labeled fallback.", role, matchEvent.Id);
        }

        return FallbackTemplates.CreateDeskTake(role, matchEvent, snapshot);
    }

    private async Task<Insight> CreateVerdictAsync(
        Persona persona,
        MatchEvent matchEvent,
        IReadOnlyList<MatchEvent> recentEvents,
        MatchState snapshot,
        IReadOnlyList<DeskTake> takes,
        CancellationToken cancellationToken)
    {
        if (UseFallbackOnly())
            return FallbackTemplates.CreateDeskVerdict(matchEvent, snapshot, persona, takes);

        try
        {
            var input = JsonSerializer.Serialize(
                new { persona, @event = matchEvent, recentEvents, state = snapshot, takes },
                JsonOptions);
            var output = await agentInvoker.InvokeTextAsync(
                configuration["Agents:Verdict"] ?? "rtmi-verdict",
                input,
                cancellationToken);
            var parsed = AgentJsonResponseParser.Parse<VerdictAgentOutput>(output);
            var verdict = new Insight
            {
                Id = $"ins_{Guid.NewGuid():N}",
                Kind = InsightKind.Verdict,
                Persona = persona,
                Source = "Foundry",
                Text = parsed.Text ?? "",
                Why = parsed.Why ?? "",
                EvidenceEventIds = parsed.EvidenceEventIds ?? [],
                Confidence = parsed.Confidence ?? double.NaN,
                CreatedAt = DateTime.UtcNow
            };
            var allowedEventIds = recentEvents
                .Append(matchEvent)
                .Select(item => item.Id)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (validator.TryValidate(verdict, out var error)
                && validator.TryValidateEvidenceSubset(verdict.EvidenceEventIds, allowedEventIds, out error))
                return verdict;

            logger.LogWarning("Foundry verdict for {EventId} failed validation: {ValidationError}", matchEvent.Id, error);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Foundry verdict agent failed for {EventId}; using a labeled fallback.", matchEvent.Id);
        }

        return FallbackTemplates.CreateDeskVerdict(matchEvent, snapshot, persona, takes);
    }

    private string AgentName(string role) =>
        configuration[$"Agents:{role}"] ?? $"rtmi-{role.ToLowerInvariant()}";

    private bool UseFallbackOnly() =>
        configuration.GetValue("Foundry:UseFallbackOnly", false) || !agentInvoker.IsConfigured;

    private sealed class DeskTakeAgentOutput
    {
        public string? Role { get; init; }
        public string? Text { get; init; }
        public string? Why { get; init; }
        public List<string>? EvidenceEventIds { get; init; }
        public double? Confidence { get; init; }
    }

    private sealed class VerdictAgentOutput
    {
        public string? Text { get; init; }
        public string? Why { get; init; }
        public List<string>? EvidenceEventIds { get; init; }
        public double? Confidence { get; init; }
    }
}

public sealed record AnalystDeskRequest(string? EventId, Persona Persona);
