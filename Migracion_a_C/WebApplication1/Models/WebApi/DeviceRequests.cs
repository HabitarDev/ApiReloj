namespace Models.WebApi;

public sealed class CreateDeviceRequest
{
    public string _deviceId { get; set; } = null!;
    public string _secretKey { get; set; } = null!;
    public DateTime? _lastSeen { get; set; }
    public string _residentialId { get; set; } = null!;
}

public sealed class DeviceResponseDto
{
    public string _deviceId { get; set; } = null!;
    public DateTime? _lastSeen { get; set; }
    public string _residentialId { get; set; } = null!;
}
