namespace Models.Dominio;

public class ResidentialDto
{
    public int _idResidential { get; set; }
    public string _ipActual { get; set; } = null!;
    public List<RelojDto> _relojes { get; set; } = [];
    public List<DeviceDto> _devices { get; set; } = [];
}
