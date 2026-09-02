using Dominio;

namespace IDataAcces;

public interface IDevicesRepository
{
    Device Add(Device device);
    Device? GetById(string id);
    List<Device> GetAll();
    List<Device> GetByResidentialId(string residentialId);
    bool TryAcceptHeartbeat(
        string deviceId,
        string residentialId,
        long timestamp,
        DateTime lastSeenUtc);
    void update(Device device);
    void delete(string id);
}
