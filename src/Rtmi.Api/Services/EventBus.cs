using System.Threading.Channels;
using Rtmi.Api.Models;

namespace Rtmi.Api.Services;

public sealed class EventBus
{
    private readonly Channel<MatchEvent> _channel = Channel.CreateUnbounded<MatchEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ChannelReader<MatchEvent> Reader => _channel.Reader;

    public void Publish(MatchEvent e)
    {
        if (!_channel.Writer.TryWrite(e))
            throw new InvalidOperationException("The match event channel is closed.");
    }

    public void Reset() { }
}
