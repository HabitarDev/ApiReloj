using Dominio;

namespace IDataAcces;

public interface IResidentialsRepository
{
    Residential Add(Residential residential);
    Residential? GetById(string id);
    List<Residential> GetAll();
    void update(Residential residential);
    void delete(string id);
}