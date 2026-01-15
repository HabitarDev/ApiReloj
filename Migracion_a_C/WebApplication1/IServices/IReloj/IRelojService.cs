using Dominio;
using Models.Dominio;

namespace IServices.IReloj;

public interface IRelojService
{
    Reloj ToEntity(RelojDto dto);
    RelojDto FromEntity(Reloj atraccion);
    void Validar (RelojDto dto);
}