using Dominio;
using Models.Dominio;

namespace IServices.IDevice;

public interface IDeviceMantenimientoService
{
    void Crear(DeviceDto device);
    void Modificar(Device device);
    void Eliminar(int id);
}
