using Dominio;
using IDataAcces;
using IServices.IDevice;
using Models.Dominio;

namespace Service.DeviceServicess;

public class DeviceEntityService(IDevicesRepository repo, IResidentialsRepository repoResidencials) : IDeviceEntityService
{
    public IDevicesRepository dbDevices = repo;
    public IResidentialsRepository dbResidentials = repoResidencials;

    public Device ToEntity(DeviceDto dto)
    {
        Device? deviceParaRetornar = dbDevices.GetById(dto._deviceId);
        if (deviceParaRetornar == null)
        {
            Residential? residencialDuenio = dbResidentials.GetById(dto._residentialId);
            if (residencialDuenio != null)
            {
                deviceParaRetornar = new Device();
                deviceParaRetornar.DeviceId = dto._deviceId;
                deviceParaRetornar.SecretKey = dto._secretKey;
                deviceParaRetornar.LastSeen = dto._lastSeen;
                deviceParaRetornar.ResidentialId = dto._residentialId;
                deviceParaRetornar.Residential = residencialDuenio;
            }
            else
            {
                throw new ArgumentException("No se encuentra dispositivo ni residencial");
            }
        }
        return deviceParaRetornar;
    }

    public DeviceDto FromEntity(Device deviceRecibido)
    {
        DeviceDto paraDevolver = new DeviceDto();
        paraDevolver._deviceId = deviceRecibido.DeviceId;
        paraDevolver._secretKey = deviceRecibido.SecretKey;
        paraDevolver._lastSeen = deviceRecibido.LastSeen;
        paraDevolver._residentialId = deviceRecibido.ResidentialId;
        return paraDevolver;
    }
}
