using System.Security.Claims;
using IDataAcces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Models.WebApi;

namespace WebApplication1.Security;

public sealed class ResidentialPushAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder,
    IRelojesRepository relojesRepository,
    IResidentialsRepository residentialsRepository)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
    private readonly IRelojesRepository _relojesRepository = relojesRepository;
    private readonly IResidentialsRepository _residentialsRepository = residentialsRepository;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var relojId = Context.GetRouteValue("relojId")?.ToString()?.Trim();
        var remoteIpAddress = BackendIpAuthorizationHandler.Normalize(
            Context.Connection.RemoteIpAddress);

        if (string.IsNullOrWhiteSpace(relojId) || remoteIpAddress == null)
        {
            return Task.FromResult(Invalid());
        }

        var reloj = _relojesRepository.GetById(relojId);
        if (reloj == null || string.IsNullOrWhiteSpace(reloj.DeviceSn))
        {
            return Task.FromResult(Invalid());
        }

        var residential = _residentialsRepository.GetById(reloj.ResidentialId);
        if (residential == null
            || string.IsNullOrWhiteSpace(residential.IpActual)
            || !System.Net.IPAddress.TryParse(residential.IpActual, out var configuredIp)
            || !remoteIpAddress.Equals(BackendIpAuthorizationHandler.Normalize(configuredIp)))
        {
            return Task.FromResult(Invalid());
        }

        var remoteIp = remoteIpAddress.ToString();
        Context.Items[PushAuthContext.HttpContextItemKey] = new PushAuthContext
        {
            RelojId = reloj.IdReloj,
            ResidentialId = residential.IdResidential,
            DeviceSn = reloj.DeviceSn,
            RemoteIp = remoteIp
        };

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, reloj.IdReloj),
            new Claim("residential_id", residential.IdResidential),
            new Claim("device_sn", reloj.DeviceSn)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(principal, Scheme.Name)));
    }

    private static AuthenticateResult Invalid()
    {
        return AuthenticateResult.Fail("Push no autorizado");
    }
}
