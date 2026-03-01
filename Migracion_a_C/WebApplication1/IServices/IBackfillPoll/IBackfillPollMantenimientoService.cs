using Models.WebApi;

namespace IServices.IBackfillPoll;

public interface IBackfillPollMantenimientoService
{
    Task<BackfillPollRunResultDto> EjecutarAsync(BackfillPollRunRequestDto request, CancellationToken ct);
    BackfillPollStatusDto ObtenerEstado();
    List<BackfillPollRunSummaryDto> ListarRuns(BackfillPollRunsQueryDto query);
    BackfillPollRunResultDto ObtenerRun(string runId);
}
