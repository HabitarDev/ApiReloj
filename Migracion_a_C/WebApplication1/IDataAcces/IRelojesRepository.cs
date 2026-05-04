using Dominio;

namespace IDataAcces;

public interface IRelojesRepository
{
    Reloj Add(Reloj reloj);
    Reloj? GetById(string id);
    List<Reloj> GetAll();
    List<Reloj> GetPollCandidates(string? residentialId = null, string? relojId = null);
    void update(Reloj reloj);
    void delete(string id);
}
