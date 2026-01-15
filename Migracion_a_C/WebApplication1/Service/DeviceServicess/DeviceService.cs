using Dominio;
using IDataAcces;
using IServices.IDevice;
using Models.Dominio;

namespace Service.DeviceServicess;

public class DeviceService(IDeviceMantenimientoService mantenimientoService, IDevicesRepository repo, IDeviceEntityService entityService, IDeviceValidationService validacionService) : IDeviceService
{
    private IDevicesRepository db = repo;
    private IDeviceEntityService entity = entityService;
    private IDeviceValidationService validacion = validacionService;
    private IDeviceMantenimientoService mantenimiento = mantenimientoService;

    public Device ToEntity(DeviceDto dto)
    {
        Validar(dto);
        return entity.ToEntity(dto);
    }

    public DeviceDto FromEntity(Device device)
    {
        DeviceDto dto = entity.FromEntity(device);
        Validar(dto);
        return dto;
    }

    public void Validar(DeviceDto dto)
    {
        validacion.Validar(dto);
    }

    public void Crear(DeviceDto device)
    {
        validacion.Validar(device);
        mantenimiento.Crear(device);
    }

    public void Modificar(Device device)
    {
        mantenimiento.Modificar(device);
    }

    public void Eliminar(int id)
    {
        mantenimiento.Eliminar(id);
    }
    
    public List<DeviceDto> Listar()
    {
        List<DeviceDto> listaADevolver = new List<DeviceDto>();
        foreach (var res in db.GetAll())
        {
            listaADevolver.Add(FromEntity(res));   
        }
        return listaADevolver;
    }

    public DeviceDto GetById(int id)
    {
        Device? device = db.GetById(id);
        if (device == null)
        {
            throw new Exception("No se encontro el device");
        }
        return FromEntity(device);
    }

    public void HeartbeatProcesado(DeviceDto dto)
    {
        Modificar(ToEntity(dto));
    }
}
