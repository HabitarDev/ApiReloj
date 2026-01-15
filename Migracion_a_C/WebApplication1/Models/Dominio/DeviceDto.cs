namespace Models.Dominio;

public class DeviceDto
{
    public int _deviceId { get; set; }
    public string _secretKey { get; set; } = null!;
    public DateTime? _lastSeen { get; set; }
    public int _residentialId { get; set; }
}
