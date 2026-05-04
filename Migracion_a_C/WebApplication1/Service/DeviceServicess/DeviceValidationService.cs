using IServices.IDevice;
using Models.Dominio;

namespace Service.DeviceServicess;

public class DeviceValidationService : IDeviceValidationService
{
    public void Validar(DeviceDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto._deviceId))
        {
            throw new ArgumentException("Id de dispositivo invalido");
        }
        if (string.IsNullOrWhiteSpace(dto._residentialId))
        {
            throw new ArgumentException("Id de residencial invalido");
        }
        if (string.IsNullOrWhiteSpace(dto._secretKey))
        {
            throw new ArgumentException("SecretKey invalida");
        }
    }
}
