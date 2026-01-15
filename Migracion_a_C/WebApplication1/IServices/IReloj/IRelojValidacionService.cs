using Models.Dominio;

namespace IServices.IReloj;

public interface IRelojValidacionService
{
    void Validar (RelojDto dto);
}