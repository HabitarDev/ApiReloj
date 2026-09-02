using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IDataAcces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Models.WebApi;

namespace WebApplication1.Security;

public sealed class HeartbeatAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder,
    IOptions<HeartbeatSecurityOptions> securityOptions,
    IDevicesRepository devicesRepository,
    IResidentialsRepository residentialsRepository,
    TimeProvider timeProvider)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HeartbeatSecurityOptions _securityOptions = securityOptions.Value;
    private readonly IDevicesRepository _devicesRepository = devicesRepository;
    private readonly IResidentialsRepository _residentialsRepository = residentialsRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!HttpMethods.IsPost(Request.Method)
            || !Request.HasJsonContentType()
            || Context.Connection.RemoteIpAddress == null
            || Request.ContentLength > _securityOptions.MaximumBodySizeBytes)
        {
            return Invalid();
        }

        HeartBeatDto? heartbeat;
        Request.EnableBuffering(
            bufferThreshold: Math.Min(_securityOptions.MaximumBodySizeBytes, 4096),
            bufferLimit: _securityOptions.MaximumBodySizeBytes);

        try
        {
            heartbeat = await JsonSerializer.DeserializeAsync<HeartBeatDto>(
                Request.Body,
                JsonOptions,
                Context.RequestAborted);
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
        {
            return Invalid();
        }
        finally
        {
            Request.Body.Position = 0;
        }

        if (heartbeat == null
            || string.IsNullOrWhiteSpace(heartbeat.DeviceId)
            || string.IsNullOrWhiteSpace(heartbeat.ResidentialId)
            || string.IsNullOrWhiteSpace(heartbeat.Signature))
        {
            return Invalid();
        }

        DateTimeOffset timestampUtc;
        try
        {
            timestampUtc = DateTimeOffset.FromUnixTimeSeconds(heartbeat.TimeStamp);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Invalid();
        }

        var now = _timeProvider.GetUtcNow();
        var skew = TimeSpan.FromSeconds(_securityOptions.AllowedClockSkewSeconds);
        if (timestampUtc < now - skew || timestampUtc > now + skew)
        {
            return Invalid();
        }

        var device = _devicesRepository.GetById(heartbeat.DeviceId);
        if (device == null
            || !string.Equals(
                device.ResidentialId,
                heartbeat.ResidentialId,
                StringComparison.Ordinal)
            || _residentialsRepository.GetById(heartbeat.ResidentialId) == null)
        {
            return Invalid();
        }

        if (!ValidSignature(heartbeat, device.SecretKey))
        {
            return Invalid();
        }

        var authContext = new HeartbeatAuthContext
        {
            DeviceId = heartbeat.DeviceId,
            ResidentialId = heartbeat.ResidentialId,
            Timestamp = heartbeat.TimeStamp,
            TimestampUtc = timestampUtc
        };
        Context.Items[HeartbeatAuthContext.HttpContextItemKey] = authContext;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, heartbeat.DeviceId),
            new Claim("residential_id", heartbeat.ResidentialId),
            new Claim("heartbeat_timestamp", heartbeat.TimeStamp.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    private static bool ValidSignature(HeartBeatDto heartbeat, string secretKey)
    {
        byte[] received;
        try
        {
            received = Convert.FromHexString(heartbeat.Signature);
        }
        catch (FormatException)
        {
            return false;
        }

        var canonical = $"{heartbeat.TimeStamp}|{heartbeat.DeviceId}|{heartbeat.ResidentialId}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return received.Length == expected.Length
               && CryptographicOperations.FixedTimeEquals(expected, received);
    }

    private static AuthenticateResult Invalid()
    {
        return AuthenticateResult.Fail("Heartbeat invalido");
    }
}
