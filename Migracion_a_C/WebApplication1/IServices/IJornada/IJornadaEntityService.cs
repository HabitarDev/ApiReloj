using Dominio;
using Models.Dominio;

namespace IServices.IJornada;

public interface IJornadaEntityService
{
    JornadaDto FromEntity(Jornada jornada);
}
