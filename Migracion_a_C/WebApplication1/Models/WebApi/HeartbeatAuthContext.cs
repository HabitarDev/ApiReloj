namespace Models.WebApi;

public sealed class HeartbeatAuthContext
{
    public const string HttpContextItemKey = "__HeartbeatAuthContext";

    public string DeviceId { get; init; } = null!;
    public string ResidentialId { get; init; } = null!;
    public long Timestamp { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }
}
