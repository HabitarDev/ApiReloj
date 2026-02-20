namespace Models.Dominio;

public class JornadaDto
{
    public string JornadaId { get; set; } = null!;
    public string EmployeeNumber { get; set; } = null!;
    public string ClockSn { get; set; } = null!;

    public DateTimeOffset? StartAt { get; set; }
    public DateTimeOffset? BreakInAt { get; set; }
    public DateTimeOffset? BreakOutAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }

    public string StatusCheck { get; set; } = null!;
    public string StatusBreak { get; set; } = null!;
    public DateTimeOffset UpdatedAt { get; set; }
}
