namespace Models.WebApi;

public class HeartBeatDto
{
    public int DeviceId { get; set; }
    public int ResidentialId { get; set; }
    public long TimeStamp { get; set; }
    public string Signature { get; set; }
}