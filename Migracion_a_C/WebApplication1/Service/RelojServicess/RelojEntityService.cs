using Dominio;
using IDataAcces;
using IServices.IReloj;
using Models.Dominio;

namespace Service.RelojServicess;

public class RelojEntityService(IRelojesRepository repo, IResidentialsRepository repoResidencials) : IRelojEntityService
{
    public IRelojesRepository dbRelojes = repo;
    public IResidentialsRepository dbResidentials = repoResidencials;
    public Reloj ToEntity(RelojDto dto)
    {
        Reloj? relojParaRetornar = dbRelojes.GetById(dto._idReloj);
        if (relojParaRetornar == null)
        {
            Residential? residencialDuenio= dbResidentials.GetById(dto._residentialId);
            if (residencialDuenio != null)
            {
                relojParaRetornar = new Reloj();
                relojParaRetornar.ResidentialId = residencialDuenio.IdResidential;
                relojParaRetornar.Puerto = dto._puerto;
                relojParaRetornar.Residential =  residencialDuenio;
                relojParaRetornar.IdReloj = dto._idReloj;
            }
            else
            {
                throw  new ArgumentException("No se encuentra reloj ni residencial");
            }
        }
        return relojParaRetornar;
    }

    public RelojDto FromEntity(Reloj relojRecibido)
    {
        
        RelojDto paraDevolver = new RelojDto();
        paraDevolver._idReloj = relojRecibido.IdReloj;
        paraDevolver._puerto = relojRecibido.Puerto;
        paraDevolver._residentialId = relojRecibido.ResidentialId;
        return paraDevolver;
    }
}
