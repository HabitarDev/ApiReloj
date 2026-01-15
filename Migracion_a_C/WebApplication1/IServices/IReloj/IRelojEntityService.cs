using Dominio;
using Models.Dominio;

namespace IServices.IReloj;

public interface IRelojEntityService
{
    Reloj ToEntity(RelojDto dto);
    RelojDto FromEntity(Reloj atraccion);
}