using IServices.IResidentials;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Models.Dominio;
using Models.WebApi;
using WebApplication1.Security;

namespace WebApplication1.Controllers;
[ApiController]
[Route("[controller]")]
public class ResidentialController(IResidentialService service) : ControllerBase
{
    private readonly IResidentialService _service = service;

    [HttpGet]
    public ActionResult<List<ResidentialDto>> Listar()
    {
        return  Ok(_service.Listar());
    }

    [HttpGet("{id}")]
    public ActionResult<ResidentialDto> BuscarPorId([FromRoute] string id)
    {
        return Ok(_service.GetById(id));
    }

    [HttpPost]
    public ActionResult<ResidentialDto> Crear([FromBody] CrearResidentialRequest residential)
    {
        _service.Crear(residential);
        return _service.GetById(residential.IdResidential);
    }

    [HttpPost("heartbeat")]
    [Authorize(Policy = SecurityPolicies.Heartbeat)]
    [EnableRateLimiting(RateLimitingPolicies.Heartbeat)]
    public IActionResult HeartBeat([FromBody] HeartBeatDto heartBeat)
    {
        if (!HttpContext.Items.TryGetValue(HeartbeatAuthContext.HttpContextItemKey, out var raw)
            || raw is not HeartbeatAuthContext authContext)
        {
            throw new InvalidOperationException("No se pudo resolver el contexto autenticado del heartbeat");
        }

        var ip = BackendIpAuthorizationHandler.Normalize(HttpContext.Connection.RemoteIpAddress)?.ToString()
                 ?? throw new InvalidOperationException("No se pudo determinar la IP del heartbeat");
        _service.ProcesarHeartBeat(authContext, ip);
        return NoContent();
    }
}
