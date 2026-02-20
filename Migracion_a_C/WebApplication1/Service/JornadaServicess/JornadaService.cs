using Dominio;
using IServices.IJornada;
using Models.Dominio;
using Models.WebApi;

namespace Service.JornadaServicess;

public class JornadaService(
    IJornadaValidationService validationService,
    IJornadaMantenimientoService mantenimientoService) : IJornadaService
{
    private readonly IJornadaValidationService _validation = validationService;
    private readonly IJornadaMantenimientoService _mantenimiento = mantenimientoService;

    public void ProcesarEventoInsertado(AccessEvents accessEvent)
    {
        _mantenimiento.ProcesarEventoInsertado(accessEvent);
    }

    public int MarcarIncompletasVencidasComoError(DateTimeOffset nowUtc)
    {
        return _mantenimiento.MarcarIncompletasVencidasComoError(nowUtc);
    }

    public List<JornadaDto> Buscar(JornadasQueryDto query)
    {
        _validation.ValidarBusqueda(query);
        return _mantenimiento.Buscar(query);
    }
}
