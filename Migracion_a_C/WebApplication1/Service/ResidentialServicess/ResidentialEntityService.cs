using Dominio;
using IDataAcces;
using IServices.IDevice;
using IServices.IReloj;
using IServices.IResidentials;
using Models.Dominio;

namespace Service.ResidentialServicess;

public class ResidentialEntityService (IResidentialsRepository repoResidencials, IRelojService relojService, IDeviceService deviceService) : IResidentialEntityService
{
    public IResidentialsRepository _dbResidentials = repoResidencials;
    public IRelojService _relojService = relojService;
    public IDeviceService _deviceService = deviceService;

    public Residential ToEntity(ResidentialDto dto)
    {
        Residential? paraRetornar = _dbResidentials.GetById(dto._idResidential);
        if (paraRetornar == null)
        {
            paraRetornar = new Residential();
            paraRetornar.IdResidential = dto._idResidential;
            paraRetornar.IpActual = dto._ipActual;
            foreach (var relojDto in dto._relojes)
            {
                paraRetornar.Relojes.Add(_relojService.ToEntity(relojDto));
            }
            foreach (var deviceDto in dto._devices)
            {
                paraRetornar.Devices.Add(_deviceService.ToEntity(deviceDto));
            }
        }
        return paraRetornar;
    }

    public ResidentialDto FromEntity(Residential residentialRecibido)
    {
        ResidentialDto paraRetornar = new ResidentialDto();
        paraRetornar._idResidential = residentialRecibido.IdResidential;
        paraRetornar._ipActual = residentialRecibido.IpActual;
        paraRetornar._relojes = new List<RelojDto>();
        foreach (var reloj in residentialRecibido.Relojes)
        {
            paraRetornar._relojes.Add(_relojService.FromEntity(reloj));
        }
        paraRetornar._devices = new List<DeviceDto>();
        foreach (var device in residentialRecibido.Devices)
        {
            paraRetornar._devices.Add(_deviceService.FromEntity(device));
        }
        return paraRetornar;
    }
}
