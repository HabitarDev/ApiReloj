using Dominio;

namespace IDataAcces;

public interface IDevicesRepository
{
    Device Add(Device device);
    Device? GetById(int id);
    List<Device> GetAll();
    List<Device> GetByResidentialId(int residentialId);
    void update(Device device);
    void delete(int id);
}
