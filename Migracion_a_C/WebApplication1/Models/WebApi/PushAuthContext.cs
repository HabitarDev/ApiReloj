namespace Models.WebApi;

public class PushAuthContext
{
    public const string HttpContextItemKey = "__PushAuthContext";

    public string RelojId { get; set; } = null!;
    public string ResidentialId { get; set; } = null!;
    public string DeviceSn { get; set; } = null!;
    public string RemoteIp { get; set; } = null!;
}
