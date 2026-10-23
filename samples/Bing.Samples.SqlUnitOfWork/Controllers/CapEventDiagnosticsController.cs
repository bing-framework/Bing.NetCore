using System.Text;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;

namespace Bing.Samples.SqlUnitOfWork.Controllers;

[ApiController]
[Route("diagnostics/events")]
public sealed class CapEventDiagnosticsController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;

    public CapEventDiagnosticsController(IWebHostEnvironment environment) => _environment = environment;

    [HttpGet]
    [Produces("text/html")]
    public IActionResult Page()
    {
        var path = Path.Combine(_environment.ContentRootPath, "wwwroot", "diagnostics", "events.html");
        return PhysicalFile(path, "text/html; charset=utf-8");
    }
}

[ApiController]
[Route("api/diagnostics/events")]
public sealed class CapEventDiagnosticsApiController : ControllerBase
{
    private readonly CapEventDiagnosticsService _diagnostics;

    public CapEventDiagnosticsApiController(CapEventDiagnosticsService diagnostics) =>
        _diagnostics = diagnostics;

    [HttpGet]
    public async Task<ActionResult<CapEventsDiagnosticsSnapshot>> Get([FromQuery] string? operationId,
        [FromQuery] string? eventId, [FromQuery] string? traceId, [FromQuery] string? source,
        [FromQuery] string? status, CancellationToken cancellationToken) =>
        Ok(await _diagnostics.ReadAsync(operationId, eventId, traceId, source, status, cancellationToken));
}
