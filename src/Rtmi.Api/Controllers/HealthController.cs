using Microsoft.AspNetCore.Mvc;
using Rtmi.Api.Models;

namespace Rtmi.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(IMatchControl control, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public ActionResult<HealthReport> Get()
    {
        var foundryStatus = configuration.GetValue("Foundry:UseFallbackOnly", false)
            ? "FallbackOnly"
            : string.IsNullOrWhiteSpace(configuration["Foundry:Endpoint"])
                ? "NotConfigured"
                : "Configured";
        return Ok(new HealthReport("Healthy", "Healthy", control.Status, foundryStatus));
    }
}
