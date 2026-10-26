using Rtmi.Api.Models;
using Rtmi.Api.Services;

namespace Rtmi.Api.Tests;

public sealed class EventStoreTests
{
    [Fact]
    public void GetSinceReturnsEventsAfterMatchingId()
    {
        var store = new EventStore();
        store.Add(new MatchEvent { Type = EventType.Pass });
        store.Add(new MatchEvent { Type = EventType.Shot });
        store.Add(new MatchEvent { Type = EventType.Goal });

        var result = store.GetSince("evt_0001");

        Assert.Equal(["evt_0002", "evt_0003"], result.Select(e => e.Id));
        Assert.Empty(store.GetSince("unknown"));
    }

    [Fact]
    public void QueryFiltersTypeAndMinuteRange()
    {
        var store = new EventStore();
        store.Add(new MatchEvent { Type = EventType.Pass, Minute = 2 });
        store.Add(new MatchEvent { Type = EventType.Goal, Minute = 5 });
        store.Add(new MatchEvent { Type = EventType.Goal, Minute = 8 });

        var result = store.Query(EventType.Goal, 4, 6, 10);

        Assert.Single(result);
        Assert.Equal(5, result[0].Minute);
    }

    [Fact]
    public void AssignsIdsAndChecksExistence()
    {
        var store = new EventStore();
        var first = new MatchEvent();
        store.Add(first);

        Assert.Equal("evt_0001", first.Id);
        Assert.True(store.Exists(first.Id));
        Assert.False(store.Exists("evt_0099"));
    }

    [Fact]
    public void GetRecentPreservesOrderOfTheNewestEvents()
    {
        var store = new EventStore();
        for (var i = 0; i < 4; i++) store.Add(new MatchEvent());

        Assert.Equal(["evt_0003", "evt_0004"], store.GetRecent(2).Select(e => e.Id));
    }
}
