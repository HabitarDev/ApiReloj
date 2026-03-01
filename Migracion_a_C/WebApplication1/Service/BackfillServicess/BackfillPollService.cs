using IServices.IBackfillPoll;
using Models.WebApi;

namespace Service.BackfillServicess;

public class BackfillPollService(
    IBackfillPollValidationService validation,
    IBackfillPollMantenimientoService mantenimiento) : IBackfillPollService
{
    private readonly IBackfillPollValidationService _validation = validation;
    private readonly IBackfillPollMantenimientoService _mantenimiento = mantenimiento;

    public async Task<BackfillPollRunResultDto> EjecutarAsync(BackfillPollRunRequestDto request, CancellationToken ct)
    {
        var safe = request ?? new BackfillPollRunRequestDto();
        _validation.Validar(safe);
        return await _mantenimiento.EjecutarAsync(safe, ct);
    }

    public BackfillPollStatusDto ObtenerEstado()
    {
        return _mantenimiento.ObtenerEstado();
    }

    public List<BackfillPollRunSummaryDto> ListarRuns(BackfillPollRunsQueryDto query)
    {
        var safe = query ?? new BackfillPollRunsQueryDto();
        _validation.ValidarHistorial(safe);
        return _mantenimiento.ListarRuns(safe);
    }

    public BackfillPollRunResultDto ObtenerRun(string runId)
    {
        _validation.ValidarRunId(runId);
        return _mantenimiento.ObtenerRun(runId);
    }
}
