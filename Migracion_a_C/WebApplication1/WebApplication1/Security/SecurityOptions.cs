namespace WebApplication1.Security;

public sealed class BackendSecurityOptions
{
    public const string SectionName = "Security:Backend";

    public string ApiKey { get; set; } = string.Empty;
    public string AllowedIp { get; set; } = string.Empty;
}

public sealed class HeartbeatSecurityOptions
{
    public const string SectionName = "Security:Heartbeat";

    public int AllowedClockSkewSeconds { get; set; } = 300;
    public int MaximumBodySizeBytes { get; set; } = 8192;
    public int PermitLimitPerIp { get; set; } = 600;
    public int RateWindowSeconds { get; set; } = 60;
    public int GlobalConcurrencyLimit { get; set; } = 200;
}
