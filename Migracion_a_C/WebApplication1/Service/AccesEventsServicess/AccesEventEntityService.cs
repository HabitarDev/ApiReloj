using System.Text.Json;
using Dominio;
using IDataAcces;
using IServices.IAccesEvent;
using Models.Dominio;
using Models.WebApi;

namespace Service.AccesEventsServicess;

public class AccesEventEntityService(IAccesEventsRepository repo) : IAccesEventEntityService
{
    private readonly IAccesEventsRepository _repo = repo;

    public AccessEvents ToEntity(AccesEventDto dto)
    {
        var existing = _repo.GetBySerialNo(dto._serialNumber)
            .FirstOrDefault(x => x.DeviceSn == dto._deviceSn);

        if (existing == null)
        {
            return new AccessEvents(
                dto._deviceSn,
                dto._serialNumber,
                dto._eventTimeUtc,
                dto._timeDevice,
                dto._employeeNumber,
                dto._major,
                dto._minor,
                dto._attendanceStatus,
                dto._raw);
        }
        return existing;
    }

    public AccesEventDto FromEntity(AccessEvents accessEvent)
    {
        return new AccesEventDto
        {
            _deviceSn = accessEvent.DeviceSn,
            _serialNumber = accessEvent.SerialNumber,
            _eventTimeUtc = accessEvent.EventTimeUtc,
            _timeDevice = accessEvent.TimeDevice,
            _employeeNumber = accessEvent.EmployeeNumber,
            _major = accessEvent.Major,
            _minor = accessEvent.Minor,
            _attendanceStatus = accessEvent.AttendanceStatus,
            _raw = accessEvent.Raw
        };
    }

    public AccesEventDto NormalizarDesdePush(
        HikvisionEventNotificationAlertDto source,
        string deviceSn,
        string contentType,
        bool hasPicture,
        string rawPayload)
    {
        if (source.AccessControllerEvent == null)
        {
            throw new ArgumentException("El payload no contiene AccessControllerEvent");
        }

        if (!DateTimeOffset.TryParse(source.DateTime, out var eventTime))
        {
            throw new ArgumentException("dateTime invalido en el payload push");
        }

        var employeeNumber = source.AccessControllerEvent.EmployeeNoString;
        if (string.IsNullOrWhiteSpace(employeeNumber) && source.AccessControllerEvent.EmployeeNo.HasValue)
        {
            employeeNumber = source.AccessControllerEvent.EmployeeNo.Value.ToString();
        }

        var rawEnvelopeJson = BuildRawEnvelopeJson(
            source: "push",
            format: DetectFormat(contentType, rawPayload),
            contentType: contentType,
            hasPicture: hasPicture,
            payload: rawPayload);

        return new AccesEventDto
        {
            _deviceSn = deviceSn,
            _serialNumber = source.AccessControllerEvent.SerialNo ?? 0,
            _eventTimeUtc = eventTime.ToUniversalTime(),
            _timeDevice = source.DateTime,
            _employeeNumber = employeeNumber,
            _major = source.AccessControllerEvent.MajorEventType ?? 0,
            _minor = source.AccessControllerEvent.SubEventType ?? 0,
            _attendanceStatus = source.AccessControllerEvent.AttendanceStatus,
            _raw = rawEnvelopeJson
        };
    }

    public AccesEventDto NormalizarDesdePoll(HikvisionAcsEventInfoDto source, string deviceSn)
    {
        if (source.SerialNo is null || source.SerialNo <= 0)
        {
            throw new ArgumentException("serialNo invalido en payload poll");
        }

        if (string.IsNullOrWhiteSpace(source.Time) || !DateTimeOffset.TryParse(source.Time, out var eventTime))
        {
            throw new ArgumentException("time invalido en payload poll");
        }

        var payload = JsonSerializer.Serialize(source);

        var rawEnvelopeJson = BuildRawEnvelopeJson(
            source: "poll",
            format: "json",
            contentType: "application/json",
            hasPicture: false,
            payload: payload);

        return new AccesEventDto
        {
            _deviceSn = deviceSn,
            _serialNumber = source.SerialNo.Value,
            _eventTimeUtc = eventTime.ToUniversalTime(),
            _timeDevice = source.Time,
            _employeeNumber = source.EmployeeNoString,
            _major = source.Major ?? 0,
            _minor = source.Minor ?? 0,
            _attendanceStatus = source.AttendanceStatus,
            _raw = rawEnvelopeJson
        };
    }

    private static string BuildRawEnvelopeJson(
        string source,
        string format,
        string contentType,
        bool hasPicture,
        string payload)
    {
        var envelope = new AccessEventRawEnvelopeDto
        {
            Source = source,
            Format = format,
            ContentType = contentType,
            HasPicture = hasPicture,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Payload = payload
        };

        return JsonSerializer.Serialize(envelope);
    }

    private static string DetectFormat(string? contentType, string rawPayload)
    {
        var ct = (contentType ?? string.Empty).ToLowerInvariant();
        if (ct.Contains("application/json"))
        {
            return "json";
        }

        if (ct.Contains("application/xml") || ct.Contains("text/xml"))
        {
            return "xml";
        }

        var trimmed = rawPayload.TrimStart();
        if (trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            return "json";
        }

        if (trimmed.StartsWith("<", StringComparison.Ordinal))
        {
            return "xml";
        }

        return "unknown";
    }
}
