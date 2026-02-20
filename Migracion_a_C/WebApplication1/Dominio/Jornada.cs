namespace Dominio;

public class Jornada
{
    public string JornadaId { get; set; } = null!;
    public string EmployeeNumber { get; set; } = null!;
    public string ClockSn { get; set; } = null!;

    public DateTimeOffset? StartAt { get; set; }
    public DateTimeOffset? BreakInAt { get; set; }
    public DateTimeOffset? BreakOutAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }

    public string StatusCheck { get; set; } = JornadaStatuses.Incomplete;
    public string StatusBreak { get; set; } = JornadaStatuses.Incomplete;

    public DateTimeOffset UpdatedAt { get; set; }
}
