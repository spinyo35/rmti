using Microsoft.AspNetCore.SignalR;
using Rtmi.Api.Models;

namespace Rtmi.Api.Hubs;

public sealed class MatchHub : Hub
{
    public async Task SetPersona(string persona)
    {
        if (!Enum.TryParse<Persona>(persona, ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
            throw new HubException("Unknown persona.");

        foreach (var group in Enum.GetValues<Persona>().Select(value => $"persona:{value}"))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"persona:{parsed}");
    }
}
