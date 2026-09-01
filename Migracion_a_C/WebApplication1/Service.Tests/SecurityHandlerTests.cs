using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dominio;
using IDataAcces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.Dominio;
using Models.WebApi;
using WebApplication1.Security;
using Xunit;

namespace Service.Tests;

public class SecurityHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 15, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Heartbeat_AuthenticatesCurrentSignedBodyAndRestoresStream()
    {
        const string secret = "device-secret";
        var body = HeartbeatBody(secret, Now.ToUnixTimeSeconds());
        var httpContext = HeartbeatHttpContext(body);
        var handler = HeartbeatHandler(secret);
        await Initialize(handler, SecuritySchemes.Heartbeat, httpContext);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(0, httpContext.Request.Body.Position);
        var authContext = Assert.IsType<HeartbeatAuthContext>(
            httpContext.Items[HeartbeatAuthContext.HttpContextItemKey]);
        Assert.Equal("DEVICE-1", authContext.DeviceId);
        Assert.Equal("RES-1", authContext.ResidentialId);

        var deserialized = await JsonSerializer.DeserializeAsync<HeartBeatDto>(
            httpContext.Request.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            TestContext.Current.CancellationToken);
        Assert.Equal("DEVICE-1", deserialized!.DeviceId);
    }

    [Theory]
    [InlineData("invalid-signature", 0)]
    [InlineData("valid", -301)]
    [InlineData("valid", 301)]
    public async Task Heartbeat_RejectsBadSignatureOrTimestamp(string signatureMode, int offsetSeconds)
    {
        const string secret = "device-secret";
        var timestamp = Now.AddSeconds(offsetSeconds).ToUnixTimeSeconds();
        var body = signatureMode == "valid"
            ? HeartbeatBody(secret, timestamp)
            : HeartbeatBody(secret, timestamp, "00");
        var httpContext = HeartbeatHttpContext(body);
        var handler = HeartbeatHandler(secret);
        await Initialize(handler, SecuritySchemes.Heartbeat, httpContext);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(0, httpContext.Request.Body.Position);
        Assert.False(httpContext.Items.ContainsKey(HeartbeatAuthContext.HttpContextItemKey));
    }

    [Fact]
    public async Task Backend_RequiresTheConfiguredApiKey()
    {
        var validContext = new DefaultHttpContext();
        validContext.Request.Headers[BackendApiKeyAuthenticationHandler.ApiKeyHeader] = "backend-secret";
        var valid = BackendHandler();
        await Initialize(valid, SecuritySchemes.Backend, validContext);

        var invalidContext = new DefaultHttpContext();
        invalidContext.Request.Headers[BackendApiKeyAuthenticationHandler.ApiKeyHeader] = "wrong";
        var invalid = BackendHandler();
        await Initialize(invalid, SecuritySchemes.Backend, invalidContext);

        Assert.True((await valid.AuthenticateAsync()).Succeeded);
        Assert.False((await invalid.AuthenticateAsync()).Succeeded);
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("127.0.0.2", false)]
    [InlineData("::ffff:127.0.0.1", true)]
    public async Task BackendIp_NormalizesAndChecksTheSource(string remoteIp, bool expected)
    {
        var requirement = new BackendIpRequirement();
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "backend")],
            SecuritySchemes.Backend));
        var authorizationContext = new AuthorizationHandlerContext(
            [requirement],
            principal,
            httpContext);
        var handler = new BackendIpAuthorizationHandler(Options.Create(
            new BackendSecurityOptions { ApiKey = "backend-secret", AllowedIp = "127.0.0.1" }));

        await handler.HandleAsync(authorizationContext);

        Assert.Equal(expected, authorizationContext.HasSucceeded);
    }

    [Fact]
    public async Task Push_OnlyAuthenticatesTheIpOfTheRoutesResidential()
    {
        var reloj = new Reloj
        {
            IdReloj = "CLOCK-1",
            ResidentialId = "RES-1",
            DeviceSn = "SN-1"
        };
        var residential = new Residential { IdResidential = "RES-1", IpActual = "203.0.113.8" };

        var allowedContext = PushHttpContext("203.0.113.8");
        var allowed = PushHandler(reloj, residential);
        await Initialize(allowed, SecuritySchemes.ResidentialPush, allowedContext);

        var rejectedContext = PushHttpContext("203.0.113.9");
        var rejected = PushHandler(reloj, residential);
        await Initialize(rejected, SecuritySchemes.ResidentialPush, rejectedContext);

        Assert.True((await allowed.AuthenticateAsync()).Succeeded);
        Assert.IsType<PushAuthContext>(allowedContext.Items[PushAuthContext.HttpContextItemKey]);
        Assert.False((await rejected.AuthenticateAsync()).Succeeded);
    }

    [Fact]
    public void DeviceDto_NeverSerializesTheHmacSecret()
    {
        var json = JsonSerializer.Serialize(new DeviceDto
        {
            _deviceId = "DEVICE-1",
            _residentialId = "RES-1",
            _secretKey = "must-not-leak"
        });

        Assert.DoesNotContain("must-not-leak", json);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProxyConfiguration_TrustsOnlyConfiguredNetworks()
    {
        var source = new ProxySecurityOptions
        {
            Enabled = true,
            ForwardLimit = 1,
            KnownNetworks = ["10.0.1.0/24"]
        };
        var target = new ForwardedHeadersOptions();

        Assert.True(source.IsValid());
        source.ApplyTo(target);

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, target.ForwardedHeaders);
        Assert.Equal(1, target.ForwardLimit);
        Assert.Empty(target.KnownProxies);
        var network = Assert.Single(target.KnownIPNetworks);
        Assert.True(network.Contains(IPAddress.Parse("10.0.1.20")));
        Assert.False(network.Contains(IPAddress.Parse("10.0.2.20")));
    }

    [Theory]
    [InlineData("not-an-ip", "10.0.1.0/24")]
    [InlineData("10.0.1.10", "not-a-network")]
    public void ProxyConfiguration_RejectsInvalidTrustedSources(string proxy, string network)
    {
        var options = new ProxySecurityOptions
        {
            Enabled = true,
            KnownProxies = [proxy],
            KnownNetworks = [network]
        };

        Assert.False(options.IsValid());
    }

    private static HeartbeatAuthenticationHandler HeartbeatHandler(string secret)
    {
        var device = new Device
        {
            DeviceId = "DEVICE-1",
            ResidentialId = "RES-1",
            SecretKey = secret
        };
        var residential = new Residential { IdResidential = "RES-1", IpActual = "127.0.0.2" };
        return new HeartbeatAuthenticationHandler(
            new StaticOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            Options.Create(new HeartbeatSecurityOptions
            {
                AllowedClockSkewSeconds = 300,
                MaximumBodySizeBytes = 8192
            }),
            new FakeDevicesRepository(device),
            new FakeResidentialsRepository(residential),
            new FixedTimeProvider(Now));
    }

    private static BackendApiKeyAuthenticationHandler BackendHandler()
    {
        return new BackendApiKeyAuthenticationHandler(
            new StaticOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            Options.Create(new BackendSecurityOptions
            {
                ApiKey = "backend-secret",
                AllowedIp = "127.0.0.1"
            }));
    }

    private static ResidentialPushAuthenticationHandler PushHandler(
        Reloj reloj,
        Residential residential)
    {
        return new ResidentialPushAuthenticationHandler(
            new StaticOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            new FakeRelojesRepository(reloj),
            new FakeResidentialsRepository(residential));
    }

    private static DefaultHttpContext HeartbeatHttpContext(string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = bytes.Length;
        context.Request.Body = new MemoryStream(bytes);
        return context;
    }

    private static DefaultHttpContext PushHttpContext(string ip)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        context.Request.RouteValues["relojId"] = "CLOCK-1";
        return context;
    }

    private static string HeartbeatBody(string secret, long timestamp, string? signature = null)
    {
        var canonical = $"{timestamp}|DEVICE-1|RES-1";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        signature ??= Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
        return JsonSerializer.Serialize(new HeartBeatDto
        {
            DeviceId = "DEVICE-1",
            ResidentialId = "RES-1",
            TimeStamp = timestamp,
            Signature = signature
        });
    }

    private static Task Initialize(
        IAuthenticationHandler handler,
        string schemeName,
        HttpContext context)
    {
        return handler.InitializeAsync(
            new AuthenticationScheme(schemeName, schemeName, handler.GetType()),
            context);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class FakeDevicesRepository(Device device) : IDevicesRepository
    {
        public Device Add(Device value) => throw new NotSupportedException();
        public Device? GetById(string id) => id == device.DeviceId ? device : null;
        public List<Device> GetAll() => [device];
        public List<Device> GetByResidentialId(string residentialId) => [device];
        public bool TryAcceptHeartbeat(string deviceId, string residentialId, long timestamp, DateTime lastSeenUtc) => throw new NotSupportedException();
        public void update(Device value) => throw new NotSupportedException();
        public void delete(string id) => throw new NotSupportedException();
    }

    private sealed class FakeResidentialsRepository(Residential residential) : IResidentialsRepository
    {
        public Residential Add(Residential value) => throw new NotSupportedException();
        public Residential? GetById(string id) => id == residential.IdResidential ? residential : null;
        public List<Residential> GetAll() => [residential];
        public bool TryUpdateIp(string residentialId, string ipNueva) => throw new NotSupportedException();
        public void update(Residential value) => throw new NotSupportedException();
        public void delete(string id) => throw new NotSupportedException();
    }

    private sealed class FakeRelojesRepository(Reloj reloj) : IRelojesRepository
    {
        public Reloj Add(Reloj value) => throw new NotSupportedException();
        public Reloj? GetById(string id) => id == reloj.IdReloj ? reloj : null;
        public List<Reloj> GetAll() => [reloj];
        public List<Reloj> GetPollCandidates(string? residentialId = null, string? relojId = null) => [reloj];
        public void update(Reloj value) => throw new NotSupportedException();
        public void delete(string id) => throw new NotSupportedException();
    }
}
