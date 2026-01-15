using Dominio;
using IDataAcces;
using IServices.IResidentials;
using Models.Dominio;

namespace Service.ResidentialServicess;

public class ResidentialMantenimientoService(IResidentialsRepository residentialRepo, IResidentialEntityService residentialEntityService) : IResidentialMantenimientoService
{
    private IResidentialsRepository _residentialRepo = residentialRepo;
    private IResidentialEntityService _residentialEntityService = residentialEntityService;
    public void Crear(ResidentialDto resiACrear)
    {
        Residential? resiBuscado = _residentialRepo.GetById(resiACrear._idResidential);
        if (resiBuscado != null) throw  new Exception("El Residential ya existe");
        resiBuscado = _residentialEntityService.ToEntity(resiACrear);
        _residentialRepo.Add(resiBuscado);
    }

    public void Modificar(Residential resiAModificar)
    {
        Residential? resiBuscado = _residentialRepo.GetById(resiAModificar.IdResidential);
        if (resiBuscado == null) throw  new Exception("El Residential no existe");
        _residentialRepo.update(resiBuscado);
    }

    public void Eliminar(int idAEliinar)
    {
        Residential? resiBuscado = _residentialRepo.GetById(idAEliinar);
        if (resiBuscado == null) throw  new Exception("El Residential no existe");
        _residentialRepo.delete(resiBuscado.IdResidential);
    }
}