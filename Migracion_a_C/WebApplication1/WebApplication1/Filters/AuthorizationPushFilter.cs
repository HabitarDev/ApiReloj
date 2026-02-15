using IDataAcces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Models.WebApi;

namespace WebApplication1.Filters;

public class AuthorizationPushFilter(
    IRelojesRepository relojesRepo,
    IResidentialsRepository residentialsRepo) : IAuthorizationFilter
{
    private readonly IRelojesRepository _relojesRepo = relojesRepo;
    private readonly IResidentialsRepository _residentialsRepo = residentialsRepo;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!TryGetRelojId(context, out var relojId))
        {
            context.Result = BadRequest("relojId invalido");
            return;
        }

        var reloj = _relojesRepo.GetById(relojId);
        if (reloj == null)
        {
            context.Result = NotFound("No se encontro el reloj");
            return;
        }

        var residential = _residentialsRepo.GetById(reloj.ResidentialId);
        if (residential == null)
        {
            context.Result = NotFound("No se encontro el residential del reloj");
            return;
        }

        var remoteIp = context.HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString();
        if (string.IsNullOrWhiteSpace(remoteIp))
        {
            context.Result = Unauthorized("No se pudo determinar la IP de origen");
            return;
        }

        if (!string.Equals(remoteIp, residential.IpActual, StringComparison.OrdinalIgnoreCase))
        {
            context.Result = Unauthorized("IP origen no autorizada para este reloj");
            return;
        }

        if (string.IsNullOrWhiteSpace(reloj.DeviceSn))
        {
            context.Result = Unprocessable("El reloj no tiene DeviceSn configurado");
            return;
        }

        context.HttpContext.Items[PushAuthContext.HttpContextItemKey] = new PushAuthContext
        {
            RelojId = reloj.IdReloj,
            ResidentialId = residential.IdResidential,
            DeviceSn = reloj.DeviceSn,
            RemoteIp = remoteIp
        };
    }

    private static bool TryGetRelojId(AuthorizationFilterContext context, out int relojId)
    {
        relojId = 0;
        if (!context.RouteData.Values.TryGetValue("relojId", out var raw))
        {
            return false;
        }

        return int.TryParse(raw?.ToString(), out relojId) && relojId > 0;
    }

    private static BadRequestObjectResult BadRequest(string detail)
    {
        return new BadRequestObjectResult(new ProblemDetails
        {
            Status = 400,
            Title = "Argumento inválido",
            Detail = detail
        });
    }

    private static NotFoundObjectResult NotFound(string detail)
    {
        return new NotFoundObjectResult(new ProblemDetails
        {
            Status = 404,
            Title = "No encontrado",
            Detail = detail
        });
    }

    private static UnauthorizedObjectResult Unauthorized(string detail)
    {
        return new UnauthorizedObjectResult(new ProblemDetails
        {
            Status = 401,
            Title = "No autorizado",
            Detail = detail
        });
    }

    private static ObjectResult Unprocessable(string detail)
    {
        return new ObjectResult(new ProblemDetails
        {
            Status = 422,
            Title = "Regla de negocio",
            Detail = detail
        })
        {
            StatusCode = 422
        };
    }
}
