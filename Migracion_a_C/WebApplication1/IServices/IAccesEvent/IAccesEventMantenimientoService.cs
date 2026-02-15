using Models.WebApi;

namespace IServices.IAccesEvent;

public interface IAccesEventMantenimientoService
{
    PushIngestResultDto ProcesarPush(HikvisionPushEnvelopeDto envelope, PushAuthContext authContext);
}
