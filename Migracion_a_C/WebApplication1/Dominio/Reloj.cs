namespace Dominio;

public class Reloj
{
    private string _idReloj = null!;
    private int _puerto;
    private string? _deviceSn = null;
    private DateTimeOffset? _lastPushEvent = null;
    private DateTimeOffset? _lastPollEvent = null;
    private string _residentialId = null!;
    private Residential _residential = null!;

    public Reloj()
    {
    }

    public Reloj(
        string idReloj,
        int puerto,
        string residentialId
    )
    {
        _idReloj = idReloj;
        _puerto = puerto;
        _residentialId = residentialId;
    }

    public string IdReloj
    {
        get => _idReloj;
        set => _idReloj = value;
    }

    public int Puerto
    {
        get => _puerto;
        set => _puerto = value;
    }

    public string ResidentialId
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
