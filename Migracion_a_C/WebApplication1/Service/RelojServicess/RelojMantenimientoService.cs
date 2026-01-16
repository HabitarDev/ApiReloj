using Dominio;
using IDataAcces;
using IServices.IReloj;
using IServices.IResidentials;
using Models.Dominio;

namespace Service.RelojServicess;

public class RelojMantenimientoService(IRelojEntityService relojEntityService,IRelojesRepository relojesRepository,IResidentialsRepository residentialRepo): IRelojMantenimientoService
{
    private IRelojEntityService _relojEntityService = relojEntityService;
    private IRelojesRepository _relojesRepository = relojesRepository;
    private IResidentialsRepository _residentialRepo = residentialRepo;
    
    public void Crear(RelojDto reloj)
    {
        Reloj? relojBuscado = _relojesRepository.GetById(reloj._idReloj);
        Residential? resiBuscado = _residentialRepo.GetById(reloj._residentialId);
        if (resiBuscado == null) throw  new Exception("El Residential no existe");
        if (relojBuscado != null) throw  new Exception("El Reloj ya existe");
        relojBuscado = _relojEntityService.ToEntity(reloj);
        _relojesRepository.Add(relojBuscado);
    }

    public void Modificar(Reloj reloj)
    {
        Reloj? relojBuscado = _relojesRepository.GetById(reloj.IdReloj);
        if (relojBuscado == null) throw  new Exception("El reloj no existe");
        _relojesRepository.update(relojBuscado);
    }

    public void Eliminar(int id)
    {
        Reloj? relojBuscado = _relojesRepository.GetById(id);
        if (relojBuscado == null) throw  new Exception("El reloj no existe");
        _relojesRepository.delete(id);
    }
}