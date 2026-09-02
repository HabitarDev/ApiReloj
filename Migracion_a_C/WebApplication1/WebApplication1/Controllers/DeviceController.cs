using IServices.IDevice;
using Microsoft.AspNetCore.Mvc;
using Models.Dominio;
using Models.WebApi;

namespace WebApplication1.Controllers;
[ApiController]
[Route("[controller]")]
public class DeviceController(IDeviceService deviceService) : ControllerBase
{
    private readonly IDeviceService _service = deviceService;

    [HttpGet]
    public ActionResult<List<DeviceResponseDto>> Listar()
    {
        return _service.Listar().Select(ToResponse).ToList();
    }

    [HttpGet("{id}")]
    public ActionResult<DeviceResponseDto> ListarPorId(string id)
    {
        return ToResponse(_service.GetById(id));
    }

    [HttpPost]
    public ActionResult<DeviceResponseDto> Crear([FromBody] CreateDeviceRequest request)
    {
        var deviceDto = new DeviceDto
        {
            _deviceId = request._deviceId,
            _secretKey = request._secretKey,
            _lastSeen = request._lastSeen,
            _residentialId = request._residentialId
        };
        _service.Crear(deviceDto);
        return ToResponse(_service.GetById(deviceDto._deviceId));
    }

    private static DeviceResponseDto ToResponse(DeviceDto device)
    {
        return new DeviceResponseDto
        {
            _deviceId = device._deviceId,
            _lastSeen = device._lastSeen,
            _residentialId = device._residentialId
        };
    }
}
