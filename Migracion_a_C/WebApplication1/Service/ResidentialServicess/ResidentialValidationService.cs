using IServices.IResidentials;
using Models.Dominio;

namespace Service.ResidentialServicess;

public class ResidentialValidationService : IResidentialValidationService
{
    public void Validar(ResidentialDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto._idResidential))
        {
            throw new ArgumentException("Id de residencial invalido");
        }
    }
}