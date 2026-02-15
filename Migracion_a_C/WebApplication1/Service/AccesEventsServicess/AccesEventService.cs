using Dominio;
using IServices.IAccesEvent;
using Models.Dominio;
using Models.WebApi;

namespace Service.AccesEventsServicess;

public class AccesEventService(
    IAccesEventEntityService entityService,
    IAccesEventValidationService validationService,
    IAccesEventMantenimientoService mantenimientoService) : IAccesEventService
{
    private readonly IAccesEventEntityService _entity = entityService;
    private readonly IAccesEventValidationService _validation = validationService;
    private readonly IAccesEventMantenimientoService _mantenimiento = mantenimientoService;

    public AccessEvents ToEntity(AccesEventDto dto)
    {
        Validar(dto);
        return _entity.ToEntity(dto);
    }

    public AccesEventDto FromEntity(AccessEvents accessEvent)
    {
        var dto = _entity.FromEntity(accessEvent);
        Validar(dto);
        return dto;
    }

    public void Validar(AccesEventDto dto)
    {
        _validation.Validar(dto);
    }

    public PushIngestResultDto ProcesarPush(HikvisionPushEnvelopeDto envelope, PushAuthContext authContext)
    {
        _validation.ValidarEnvelope(envelope);
        return _mantenimiento.ProcesarPush(envelope, authContext);
    }
}
