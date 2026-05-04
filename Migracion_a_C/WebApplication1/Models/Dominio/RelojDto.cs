namespace Models.Dominio;

public class RelojDto
{
    public string _idReloj { get; set; } = null!;
    public int _puerto { get; set; }
    public string _residentialId { get; set; } = null!;
    public string? _deviceSn { get; set; }
}
