namespace Dominio;

public class Residential
{
    private int _idResidential;
    private string _ipActual = null!;
    private List<Reloj> _relojes = [];
    private List<Device> _devices = [];

    public Residential()
    {
    }

    public Residential(
        int idResidential,
        string ipActual,
        List<Reloj> relojes
    )
    {
        _idResidential = idResidential;
        _ipActual = ipActual;
        _relojes = relojes;
    }

    public Residential(
        int idResidential,
        string ipActual,
        List<Reloj> relojes,
        List<Device> devices
    )
    {
        _idResidential = idResidential;
        _ipActual = ipActual;
        _relojes = relojes;
        _devices = devices;
    }

    public int IdResidential
    {
        get => _idResidential;
        set => _idResidential = value;
    }

    public string IpActual
    {
        get => _ipActual;
        set => _ipActual = value;
    }

    public List<Reloj> Relojes
    {
        get => _relojes;
        set => _relojes = value;
    }

    public List<Device> Devices
    {
        get => _devices;
        set => _devices = value;
    }

}
