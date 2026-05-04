namespace Models.WebApi;

public class HikvisionPushEnvelopeDto
{
    public string RelojId { get; set; } = null!;
    public string RemoteIp { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public string EventPayloadRaw { get; set; } = null!;
    public bool HasPicture { get; set; }
}
