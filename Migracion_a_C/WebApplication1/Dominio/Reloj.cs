namespace Dominio;

public class Reloj
{
    private int _idReloj;
    private int _puerto;
    private string? _deviceSn = null;
    private DateTimeOffset? _lastPushEvent = null;
    private DateTimeOffset? _lastPollEvent = null;
    private int _residentialId { get; set; }     // FK explícita
    private Residential _residential { get; set; } = null!;

    public Reloj()
    {
    }

    public Reloj(
        int idReloj,
        int puerto,
        int residentialId
    )
    {

        this._idReloj = idReloj;
        this._puerto = puerto;
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

    public DateTimeOffset? LastPushEvent
    {
        get => _lastPushEvent;
        set => _lastPushEvent = value;
    }

    public DateTimeOffset? LastPollEvent
    {
        get => _lastPollEvent;
        set => _lastPollEvent = value;
    }
    
    public string? DeviceSn
    {
        get => _deviceSn;
        set => _deviceSn = value;
    }
}
