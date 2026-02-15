namespace Models.WebApi;

public class HikvisionEventNotificationAlertDto
{
    public string? DateTime { get; set; }
    public string? EventType { get; set; }
    public string? EventState { get; set; }
    public string? EventDescription { get; set; }
    public string? DeviceID { get; set; }
    public HikvisionAccessControllerEventDto? AccessControllerEvent { get; set; }
}
