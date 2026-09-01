using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

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

public sealed class ProxySecurityOptions
{
    public const string SectionName = "Security:Proxy";

    public bool Enabled { get; set; }
    public int ForwardLimit { get; set; } = 1;
    public List<string> KnownProxies { get; set; } = [];
    public List<string> KnownNetworks { get; set; } = [];

    public bool IsValid()
    {
        if (!Enabled)
        {
            return true;
        }

        return ForwardLimit is > 0 and <= 10
               && (KnownProxies.Count > 0 || KnownNetworks.Count > 0)
               && KnownProxies.All(value => IPAddress.TryParse(value, out _))
               && KnownNetworks.All(value => System.Net.IPNetwork.TryParse(value, out _));
    }

    public void ApplyTo(ForwardedHeadersOptions target)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        target.ForwardLimit = ForwardLimit;
        target.KnownProxies.Clear();
        target.KnownIPNetworks.Clear();

        foreach (var proxy in KnownProxies)
        {
            target.KnownProxies.Add(IPAddress.Parse(proxy));
        }

        foreach (var network in KnownNetworks)
        {
            target.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        }
    }
}
