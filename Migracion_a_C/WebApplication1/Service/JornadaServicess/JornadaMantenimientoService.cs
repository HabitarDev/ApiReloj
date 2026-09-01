using Dominio;
using IDataAcces;
using IServices.IJornada;
using Microsoft.Extensions.Logging;
using Models.Dominio;
using Models.WebApi;

namespace Service.JornadaServicess;

public class JornadaMantenimientoService(
    IJornadasRepository jornadasRepository,
    IJornadaProjectionStateRepository projectionStateRepository,
    IJornadaProjectionService projectionService,
    IJornadaEntityService jornadaEntityService,
    ILogger<JornadaMantenimientoService> logger) : IJornadaMantenimientoService
{
    private readonly IJornadasRepository _jornadasRepository = jornadasRepository;
    private readonly IJornadaProjectionStateRepository _projectionStateRepository = projectionStateRepository;
    private readonly IJornadaProjectionService _projectionService = projectionService;
    private readonly IJornadaEntityService _entity = jornadaEntityService;
    private readonly ILogger<JornadaMantenimientoService> _logger = logger;

    // Compatibilidad interna: la ingesta nueva encola dentro de su propia
    // transaccion. Este metodo queda seguro para otros consumidores del servicio.
    public void ProcesarEventoInsertado(AccessEvents accessEvent)
    {
        if (string.IsNullOrWhiteSpace(accessEvent.EmployeeNumber)
            || string.IsNullOrWhiteSpace(accessEvent.ResidentialId))
        {
            _logger.LogWarning(
                "Evento sin employee/residential; no se encola jornada. DeviceSn={DeviceSn}, SerialNo={SerialNo}",
                accessEvent.DeviceSn,
                accessEvent.SerialNumber);
            return;
        }

        _projectionStateRepository.Enqueue(
            accessEvent.EmployeeNumber,
            accessEvent.ResidentialId,
            accessEvent.EventTimeUtc);
    }

    public int MarcarIncompletasVencidasComoError(DateTimeOffset nowUtc)
    {
        // El estado nunca se muta incrementalmente: se solicita una nueva
        // proyeccion para que el limite de 24 h se aplique deterministamente.
        return _projectionService.EnqueueExpired(nowUtc);
    }

    public List<JornadaDto> Buscar(JornadasQueryDto query)
    {
        var rows = _jornadasRepository.Search(
            query.FromUtc,
            query.ToUtc,
            query.UpdatedSinceUtc,
            query.EmployeeNumber,
            query.ResidentialId,
            query.ClockSn,
            query.StatusCheck,
            query.StatusBreak,
            query.ProjectionStatus,
            query.IncludeDeleted,
            query.Limit,
            query.Offset);

        return rows.Select(_entity.FromEntity).ToList();
    }
}
