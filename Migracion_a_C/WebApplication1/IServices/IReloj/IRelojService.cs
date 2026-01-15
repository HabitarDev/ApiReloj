using Dominio;
using Models.Dominio;
using Models.WebApi;

namespace IServices.IReloj;

public interface IRelojService
{
    Reloj ToEntity(RelojDto dto);
    RelojDto FromEntity(Reloj atraccion);
    void Validar (RelojDto dto);
    void Crear(CrearRelojRequest reloj);
    void Modificar(Reloj reloj);
    void Eliminar(int id);
    List<RelojDto> Listar();
    RelojDto GetById(int id);
}