namespace Models.Dominio;

public class ResidentialDto
{
    public string _idResidential { get; set; } = null!;
    public string _ipActual { get; set; } = null!;
    public List<RelojDto> _relojes { get; set; } = [];
    public List<DeviceDto> _devices { get; set; } = [];
}
