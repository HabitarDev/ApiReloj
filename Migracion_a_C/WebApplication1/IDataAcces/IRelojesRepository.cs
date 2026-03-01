using Dominio;

namespace IDataAcces;

public interface IRelojesRepository
{
    Reloj Add(Reloj reloj);
    Reloj? GetById(int id);
    List<Reloj> GetAll();
    List<Reloj> GetPollCandidates(int? residentialId = null, int? relojId = null);
    void update(Reloj reloj);
    void delete(int id);
}
