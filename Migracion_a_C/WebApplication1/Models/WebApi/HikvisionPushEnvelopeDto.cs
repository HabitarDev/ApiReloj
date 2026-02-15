namespace Models.WebApi;

public class HikvisionPushEnvelopeDto
{
    public int RelojId { get; set; }
    public string RemoteIp { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public string EventPayloadRaw { get; set; } = null!;
    public bool HasPicture { get; set; }
}
