using IServices.IReloj;
using Models.Dominio;

namespace Service.RelojServicess;

public class RelojValidationService : IRelojValidacionService
{
    public void Validar(RelojDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto._residentialId <= 0)
        {
            throw new ArgumentException("Id de residencial invalido");
        }
        if (dto._idReloj <= 0)
        {
            throw new ArgumentException("Id de reloj invalido");
        }

        if (dto._puerto <= 0)
        {
            throw new ArgumentException("Puerto de reloj invalido");
        }
    }
}