using IServices.IReloj;
using Models.Dominio;

namespace Service.RelojServicess;

public class RelojValidationService : IRelojValidacionService
{
    public void Validar(RelojDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto._residentialId))
        {
            throw new ArgumentException("Id de residencial invalido");
        }
        if (string.IsNullOrWhiteSpace(dto._idReloj))
        {
            throw new ArgumentException("Id de reloj invalido");
        }

        if (dto._puerto <= 0)
        {
            throw new ArgumentException("Puerto de reloj invalido");
        }
    }
}