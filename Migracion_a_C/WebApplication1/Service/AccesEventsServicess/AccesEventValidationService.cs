using IServices.IAccesEvent;
using Models.Dominio;
using Models.WebApi;

namespace Service.AccesEventsServicess;

public class AccesEventValidationService : IAccesEventValidationService
{
    public void Validar(AccesEventDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto._deviceSn))
        {
            throw new ArgumentException("DeviceSn invalido");
        }

        if (dto._serialNumber <= 0)
        {
            throw new ArgumentException("SerialNumber invalido");
        }

        if (dto._eventTimeUtc == default)
        {
            throw new ArgumentException("EventTimeUtc invalido");
        }

        if (string.IsNullOrWhiteSpace(dto._raw))
        {
            throw new ArgumentException("Raw invalido");
        }

        if (dto._major < 0)
        {
            throw new ArgumentException("Major invalido");
        }

        if (dto._minor < 0)
        {
            throw new ArgumentException("Minor invalido");
        }
    }

    public void ValidarEnvelope(HikvisionPushEnvelopeDto envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.RelojId <= 0)
        {
            throw new ArgumentException("relojId invalido");
        }

        if (string.IsNullOrWhiteSpace(envelope.RemoteIp))
        {
            throw new ArgumentException("RemoteIp invalida");
        }

        if (string.IsNullOrWhiteSpace(envelope.ContentType))
        {
            throw new ArgumentException("Content-Type invalido");
        }

        if (string.IsNullOrWhiteSpace(envelope.EventPayloadRaw))
        {
            throw new ArgumentException("Payload de evento vacio");
        }

        var ct = envelope.ContentType.ToLowerInvariant();
        var isValid = ct.Contains("application/json")
                      || ct.Contains("application/xml")
                      || ct.Contains("text/xml")
                      || ct.Contains("multipart/form-data");
        if (!isValid)
        {
            throw new ArgumentException("Content-Type no soportado para push");
        }
    }

    public void ValidarEventoPush(HikvisionEventNotificationAlertDto payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (string.IsNullOrWhiteSpace(payload.EventType))
        {
            throw new ArgumentException("eventType invalido en payload push");
        }

        if (!payload.EventType.Equals("AccessControllerEvent", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (payload.AccessControllerEvent == null)
        {
            throw new ArgumentException("AccessControllerEvent invalido en payload push");
        }

        if (string.IsNullOrWhiteSpace(payload.DateTime)
            || !DateTimeOffset.TryParse(payload.DateTime, out _))
        {
            throw new ArgumentException("dateTime invalido en payload push");
        }
    }
}
