using Dominio;
using IDataAcces;
using IServices.IReloj;
using Models.Dominio;

namespace Service.RelojServicess;

public class RelojService (IRelojesRepository repo, IRelojEntityService entityService, IRelojValidacionService validacionService): IRelojService
{
    public IRelojesRepository db = repo;
    public IRelojEntityService entity = entityService;
    public IRelojValidacionService validacion = validacionService;
    
    public Reloj ToEntity(RelojDto dto)
    {
        Validar(dto);
        return entity.ToEntity(dto);
    }

    public RelojDto FromEntity(Reloj atraccion)
    {
        RelojDto dto = entity.FromEntity(atraccion);
        Validar(dto);
        return dto;
    }

    public void Validar(RelojDto dto)
    {
        validacion.Validar(dto);
    }
}