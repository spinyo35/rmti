using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Rtmi.Api.Hubs;
using Rtmi.Api.Models;
using Rtmi.Api.Services;

namespace Rtmi.Api.Controllers;

[ApiController]
[Route("api/referee")]
public sealed class RefereeController(
    EventStore events,
    StatsEngine stats,
    IMatchBroadcaster broadcaster,
    InsightValidator validator,
    IFoundryAgentInvoker agentInvoker,
    IConfiguration configuration,
    ILogger<RefereeController> logger) : ControllerBase
{
    private const int MaxFrames = 8;
    private const int MaxFrameCharacters = 180_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost("check")]
    [RequestSizeLimit(2_000_000)]
    public async Task<ActionResult<RefereeVerdict>> Check(
        [FromBody] RefereeCheckRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Persona) || !Enum.IsDefined(request.Mode))
            return BadRequest(new ApiResult<RefereeVerdict>(false, null, "Unknown persona or referee mode."));
        if (request.Frames is null || request.Frames.Count is < 1 or > MaxFrames)
            return BadRequest(new ApiResult<RefereeVerdict>(false, null, $"Provide between 1 and {MaxFrames} sampled frames."));
        if (request.Frames.Any(frame =>
                string.IsNullOrWhiteSpace(frame)
                || frame.Length > MaxFrameCharacters
                || !frame.StartsWith("data:image/jpeg;base64,", StringComparison.Ordinal)
                || !IsValidBase64Image(frame)))
            return BadRequest(new ApiResult<RefereeVerdict>(false, null, "Each frame must be a valid JPEG data URL within the allowed size."));
        if (request.LinkedEventId is not null && !events.Exists(request.LinkedEventId))
            return BadRequest(new ApiResult<RefereeVerdict>(false, null, "The linked event does not exist."));

        var verdict = UseFallbackOnly()
            ? CreateFallback(request)
            : await ReviewWithFoundryAsync(request, cancellationToken);

        await broadcaster.RefereeReady(verdict);
        return Ok(verdict);
    }

    private async Task<RefereeVerdict> ReviewWithFoundryAsync(
        RefereeCheckRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var recentEvents = events.GetRecent(5);
            var context = JsonSerializer.Serialize(new
            {
                persona = request.Persona,
                mode = request.Mode,
                frames = request.Frames.Select((_, index) => new { index }),
                linkedEventId = request.LinkedEventId,
                recentEvents,
                state = stats.Snapshot()
            }, JsonOptions);
            var output = await agentInvoker.InvokeWithImagesAsync(
                configuration["Agents:VideoReferee"] ?? "rtmi-video-referee",
                context,
                request.Frames,
                cancellationToken);
            var parsed = AgentJsonResponseParser.Parse<RefereeAgentOutput>(output);
            if (!Enum.TryParse<RefereeDecision>(parsed.Decision, false, out var decision)
                || !Enum.IsDefined(decision))
                throw new JsonException("The referee agent returned an unsupported decision.");

            var verdict = new RefereeVerdict
            {
                Id = $"var_{Guid.NewGuid():N}",
                Mode = request.Mode,
                Decision = decision,
                Text = parsed.Text ?? "",
                Why = parsed.Why ?? "",
                EvidenceFrameIndexes = parsed.EvidenceFrameIndexes ?? [],
                EvidenceEventIds = parsed.EvidenceEventIds ?? [],
                Confidence = parsed.Confidence ?? double.NaN,
                PersonaText = parsed.PersonaText ?? "",
                Source = "Foundry",
                CreatedAt = DateTime.UtcNow
            };
            var allowedEventIds = recentEvents.Select(matchEvent => matchEvent.Id).ToHashSet(StringComparer.Ordinal);
            if (request.LinkedEventId is not null)
                allowedEventIds.Add(request.LinkedEventId);

            if (!validator.TryValidate(verdict, request.Frames.Count, out var error))
            {
                logger.LogWarning("Foundry referee response failed validation: {ValidationError}", error);
                return CreateFallback(request);
            }
            if (verdict.EvidenceEventIds.Any(id => !allowedEventIds.Contains(id)))
            {
                logger.LogWarning("Foundry referee response cited an event ID not included in the review context.");
                return CreateFallback(request);
            }

            return verdict;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Foundry video referee failed; returning an explicit no-analysis fallback.");
            return CreateFallback(request);
        }
    }

    private RefereeVerdict CreateFallback(RefereeCheckRequest request) => new()
    {
        Id = $"var_{Guid.NewGuid():N}",
        Mode = request.Mode,
        Decision = RefereeDecision.NoAction,
        Text = "No automated review was performed because the Foundry video-referee agent is unavailable.",
        Why = "The sampled clip was received, but the fallback does not inspect video frames. No on-field decision is inferred.",
        EvidenceFrameIndexes = [],
        EvidenceEventIds = request.LinkedEventId is null ? [] : [request.LinkedEventId],
        Confidence = 0,
        PersonaText = request.Persona switch
        {
            Persona.Kid => "I can't check this clip yet, so I don't know what happened.",
            Persona.StatsNerd => "No frame analysis is available; there is not enough evidence for an automated call.",
            Persona.Tactician => "This clip has not been analyzed; no review conclusion can be drawn.",
            _ => "The clip hasn't been analyzed yet, so there's no call to make."
        },
        Source = "Fallback",
        CreatedAt = DateTime.UtcNow
    };

    private bool UseFallbackOnly() =>
        configuration.GetValue("Foundry:UseFallbackOnly", false) || !agentInvoker.IsConfigured;

    private static bool IsValidBase64Image(string dataUrl)
    {
        try
        {
            var bytes = Convert.FromBase64String(dataUrl["data:image/jpeg;base64,".Length..]);
            return bytes.Length >= 3
                && bytes[0] == 0xFF
                && bytes[1] == 0xD8
                && bytes[^2] == 0xFF
                && bytes[^1] == 0xD9;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private sealed class RefereeAgentOutput
    {
        public string? Decision { get; init; }
        public string? Text { get; init; }
        public string? Why { get; init; }
        public List<int>? EvidenceFrameIndexes { get; init; }
        public List<string>? EvidenceEventIds { get; init; }
        public double? Confidence { get; init; }
        public string? PersonaText { get; init; }
    }
}

public sealed record RefereeCheckRequest(Persona Persona, RefereeMode Mode, List<string> Frames, string? LinkedEventId);
