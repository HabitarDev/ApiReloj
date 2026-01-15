using IServices.IResidentials;
using Models.Dominio;

namespace Service.ResidentialServicess;

public class ResidentialValidationService : IResidentialValidationService
{
    public void Validar(ResidentialDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto._idResidential <= 0)
        {
            throw new ArgumentException("Id de residencial invalido");
        }
    }
}