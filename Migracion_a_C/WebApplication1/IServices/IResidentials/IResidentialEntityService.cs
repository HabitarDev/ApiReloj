using Dominio;
using Models.Dominio;

namespace IServices.IResidentials;

public interface IResidentialEntityService
{
    Residential ToEntity(ResidentialDto dto);
    ResidentialDto FromEntity(Residential atraccion);
}