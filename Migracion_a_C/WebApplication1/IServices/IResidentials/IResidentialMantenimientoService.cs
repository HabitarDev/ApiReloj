using Dominio;
using Models.Dominio;

namespace IServices.IResidentials;

public interface IResidentialMantenimientoService
{
    void Crear(ResidentialDto reloj);
    void Modificar(Residential reloj);
    void Eliminar(int id);
}