using Dominio;
using Models.Dominio;
using Models.WebApi;

namespace IServices.IAccesEvent;

public interface IAccesEventService
{
    AccessEvents ToEntity(AccesEventDto dto);
    AccesEventDto FromEntity(AccessEvents accessEvent);
    void Validar(AccesEventDto dto);
    List<AccesEventDto> ListarTodos();
    List<AccesEventDto> Buscar(AccessEventsQueryDto query);
    PushIngestResultDto ProcesarPush(HikvisionPushEnvelopeDto envelope, PushAuthContext authContext);
}
