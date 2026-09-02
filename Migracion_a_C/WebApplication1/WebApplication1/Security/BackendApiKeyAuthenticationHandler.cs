using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace WebApplication1.Security;

public sealed class BackendApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder,
    IOptions<BackendSecurityOptions> securityOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
    public const string ApiKeyHeader = "X-Api-Key";
    private readonly BackendSecurityOptions _securityOptions = securityOptions.Value;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeader, out var values)
            || values.Count != 1
            || string.IsNullOrWhiteSpace(values[0]))
        {
            return Task.FromResult(AuthenticateResult.Fail("Credencial de backend invalida"));
        }

        if (!SecureEquals(values[0]!, _securityOptions.ApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Credencial de backend invalida"));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "backend"),
            new Claim(ClaimTypes.Name, "backend")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(principal, Scheme.Name)));
    }

    private static bool SecureEquals(string received, string expected)
    {
        var receivedHash = SHA256.HashData(Encoding.UTF8.GetBytes(received));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(receivedHash, expectedHash);
    }
}

public sealed class BackendIpRequirement : Microsoft.AspNetCore.Authorization.IAuthorizationRequirement;

public sealed class BackendIpAuthorizationHandler(IOptions<BackendSecurityOptions> options)
    : Microsoft.AspNetCore.Authorization.AuthorizationHandler<BackendIpRequirement>
{
    private readonly IPAddress _allowedIp = Parse(options.Value.AllowedIp);

    protected override Task HandleRequirementAsync(
        Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext context,
        BackendIpRequirement requirement)
    {
        if (context.Resource is HttpContext httpContext)
        {
            var remoteIp = Normalize(httpContext.Connection.RemoteIpAddress);
            if (remoteIp != null && remoteIp.Equals(_allowedIp))
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }

    private static IPAddress Parse(string value)
    {
        return Normalize(IPAddress.Parse(value))!;
    }

    internal static IPAddress? Normalize(IPAddress? ip)
    {
        return ip?.IsIPv4MappedToIPv6 == true ? ip.MapToIPv4() : ip;
    }
}
