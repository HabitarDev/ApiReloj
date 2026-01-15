using Models.Dominio;

namespace IServices.IResidentials;

public interface IResidentialValidationService
{
    void Validar (ResidentialDto dto);
}