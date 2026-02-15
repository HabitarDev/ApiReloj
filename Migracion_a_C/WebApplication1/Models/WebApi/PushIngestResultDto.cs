namespace Models.WebApi;

public class PushIngestResultDto
{
    public string Status { get; set; } = null!;
    public string? Reason { get; set; }
    public string? EventType { get; set; }
    public long? SerialNo { get; set; }
    public string? DeviceSn { get; set; }
    public DateTimeOffset? EventTimeUtc { get; set; }
}
