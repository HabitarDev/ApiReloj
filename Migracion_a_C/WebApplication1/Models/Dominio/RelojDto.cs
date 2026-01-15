namespace Models.Dominio;

public class RelojDto
{
    private int _idReloj;
    private int _puerto;
    private string _secretKey = null!;
    private DateTime? _lastSeen;
    public int _residentialId { get; set; }   
}