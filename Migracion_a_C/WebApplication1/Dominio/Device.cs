namespace Dominio;

public class Device
{
    private string _deviceId = null!;
    private string _secretKey = null!;
    private DateTime? _lastSeen;
    private string _residentialId = null!;
    private Residential _residential = null!;

    public Device()
    {
    }

    public Device(
        string deviceId,
        string secretKey,
        DateTime? lastSeen,
        string residentialId
    )
    {
        _deviceId = deviceId;
        _secretKey = secretKey;
        _lastSeen = lastSeen;
        _residentialId = residentialId;
    }

    public string DeviceId
    {
        get => _deviceId;
        set => _deviceId = value;
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
}
