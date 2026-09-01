using Dominio;

namespace IDataAcces;

public interface IResidentialsRepository
{
    Residential Add(Residential residential);
    Residential? GetById(string id);
    List<Residential> GetAll();
    bool TryUpdateIp(string residentialId, string ipNueva);
    void update(Residential residential);
    void delete(string id);
}
