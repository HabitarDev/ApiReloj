using Models.Dominio;

namespace IServices.IDevice;

public interface IDeviceValidationService
{
    void Validar(DeviceDto dto);
}
