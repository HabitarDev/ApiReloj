using IServices.IDevice;
using Microsoft.AspNetCore.Mvc;
using Models.Dominio;

namespace WebApplication1.Controllers;
[ApiController]
[Route("[controller]")]
public class DeviceController(IDeviceService deviceService) : ControllerBase
{
    private readonly IDeviceService _service = deviceService;

    [HttpGet]
    public ActionResult<List<DeviceDto>> Listar()
    {
        return _service.Listar();
    }

    [HttpGet("{id}")]
    public ActionResult<DeviceDto> ListarPorId(int id)
    {
        return _service.GetById(id);
    }

    [HttpPost]
    public ActionResult<DeviceDto> Crear([FromBody] DeviceDto deviceDto)
    {
        _service.Crear(deviceDto);
        return _service.GetById(deviceDto._deviceId);
    }
}