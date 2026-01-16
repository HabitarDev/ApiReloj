using IServices.IReloj;
using Microsoft.AspNetCore.Mvc;
using Models.Dominio;
using Models.WebApi;

namespace WebApplication1.Controllers;
[ApiController]
[Route("[controller]")]
public class RelojController(IRelojService relojService) : ControllerBase
{
    private readonly IRelojService _relojService = relojService;

    [HttpGet]
    public ActionResult<List<RelojDto>> Listar()
    {
        return _relojService.Listar();
    }

    [HttpGet("{id}")]
    public ActionResult<RelojDto> ListarPorId([FromRoute] int id)
    {
        return _relojService.GetById(id);
    }

    [HttpPost]
    public ActionResult<RelojDto> Crear([FromBody] CrearRelojRequest reloj)
    {
        _relojService.Crear(reloj);
        return _relojService.GetById(reloj._idReloj);
    }
}