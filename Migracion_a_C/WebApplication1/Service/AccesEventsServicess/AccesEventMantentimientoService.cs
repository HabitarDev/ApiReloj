using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Dominio;
using IDataAcces;
using IServices.IAccesEvent;
using Models.Dominio;
using Models.WebApi;

namespace Service.AccesEventsServicess;

public class AccesEventMantentimientoService(
    IAccesEventsRepository accessEventsRepository,
    IRelojesRepository relojesRepository,
    IAccesEventEntityService entityService,
    IAccesEventValidationService validationService) : IAccesEventMantenimientoService
{
    private readonly IAccesEventsRepository _accessEventsRepository = accessEventsRepository;
    private readonly IRelojesRepository _relojesRepository = relojesRepository;
    private readonly IAccesEventEntityService _entityService = entityService;
    private readonly IAccesEventValidationService _validationService = validationService;

    public PushIngestResultDto ProcesarPush(HikvisionPushEnvelopeDto envelope, PushAuthContext authContext)
    {
        var payload = ParsePushPayload(envelope.ContentType, envelope.EventPayloadRaw);
        _validationService.ValidarEventoPush(payload);

        var eventType = payload.EventType;
        if (!eventType!.Equals("AccessControllerEvent", StringComparison.OrdinalIgnoreCase))
        {
            return new PushIngestResultDto
            {
                Status = "ignored",
                Reason = "unsupported_event_type",
                EventType = eventType
            };
        }

        if (!payload.AccessControllerEvent!.SerialNo.HasValue || payload.AccessControllerEvent.SerialNo.Value <= 0)
        {
            return new PushIngestResultDto
            {
                Status = "ignored",
                Reason = "missing_serial_no",
                EventType = eventType
            };
        }

        var normalized = _entityService.NormalizarDesdePush(payload, authContext.DeviceSn, envelope.EventPayloadRaw);
        _validationService.Validar(normalized);

        AccessEvents accessEvent = _entityService.ToEntity(normalized);
        var inserted = _accessEventsRepository.AddIfNotExists(accessEvent);
        UpdateLastPushEvent(authContext.RelojId, normalized._eventTimeUtc);

        var status = inserted ? "inserted" : "duplicate";
        return new PushIngestResultDto
        {
            Status = status,
            EventType = eventType,
            SerialNo = normalized._serialNumber,
            DeviceSn = normalized._deviceSn,
            EventTimeUtc = normalized._eventTimeUtc
        };
    }

    public List<AccesEventDto> ListarTodos()
    {
        List<AccessEvents> lista = _accessEventsRepository.GetAll();
        List<AccesEventDto> listaARetornar = new List<AccesEventDto>();
        foreach (var evento in lista)
        {
            AccesEventDto eventoDto = _entityService.FromEntity(evento);
            listaARetornar.Add(eventoDto);
        }

        return listaARetornar;
    }

    private void UpdateLastPushEvent(int relojId, DateTimeOffset eventTimeUtc)
    {
        var reloj = _relojesRepository.GetById(relojId);
        if (reloj == null)
        {
            throw new InvalidOperationException("Reloj inexistente");
        }

        if (!reloj.LastPushEvent.HasValue || eventTimeUtc > reloj.LastPushEvent.Value)
        {
            reloj.LastPushEvent = eventTimeUtc;
            _relojesRepository.update(reloj);
        }
    }

    private static HikvisionEventNotificationAlertDto ParsePushPayload(string contentType, string rawPayload)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            throw new ArgumentException("Payload vacio");
        }

        var ct = (contentType ?? string.Empty).ToLowerInvariant();
        if (ct.Contains("application/json"))
        {
            return ParseJson(rawPayload);
        }

        if (ct.Contains("multipart/form-data"))
        {
            return rawPayload.TrimStart().StartsWith("{", StringComparison.Ordinal)
                ? ParseJson(rawPayload)
                : ParseXml(rawPayload);
        }

        if (ct.Contains("application/xml") || ct.Contains("text/xml"))
        {
            return ParseXml(rawPayload);
        }

        return rawPayload.TrimStart().StartsWith("{", StringComparison.Ordinal)
            ? ParseJson(rawPayload)
            : ParseXml(rawPayload);
    }

    private static HikvisionEventNotificationAlertDto ParseJson(string raw)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var direct = JsonSerializer.Deserialize<HikvisionEventNotificationAlertDto>(raw, options);
        if (direct?.EventType != null || direct?.AccessControllerEvent != null)
        {
            return direct;
        }

        var wrapped = JsonSerializer.Deserialize<EventNotificationAlertWrapper>(raw, options);
        if (wrapped?.EventNotificationAlert != null)
        {
            return wrapped.EventNotificationAlert;
        }

        throw new ArgumentException("No se pudo parsear el JSON de push");
    }

    private static HikvisionEventNotificationAlertDto ParseXml(string raw)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(raw);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("No se pudo parsear el XML de push", ex);
        }

        var alert = doc.Root;
        if (alert == null)
        {
            throw new ArgumentException("XML push vacio");
        }

        if (!alert.Name.LocalName.Equals("EventNotificationAlert", StringComparison.OrdinalIgnoreCase))
        {
            alert = doc.Descendants().FirstOrDefault(x =>
                x.Name.LocalName.Equals("EventNotificationAlert", StringComparison.OrdinalIgnoreCase));
            if (alert == null)
            {
                throw new ArgumentException("XML push sin nodo EventNotificationAlert");
            }
        }

        var ace = alert.Elements().FirstOrDefault(x =>
            x.Name.LocalName.Equals("AccessControllerEvent", StringComparison.OrdinalIgnoreCase));

        var accessController = ace == null
            ? null
            : new HikvisionAccessControllerEventDto
            {
                DeviceName = ElementValue(ace, "deviceName"),
                MajorEventType = TryInt(ElementValue(ace, "majorEventType")),
                SubEventType = TryInt(ElementValue(ace, "subEventType")),
                CardReaderKind = TryInt(ElementValue(ace, "cardReaderKind")),
                EmployeeNoString = ElementValue(ace, "employeeNoString"),
                EmployeeNo = TryLong(ElementValue(ace, "employeeNo")),
                SerialNo = TryLong(ElementValue(ace, "serialNo")),
                FrontSerialNo = TryInt(ElementValue(ace, "frontSerialNo")),
                CurrentVerifyMode = ElementValue(ace, "currentVerifyMode"),
                AttendanceStatus = ElementValue(ace, "attendanceStatus")
            };

        return new HikvisionEventNotificationAlertDto
        {
            DateTime = ElementValue(alert, "dateTime"),
            EventType = ElementValue(alert, "eventType"),
            EventState = ElementValue(alert, "eventState"),
            EventDescription = ElementValue(alert, "eventDescription"),
            DeviceID = ElementValue(alert, "deviceID"),
            AccessControllerEvent = accessController
        };
    }

    private static string? ElementValue(XElement parent, string localName)
    {
        return parent.Elements()
            .FirstOrDefault(x => x.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private static int? TryInt(string? value)
    {
        if (int.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static long? TryLong(string? value)
    {
        if (long.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private class EventNotificationAlertWrapper
    {
        [JsonPropertyName("EventNotificationAlert")]
        public HikvisionEventNotificationAlertDto? EventNotificationAlert { get; set; }
    }
}
