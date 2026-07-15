using Dominio;
using Models.Dominio;
using Models.WebApi;

namespace IServices.IResidentials;

public interface IResidentialService
{
    Residential ToEntity(ResidentialDto dto);
    ResidentialDto FromEntity(Residential atraccion);
    void Validar (ResidentialDto dto);
    void Crear(CrearResidentialRequest reloj);
    void Modificar(Residential reloj);
    void Eliminar(string id);
    List<ResidentialDto> Listar();
    ResidentialDto GetById(string id);
    bool ProcesarHeartBeat(HeartbeatAuthContext authContext, string ipNueva);
}
