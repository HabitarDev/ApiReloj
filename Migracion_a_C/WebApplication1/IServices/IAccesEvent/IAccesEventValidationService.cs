using Models.Dominio;
using Models.WebApi;

namespace IServices.IAccesEvent;

public interface IAccesEventValidationService
{
    void Validar(AccesEventDto dto);
    void ValidarEnvelope(HikvisionPushEnvelopeDto envelope);
    void ValidarEventoPush(HikvisionEventNotificationAlertDto payload);
    void ValidarBusqueda(AccessEventsQueryDto query);
}
