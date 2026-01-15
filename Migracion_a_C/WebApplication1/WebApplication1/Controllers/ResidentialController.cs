using IServices.IResidentials;
using Microsoft.AspNetCore.Mvc;
using Models.Dominio;
using Models.WebApi;

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

    [HttpGet]
    public ActionResult<ResidentialDto> BuscarPorId([FromRoute] int id)
    {
        return Ok(_service.GetById(id));
    }

    [HttpPost]
    public ActionResult<ResidentialDto> Crear([FromBody] CrearResidentialRequest residential)
    {
        _service.Crear(residential);
        return _service.GetById(residential.IdResidential);
    }

    [HttpPut]
    public void HeartBeat([FromBody] HeartBeatDto heartBeat)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString();
        _service.ProcesarHeartBeat(heartBeat, ip);
    }
}