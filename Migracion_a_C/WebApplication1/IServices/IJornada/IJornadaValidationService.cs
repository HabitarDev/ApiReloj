using Models.WebApi;

namespace IServices.IJornada;

public interface IJornadaValidationService
{
    void ValidarBusqueda(JornadasQueryDto query);
    void ValidarStatus(string? statusCheck, string? statusBreak);
}
