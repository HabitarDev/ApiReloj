using DataAcces.Context;
using Dominio;
using IDataAcces;

namespace DataAcces.Repositories;

public class DevicesRepository(SqlContext repos) : IDevicesRepository
{
    private readonly SqlContext _context = repos;

    public Device Add(Device device)
    {
        _context.Devices.Add(device);
        _context.SaveChanges();
        return device;
    }

    public Device? GetById(string id)
    {
        return _context.Devices.FirstOrDefault(x => x.DeviceId == id);
    }

    public List<Device> GetAll()
    {
        return _context.Devices.ToList();
    }

    public List<Device> GetByResidentialId(string residentialId)
    {
        return _context.Devices.Where(x => x.ResidentialId == residentialId).ToList();
    }

    public void update(Device device)
    {
        var exists = _context.Devices.Any(x => x.DeviceId == device.DeviceId);
        if (!exists)
        {
            throw new InvalidOperationException("Dispositivo inexistente");
        }

        _context.Devices.Update(device);
        _context.SaveChanges();
    }

    public void delete(string id)
    {
        var device = GetById(id);
        if (device == null)
        {
            throw new InvalidOperationException("Dispositivo inexistente");
        }

        _context.Devices.Remove(device);
        _context.SaveChanges();
    }
}
