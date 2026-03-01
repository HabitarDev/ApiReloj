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

    public void ValidarBusqueda(AccessEventsQueryDto query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var hasFrom = query.FromUtc.HasValue;
        var hasTo = query.ToUtc.HasValue;

        if (hasFrom != hasTo)
        {
            throw new ArgumentException("Para filtrar por fecha se requieren fromUtc y toUtc juntos");
        }

        if (hasFrom && query.FromUtc!.Value > query.ToUtc!.Value)
        {
            throw new ArgumentException("El rango de fechas es invalido");
        }

        if (query.Limit <= 0)
        {
            throw new ArgumentException("limit debe ser mayor a 0");
        }

        if (query.Offset < 0)
        {
            throw new ArgumentException("offset debe ser mayor o igual a 0");
        }

        if (query.Major.HasValue && query.Major.Value < 0)
        {
            throw new ArgumentException("major debe ser mayor o igual a 0");
        }

        if (query.Minor.HasValue && query.Minor.Value < 0)
        {
            throw new ArgumentException("minor debe ser mayor o igual a 0");
        }

        if (!string.IsNullOrWhiteSpace(query.AttendanceStatus))
        {
            query.AttendanceStatus = query.AttendanceStatus.Trim();
        }
    }
}
