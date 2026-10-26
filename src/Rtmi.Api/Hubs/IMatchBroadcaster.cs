using Microsoft.AspNetCore.SignalR;
using Rtmi.Api.Models;

namespace Rtmi.Api.Hubs;

public interface IMatchBroadcaster
{
    Task EventReceived(MatchEvent e);
    Task StateUpdated(MatchState state);
    Task InsightCreated(Insight insight);
    Task DeskReady(AnalystDeskResult result);
    Task RefereeReady(RefereeVerdict verdict);
}

public sealed class MatchBroadcaster(IHubContext<MatchHub> hub) : IMatchBroadcaster
{
    public Task EventReceived(MatchEvent e) => hub.Clients.All.SendAsync("EventReceived", e);
    public Task StateUpdated(MatchState state) => hub.Clients.All.SendAsync("StateUpdated", state);
    public Task InsightCreated(Insight insight) =>
        hub.Clients.Group($"persona:{insight.Persona}").SendAsync("InsightCreated", insight);
    public Task DeskReady(AnalystDeskResult result) => hub.Clients.All.SendAsync("DeskReady", result);
    public Task RefereeReady(RefereeVerdict verdict) => hub.Clients.All.SendAsync("RefereeReady", verdict);
}
