using Dominio;
using Models.Dominio;
using Models.WebApi;

namespace IServices.IJornada;

public interface IJornadaMantenimientoService
{
    void ProcesarEventoInsertado(AccessEvents accessEvent);
    int MarcarIncompletasVencidasComoError(DateTimeOffset nowUtc);
    List<JornadaDto> Buscar(JornadasQueryDto query);
}
