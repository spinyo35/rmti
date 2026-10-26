using Microsoft.AspNetCore.Mvc;
using Rtmi.Api.Models;
using Rtmi.Api.Services;

namespace Rtmi.Api.Controllers;

[ApiController]
[Route("api/insights")]
public sealed class InsightsController(InsightStore insights) : ControllerBase
{
    [HttpGet]
    public ActionResult<List<Insight>> Get([FromQuery] string? persona)
    {
        if (persona is null) return Ok(insights.GetAll());
        return Enum.TryParse<Persona>(persona, true, out var parsed) && Enum.IsDefined(parsed)
            ? Ok(insights.GetByPersona(parsed))
            : Ok(new List<Insight>());
    }
}
