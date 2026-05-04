using Dominio;
using Models.Dominio;

namespace IServices.IDevice;

public interface IDeviceService
{
    Device ToEntity(DeviceDto dto);
    DeviceDto FromEntity(Device device);
    void Validar(DeviceDto dto);
    void Crear(DeviceDto device);
    void Modificar(Device device);
    void Eliminar(string id);
    List<DeviceDto> Listar();
    DeviceDto GetById(string id);
    void HeartbeatProcesado(DeviceDto dto);
}
