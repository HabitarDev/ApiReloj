using System.ComponentModel.Design;
using System.Security.Cryptography;
using System.Text;
using Dominio;
using IDataAcces;
using IServices.IDevice;
using IServices.IResidentials;
using Models.Dominio;
using Models.WebApi;

namespace Service.ResidentialServicess;

public class ResidentialService(IResidentialsRepository repo, IResidentialEntityService entityService, 
    IResidentialValidationService validacionService, IResidentialMantenimientoService mantenimientoService
    , IDeviceService deviceService) : IResidentialService
{
    private IResidentialsRepository db = repo;
    private IResidentialEntityService entity = entityService;
    private IResidentialValidationService validacion = validacionService;
    private IResidentialMantenimientoService mantenimiento = mantenimientoService;
    private IDeviceService device = deviceService;
    
    public Residential ToEntity(ResidentialDto dto)
    {
        Validar(dto);
        return entity.ToEntity(dto);
    }

    public ResidentialDto FromEntity(Residential residential)
    {
        ResidentialDto dto = entity.FromEntity(residential);
        Validar(dto);
        return dto;
    }

    public void Validar(ResidentialDto dto)
    {
        validacion.Validar(dto);
        
    }

    public void Crear(CrearResidentialRequest dto)
    {
        ResidentialDto adaptado = new ResidentialDto();
        adaptado._idResidential = dto.IdResidential;
        adaptado._ipActual = dto.IpActual;
        validacion.Validar(adaptado);
        mantenimiento.Crear(adaptado);
    }

    public void Modificar(Residential res)
    {
        mantenimiento.Modificar(res);
    }

    public void Eliminar(int id)
    {
        mantenimiento.Eliminar(id);
    }

    public List<ResidentialDto> Listar()
    {
        List<ResidentialDto> listaADevolver = new List<ResidentialDto>();
        foreach (var res in db.GetAll())
        {
         listaADevolver.Add(FromEntity(res));   
        }
        return listaADevolver;
    }

    public ResidentialDto GetById(int id)
    {
        Residential? residential = db.GetById(id);
        if (residential == null)
        {
            throw new Exception("No se encontro el residential");
        }
        return FromEntity(residential);
    }

    public void ProcesarHeartBeat(HeartBeatDto dto, string ipNueva)
    {
        ResidentialDto residential = GetById(dto.ResidentialId);
        DeviceDto buscado = EsMio(dto.DeviceId, residential);
        if (SignatureAprobada(dto.Signature, buscado, dto.TimeStamp))
        {
            DateTime timeStampEnDateTime = DateTimeOffset.FromUnixTimeSeconds(dto.TimeStamp).UtcDateTime;
            Residential resiFinal = entity.ToEntity(residential);
            resiFinal.IpActual = ipNueva;
            Modificar(resiFinal);
            buscado._lastSeen = timeStampEnDateTime;
            device.HeartbeatProcesado(buscado);
        }
    }

    private DeviceDto EsMio(int deviceId, ResidentialDto residential)
    {
        DeviceDto buscado = device.GetById(deviceId);
        if (buscado._residentialId != residential._idResidential)
        {
            throw new Exception("El device no pertenece al residencial");
        }
        return buscado;
    }
    
    private bool SignatureAprobada(string signature, DeviceDto deviceBuscado, long timeStamp)
    {
        string claveJunta = $"{timeStamp}|{deviceBuscado._deviceId}|{deviceBuscado._residentialId}";
        var keyBytes = Encoding.UTF8.GetBytes(deviceBuscado._secretKey);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(claveJunta));
        byte[] sigBytes;
        try
        {
            sigBytes = Convert.FromHexString(signature);
        }
        catch
        {
            return false;
        }
        return sigBytes.Length == hash.Length
               && CryptographicOperations.FixedTimeEquals(hash, sigBytes);
    }
}
