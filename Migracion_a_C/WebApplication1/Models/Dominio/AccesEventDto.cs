namespace Models.Dominio;

public class AccesEventDto
{
    public string _deviceSn { get; set; } = null!;
    public long _serialNumber { get; set; }
    public DateTimeOffset _eventTimeUtc { get; set; }
    public string? _timeDevice { get; set; }
    public string? _employeeNumber { get; set; }
    public int _major { get; set; }
    public int _minor { get; set; }
    public string? _attendanceStatus { get; set; }
    public string _raw { get; set; } = null!;
}
