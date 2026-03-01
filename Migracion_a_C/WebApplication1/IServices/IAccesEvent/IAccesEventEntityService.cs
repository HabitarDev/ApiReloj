using Dominio;
using Models.Dominio;
using Models.WebApi;

namespace IServices.IAccesEvent;

public interface IAccesEventEntityService
{
    AccessEvents ToEntity(AccesEventDto dto);
    AccesEventDto FromEntity(AccessEvents accessEvent);
    AccesEventDto NormalizarDesdePush(
        HikvisionEventNotificationAlertDto source,
        string deviceSn,
        string contentType,
        bool hasPicture,
        string rawPayload);
    AccesEventDto NormalizarDesdePoll(HikvisionAcsEventInfoDto source, string deviceSn);
}
