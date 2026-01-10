namespace Dominio;

public class Residential
{
    private int idResidential;
    private string ipActual = null!;
    private List<Reloj> relojes = [];

    public Residential()
    {
    }

    public Residential(
        int idResidential,
        string ipActual,
        List<Reloj> relojes
        )
    {

        this.idResidential = idResidential;

        this.ipActual = ipActual;
        this.relojes = relojes ?? [];
    }

    public int IdResidential
    {
        get => idResidential;
        set => idResidential = value;
    }

        public string IpActual
    {
        get => ipActual;
        set => ipActual = value;
    }

        public List<Reloj> Relojes
    {
        get => relojes;
        set => relojes = value;
    }

}
