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

    public AccesEventDto NormalizarDesdePush(HikvisionEventNotificationAlertDto source, string deviceSn, string rawPayload)
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
            _raw = rawPayload
        };
    }
}
