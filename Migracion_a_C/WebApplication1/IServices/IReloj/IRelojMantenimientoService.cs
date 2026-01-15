using Dominio;
using Models.Dominio;

namespace IServices.IReloj;

public interface IRelojMantenimientoService
{
    void Crear(RelojDto reloj);
    void Modificar(Reloj reloj);
    void Eliminar(int id);
}