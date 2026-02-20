using IServices.IJornada;
using Microsoft.AspNetCore.Mvc;
using Models.Dominio;
using Models.WebApi;

namespace WebApplication1.Controllers;

[ApiController]
[Route("[controller]")]
public class JornadasController(IJornadaService jornadaService) : ControllerBase
{
    private readonly IJornadaService _jornadaService = jornadaService;

    [HttpGet]
    public ActionResult<List<JornadaDto>> Get([FromQuery] JornadasQueryDto? query)
    {
        return Ok(_jornadaService.Buscar(query ?? new JornadasQueryDto()));
    }
}
