namespace Dominio;

public class AccessEvents
{
    // EF necesita ctor vacío
    public AccessEvents() { }

    public AccessEvents(
        string deviceSn,
        long serialNumber,
        DateTimeOffset eventTimeUtc,
        string? timeDevice,
        string? employeeNumber,
        int major,
        int minor,
        string? attendanceStatus,
        string raw)
    {
        DeviceSn = deviceSn;
        SerialNumber = serialNumber;
        EventTimeUtc = eventTimeUtc;
        TimeDevice = timeDevice;
        EmployeeNumber = employeeNumber;
        Major = major;
        Minor = minor;
        AttendanceStatus = attendanceStatus;
        Raw = raw;
    }

    public string DeviceSn { get; set; } = null!;
    public long SerialNumber { get; set; }
    public DateTimeOffset EventTimeUtc { get; set; }
    public string? TimeDevice { get; set; }
    public string? EmployeeNumber { get; set; }
    public int Major { get; set; }
    public int Minor { get; set; }
    public string? AttendanceStatus { get; set; }
    public string Raw { get; set; } = null!;
}
