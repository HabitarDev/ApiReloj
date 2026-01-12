using Dominio;

namespace IDataAcces;

public interface IRelojesRepository
{
    Reloj Add(Reloj reloj);
    Reloj? GetById(int id);
    List<Reloj> GetAll();
    void update(Reloj reloj);
    void delete(int id);
}