namespace Models.WebApi;

public class PushAuthContext
{
    public const string HttpContextItemKey = "__PushAuthContext";

    public int RelojId { get; set; }
    public int ResidentialId { get; set; }
    public string DeviceSn { get; set; } = null!;
    public string RemoteIp { get; set; } = null!;
}
