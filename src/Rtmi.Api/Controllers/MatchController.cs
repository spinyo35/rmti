using Microsoft.AspNetCore.Mvc;
using Rtmi.Api.Models;
using Rtmi.Api.Services;

namespace Rtmi.Api.Controllers;

[ApiController]
[Route("api/match")]
public sealed class MatchController(IMatchControl control, StatsEngine stats, EventStore events) : ControllerBase
{
    [HttpPost("start")]
    public ActionResult<ApiResult<string>> Start([FromQuery] int speed = 30)
    {
        control.Start(Math.Clamp(speed, 1, 120));
        return Ok(new ApiResult<string>(true, control.Status));
    }

    [HttpPost("pause")]
    public ActionResult<ApiResult<string>> Pause()
    {
        control.Pause();
        return Ok(new ApiResult<string>(true, control.Status));
    }

    [HttpPost("reset")]
    public ActionResult<ApiResult<string>> Reset()
    {
        control.Reset();
        return Ok(new ApiResult<string>(true, control.Status));
    }

    [HttpPost("demo")]
    public ActionResult<ApiResult<string>> Demo()
    {
        control.Demo();
        return Ok(new ApiResult<string>(true, control.Status));
    }

    [HttpGet("state")]
    public ActionResult<MatchState> State() => Ok(stats.Snapshot());

    [HttpGet("events")]
    public ActionResult<List<MatchEvent>> Events([FromQuery] EventsQuery query)
    {
        EventType? type = Enum.TryParse<EventType>(query.Type, true, out var parsed) ? parsed : null;
        var source = query.Since is null
            ? events.GetAll()
            : events.GetSince(query.Since);
        return Ok(source
            .Where(e => type is null || e.Type == type)
            .Where(e => query.FromMinute is null || e.Minute >= query.FromMinute)
            .Where(e => query.ToMinute is null || e.Minute <= query.ToMinute)
            .Take(Math.Clamp(query.Take, 0, 5000))
            .ToList());
    }
}
