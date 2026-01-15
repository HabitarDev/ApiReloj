using Dominio;
using Models.Dominio;

namespace IServices.IDevice;

public interface IDeviceEntityService
{
    Device ToEntity(DeviceDto dto);
    DeviceDto FromEntity(Device device);
}
