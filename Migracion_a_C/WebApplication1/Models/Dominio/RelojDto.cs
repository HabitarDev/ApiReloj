namespace Models.Dominio;

public class RelojDto
{
    public int _idReloj;
    public int _puerto;
    public string _secretKey = null!;
    public DateTime? _lastSeen;
    public int _residentialId { get; set; }   
}