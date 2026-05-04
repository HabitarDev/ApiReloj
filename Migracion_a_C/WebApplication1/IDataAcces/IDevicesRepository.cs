using Dominio;

namespace IDataAcces;

public interface IDevicesRepository
{
    Device Add(Device device);
    Device? GetById(string id);
    List<Device> GetAll();
    List<Device> GetByResidentialId(string residentialId);
    void update(Device device);
    void delete(string id);
}
