using Models.Dominio;
using Models.WebApi;

namespace IServices.IAccesEvent;

public interface IAccesEventMantenimientoService
{
    PushIngestResultDto ProcesarPush(HikvisionPushEnvelopeDto envelope, PushAuthContext authContext);
    List<AccesEventDto> ListarTodos();
    List<AccesEventDto> Buscar(AccessEventsQueryDto query);
    PollIngestResultDto ProcesarEventosDesdePoll(
        int relojId,
        string deviceSn,
        IReadOnlyCollection<HikvisionAcsEventInfoDto> infoList);
}
