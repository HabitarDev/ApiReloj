namespace Models.WebApi;

public class HeartBeatDto
{
    public string DeviceId { get; set; } = null!;
    public string ResidentialId { get; set; } = null!;
    public long TimeStamp { get; set; }
    public string Signature { get; set; }
}