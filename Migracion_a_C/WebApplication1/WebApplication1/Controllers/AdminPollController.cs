using IServices.IBackfillPoll;
using Microsoft.AspNetCore.Mvc;
using Models.WebApi;

namespace WebApplication1.Controllers;

[ApiController]
[Route("admin/poll")]
public class AdminPollController(IBackfillPollService service) : ControllerBase
{
    private readonly IBackfillPollService _service = service;

    [HttpPost("run")]
    public async Task<ActionResult<BackfillPollRunResultDto>> Run(
        [FromBody] BackfillPollRunRequestDto? request,
        CancellationToken ct)
    {
        var safe = request ?? new BackfillPollRunRequestDto();
        safe.Trigger = "manual";
        var result = await _service.EjecutarAsync(safe, ct);
        return Ok(result);
    }

    [HttpGet("status")]
    public ActionResult<BackfillPollStatusDto> Status()
    {
        return Ok(_service.ObtenerEstado());
    }

    [HttpGet("runs")]
    public ActionResult<List<BackfillPollRunSummaryDto>> Runs([FromQuery] BackfillPollRunsQueryDto? query)
    {
        return Ok(_service.ListarRuns(query ?? new BackfillPollRunsQueryDto()));
    }

    [HttpGet("runs/{runId}")]
    public ActionResult<BackfillPollRunResultDto> RunById([FromRoute] string runId)
    {
        return Ok(_service.ObtenerRun(runId));
    }
}
