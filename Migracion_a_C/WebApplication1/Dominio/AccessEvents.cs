namespace Dominio;

public class AccessEvents
{
    private string _device_sn;
    private int _serial_number;
    private DateTime _event_time_utc;
    private string _time_device;
    private int _employee_number;
    private int _major;
    private int _minor;
    private string _attendance_status;
    private string _raw;
    
    public AccessEvents(string  device_sn,  int serial_number, DateTime event_time_utc, string time_device,  int employee_number, int major, int minor, string attendance_status, string raw)
    {
        _device_sn = device_sn;
        _serial_number = serial_number;
        _event_time_utc = event_time_utc;
        _time_device = time_device;
        _employee_number = employee_number;
        _major = major;
        _minor = minor;
        _attendance_status = attendance_status;
        _raw = raw;
    }

    public string DeviceSn
    {
        get => _device_sn;
        set => _device_sn = value;
    }
    
    public int SerialNumber
    {
        get => _serial_number;
        set => _serial_number = value;
    }
    
    public DateTime EventTimeUtc
    {
        get => _event_time_utc;
        set => _event_time_utc = value;
    }

    public string TimeDevice
    {
        get => _time_device;
        set => _time_device = value;
    }

    public int EmployeeNumber
    {
        get => _employee_number;
        set => _employee_number = value;
    }
    
    public int Major
    {
        get => _major;
        set => _major = value;
    }
    
    public int Minor
    {
        get => _minor;
        set => _minor = value;
    }
    
    public string AttendanceStatus
    {
        get => _attendance_status;
        set => _attendance_status = value;
    }
    
    public string Raw
    {
        get => _raw;
        set => _raw = value;
    }
}