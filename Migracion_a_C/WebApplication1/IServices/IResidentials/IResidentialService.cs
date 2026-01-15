using Dominio;
using Models.Dominio;

namespace IServices.IResidentials;

public interface IResidentialService
{
    Residential ToEntity(ResidentialDto dto);
    ResidentialDto FromEntity(Residential atraccion);
    void Validar (ResidentialDto dto);
}