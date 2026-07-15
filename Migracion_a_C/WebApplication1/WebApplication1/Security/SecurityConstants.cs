namespace WebApplication1.Security;

public static class SecuritySchemes
{
    public const string Backend = "BackendApiKey";
    public const string Heartbeat = "HeartbeatHmac";
    public const string ResidentialPush = "ResidentialPush";
}

public static class SecurityPolicies
{
    public const string Backend = "Backend";
    public const string Heartbeat = "Heartbeat";
    public const string ResidentialPush = "ResidentialPush";
}

public static class RateLimitingPolicies
{
    public const string Heartbeat = "Heartbeat";
}
