using System.ComponentModel.Design;
using Dominio;
using IDataAcces;
using IServices.IResidentials;
using Models.Dominio;
using Models.WebApi;

namespace Service.ResidentialServicess;

public class ResidentialService(
    IResidentialsRepository repo,
    IResidentialEntityService entityService,
    IResidentialValidationService validacionService,
    IResidentialMantenimientoService mantenimientoService,
    IDevicesRepository devicesRepository,
    IDataTransactionManager transactionManager) : IResidentialService
{
    private IResidentialsRepository db = repo;
    private IResidentialEntityService entity = entityService;
    private IResidentialValidationService validacion = validacionService;
    private IResidentialMantenimientoService mantenimiento = mantenimientoService;
    private readonly IDevicesRepository _devicesRepository = devicesRepository;
    private readonly IDataTransactionManager _transactionManager = transactionManager;
    
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

    public void Eliminar(string id)
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

    public ResidentialDto GetById(string id)
    {
        Residential? residential = db.GetById(id);
        if (residential == null)
        {
            throw new Exception("No se encontro el residential");
        }
        return FromEntity(residential);
    }

    public bool ProcesarHeartBeat(HeartbeatAuthContext authContext, string ipNueva)
    {
        if (string.IsNullOrWhiteSpace(ipNueva))
        {
            throw new InvalidOperationException("No se pudo determinar la IP del heartbeat");
        }

        using var transaction = _transactionManager.BeginTransaction();
        try
        {
            var accepted = _devicesRepository.TryAcceptHeartbeat(
                authContext.DeviceId,
                authContext.ResidentialId,
                authContext.Timestamp,
                authContext.TimestampUtc.UtcDateTime);

            if (!accepted)
            {
                transaction.Rollback();
                return false;
            }

            if (!db.TryUpdateIp(authContext.ResidentialId, ipNueva))
            {
                throw new InvalidOperationException("El Residential no existe");
            }

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
        finally
        {
            _transactionManager.ClearTracking();
        }
    }
}
