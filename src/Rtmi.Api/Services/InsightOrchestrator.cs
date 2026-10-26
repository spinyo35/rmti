using System.Diagnostics;
using System.Text.Json;
using Rtmi.Api.Hubs;
using Rtmi.Api.Models;

namespace Rtmi.Api.Services;

public sealed class InsightOrchestrator(
    EventBus eventBus,
    EventStore eventStore,
    StatsEngine stats,
    InsightStore insightStore,
    InsightValidator validator,
    IFoundryAgentInvoker agentInvoker,
    IMatchBroadcaster broadcaster,
    IConfiguration configuration,
    ILogger<InsightOrchestrator> logger) : BackgroundService
{
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(3);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Foundry:UseFallbackOnly", false) && !agentInvoker.IsConfigured)
            logger.LogWarning("Foundry:Endpoint is not configured; live commentary will use fallback templates.");

        while (!stoppingToken.IsCancellationRequested)
        {
            MatchEvent first;
            try
            {
                first = await eventBus.Reader.ReadAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (!first.IsKey || !eventStore.Exists(first.Id))
                continue;

            var latest = await DebounceKeyEvents(first, stoppingToken);
            if (!eventStore.Exists(latest.Id))
                continue;

            await PublishNarration(latest, stoppingToken);
        }
    }

    private async Task<MatchEvent> DebounceKeyEvents(MatchEvent current, CancellationToken cancellationToken)
    {
        var lastKeyAt = Stopwatch.GetTimestamp();
        while (true)
        {
            var elapsed = Stopwatch.GetElapsedTime(lastKeyAt);
            var remaining = DebounceWindow - elapsed;
            if (remaining <= TimeSpan.Zero)
                return current;

            try
            {
                var next = await eventBus.Reader.ReadAsync(cancellationToken).AsTask().WaitAsync(remaining, cancellationToken);
                if (next.IsKey && eventStore.Exists(next.Id))
                {
                    current = next;
                    lastKeyAt = Stopwatch.GetTimestamp();
                }
            }
            catch (TimeoutException)
            {
                return current;
            }
        }
    }

    private async Task PublishNarration(MatchEvent matchEvent, CancellationToken cancellationToken)
    {
        var snapshot = stats.Snapshot();
        var tasks = Enum.GetValues<Persona>()
            .Select(persona => CreateNarrationAsync(matchEvent, snapshot, persona, cancellationToken));
        foreach (var insight in await Task.WhenAll(tasks))
        {
            insightStore.Add(insight);
            await broadcaster.InsightCreated(insight);
        }
    }

    private async Task<Insight> CreateNarrationAsync(
        MatchEvent matchEvent,
        MatchState snapshot,
        Persona persona,
        CancellationToken cancellationToken)
    {
        var useFallback = configuration.GetValue("Foundry:UseFallbackOnly", false) || !agentInvoker.IsConfigured;
        if (!useFallback)
        {
            var recentEvents = eventStore.GetRecent(5);
            try
            {
                var input = JsonSerializer.Serialize(new
                {
                    persona,
                    @event = matchEvent,
                    recentEvents,
                    state = snapshot
                }, JsonOptions);
                var output = await agentInvoker.InvokeTextAsync(
                    configuration["Agents:Narrator"] ?? "rtmi-narrator",
                    input,
                    cancellationToken);
                var parsed = AgentJsonResponseParser.Parse<NarratorAgentOutput>(output);
                var insight = new Insight
                {
                    Id = $"ins_{Guid.NewGuid():N}",
                    Kind = InsightKind.Narration,
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
                if (validator.TryValidate(insight, out var error)
                    && validator.TryValidateEvidenceSubset(insight.EvidenceEventIds, allowedEventIds, out error))
                    return insight;

                logger.LogWarning("Foundry narrator response for {EventId}/{Persona} failed validation: {ValidationError}", matchEvent.Id, persona, error);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Foundry narrator failed for {EventId}/{Persona}; using a labeled fallback.", matchEvent.Id, persona);
            }
        }

        var fallback = FallbackTemplates.CreateNarration(matchEvent, snapshot, persona);
        if (!validator.TryValidate(fallback, out var fallbackError))
            throw new InvalidOperationException($"Fallback narration failed validation: {fallbackError}");
        return fallback;
    }

    private sealed class NarratorAgentOutput
    {
        public string? Text { get; init; }
        public string? Why { get; init; }
        public List<string>? EvidenceEventIds { get; init; }
        public double? Confidence { get; init; }
    }
}
