using Dominio;
using IDataAcces;
using IServices.IDevice;
using Models.Dominio;

namespace Service.DeviceServicess;

public class DeviceMantenimientoService(IDeviceEntityService deviceEntityService, IDevicesRepository devicesRepository, IResidentialsRepository residentialRepo) : IDeviceMantenimientoService
{
    private IDeviceEntityService _deviceEntityService = deviceEntityService;
    private IDevicesRepository _devicesRepository = devicesRepository;
    private IResidentialsRepository _residentialRepo = residentialRepo;

    public void Crear(DeviceDto device)
    {
        Device? deviceBuscado = _devicesRepository.GetById(device._deviceId);
        Residential? resiBuscado = _residentialRepo.GetById(device._residentialId);
        if (resiBuscado == null) throw new Exception("El Residential no existe");
        if (deviceBuscado != null) throw new Exception("El Device ya existe");
        deviceBuscado = _deviceEntityService.ToEntity(device);
        _devicesRepository.Add(deviceBuscado);
    }

    public void Modificar(Device device)
    {
        Device? deviceBuscado = _devicesRepository.GetById(device.DeviceId);
        if (deviceBuscado == null) throw new Exception("El Device no existe");
        _devicesRepository.update(deviceBuscado);
    }

    public void Eliminar(int id)
    {
        Device? deviceBuscado = _devicesRepository.GetById(id);
        if (deviceBuscado == null) throw new Exception("El Device no existe");
        _devicesRepository.delete(id);
    }
}
