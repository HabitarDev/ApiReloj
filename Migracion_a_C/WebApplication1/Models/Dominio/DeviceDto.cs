namespace Models.Dominio;

public class DeviceDto
{
    public string _deviceId { get; set; } = null!;
    [System.Text.Json.Serialization.JsonIgnore]
    public string _secretKey { get; set; } = null!;
    public DateTime? _lastSeen { get; set; }
    public string _residentialId { get; set; } = null!;
}
