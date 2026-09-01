using IServices.IJornada;
using Microsoft.AspNetCore.Mvc;
using Models.WebApi;

namespace WebApplication1.Controllers;

[ApiController]
[Route("admin/jornadas")]
public class AdminJornadasController(IJornadaProjectionService projectionService) : ControllerBase
{
    private readonly IJornadaProjectionService _projectionService = projectionService;

    [HttpPost("rebuild")]
    public IActionResult Rebuild([FromBody] JornadaRebuildRequestDto request)
    {
        _projectionService.RequestRebuild(request);
        return Accepted(new
        {
            status = "queued",
            request.EmployeeNumber,
            request.ResidentialId,
            dirtyFromUtc = request.FromUtc
        });
    }

    [HttpGet("projection-states")]
    public ActionResult<List<JornadaProjectionStateDto>> ProjectionStates(
        [FromQuery] string? status = null,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0)
    {
        return Ok(_projectionService.GetStates(status, limit, offset));
    }
}
