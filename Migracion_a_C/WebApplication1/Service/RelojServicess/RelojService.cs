using Dominio;
using IDataAcces;
using IServices.IReloj;
using Models.Dominio;
using Models.WebApi;

namespace Service.RelojServicess;

public class RelojService (IRelojMantenimientoService mantenimientoService,IRelojesRepository repo, IRelojEntityService entityService, IRelojValidacionService validacionService): IRelojService
{
    private IRelojesRepository db = repo;
    private IRelojEntityService entity = entityService;
    private IRelojValidacionService validacion = validacionService;
    private IRelojMantenimientoService mantenimiento = mantenimientoService;
    
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

    public void Crear(CrearRelojRequest dto)
    {
        RelojDto adaptado = new RelojDto();
        adaptado._idReloj = dto._idReloj;
        adaptado._puerto = dto._puerto;
        adaptado._residentialId = dto._residentialId;
        validacion.Validar(adaptado);
        mantenimiento.Crear(adaptado);
    }

    public void Modificar(Reloj reloj)
    {
        mantenimiento.Modificar(reloj);
    }

    public void ModificarDesdeDto(ActualizarRelojRequest relojDto)
{
    if (relojDto._idReloj <= 0) throw new ArgumentException("Id de reloj invalido");
    if (relojDto._puerto <= 0) throw new ArgumentException("Puerto de reloj invalido");
    if (string.IsNullOrWhiteSpace(relojDto._deviceSn)) throw new ArgumentException("DeviceSn invalido");

    RelojDto dtoNecesario = GetById(relojDto._idReloj);
    Reloj entidad = ToEntity(dtoNecesario);

    entidad.Puerto = relojDto._puerto;
    entidad.DeviceSn = relojDto._deviceSn;

    Modificar(entidad);
}

    public void Eliminar(int id)
    {
        mantenimiento.Eliminar(id);
    }
    
    public List<RelojDto> Listar()
    {
        List<RelojDto> listaADevolver = new List<RelojDto>();
        foreach (var res in db.GetAll())
        {
            listaADevolver.Add(FromEntity(res));   
        }
        return listaADevolver;
    }

    public RelojDto GetById(int id)
    {
        Reloj? reloj = db.GetById(id);
        if (reloj == null)
        {
            throw new Exception("No se encontro el reloj");
        }
        return FromEntity(reloj);
    }
}
