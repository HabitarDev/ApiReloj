namespace Models.WebApi;

public class JornadasQueryDto
{
    public int? ResidentialId { get; set; }
    public string? ClockSn { get; set; }
    public string? EmployeeNumber { get; set; }
    public string? StatusCheck { get; set; }
    public string? StatusBreak { get; set; }

    public DateTimeOffset? FromUtc { get; set; }
    public DateTimeOffset? ToUtc { get; set; }
    public DateTimeOffset? UpdatedSinceUtc { get; set; }

    public int Limit { get; set; } = 100;
    public int Offset { get; set; } = 0;
}
