namespace Dominio;

public class Device
{
    private int _deviceId;
    private string _secretKey = null!;
    private DateTime? _lastSeen;
    private int _residentialId;
    private Residential _residential = null!;

    public Device()
    {
    }

    public Device(
        int deviceId,
        string secretKey,
        DateTime? lastSeen,
        int residentialId
    )
    {
        _deviceId = deviceId;
        _secretKey = secretKey;
        _lastSeen = lastSeen;
        _residentialId = residentialId;
    }

    public int DeviceId
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
