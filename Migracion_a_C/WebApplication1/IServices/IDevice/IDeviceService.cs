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
    void Eliminar(int id);
    List<DeviceDto> Listar();
    DeviceDto GetById(int id);
    void HeartbeatProcesado(DeviceDto dto);
}
