namespace Dominio;

public class Reloj
{
    private int _idReloj;
    private int _puerto;
    private string _secretKey = null!;
    private DateTime? _lastSeen;
    
    public int _residentialId { get; set; }     // FK explícita
    public Residential _residential { get; set; } = null!;

    public Reloj()
    {
    }

    public Reloj(
        int idReloj,
        int puerto,
        string secretKey,
        DateTime lastSeen,
        int residentialId
    )
    {

        this._idReloj = idReloj;
        this._puerto = puerto;
        this._secretKey = secretKey;
        this._lastSeen = lastSeen;
        this._residentialId = residentialId;
    }

    public int IdReloj
    {
        get => _idReloj;
        set => _idReloj = value;
    }

    public int Puerto
    {
        get => _puerto;
        set => _puerto = value;
    }

    public string SecretKey
    {
        get => _secretKey;
        set => _secretKey = value;
    }
    
    public DateTime? LastSeen
    {
        get => _lastSeen;
        set => _lastSeen = value;
    }

        public int ResidentialId
    {
        get => _residentialId;
        set => _residentialId = value;
    }

    public Residential Residential
    {
        get => _residential;
        set => _residential = value;
    }
}