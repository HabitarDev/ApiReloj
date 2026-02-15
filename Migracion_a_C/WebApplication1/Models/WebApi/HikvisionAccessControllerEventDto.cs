namespace Models.WebApi;

public class HikvisionAccessControllerEventDto
{
    public string? DeviceName { get; set; }
    public int? MajorEventType { get; set; }
    public int? SubEventType { get; set; }
    public int? CardReaderKind { get; set; }
    public string? EmployeeNoString { get; set; }
    public long? EmployeeNo { get; set; }
    public long? SerialNo { get; set; }
    public int? FrontSerialNo { get; set; }
    public string? CurrentVerifyMode { get; set; }
    public string? AttendanceStatus { get; set; }
}
