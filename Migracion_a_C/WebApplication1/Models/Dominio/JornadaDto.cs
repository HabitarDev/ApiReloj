namespace Models.Dominio;

public class JornadaDto
{
    public string JornadaId { get; set; } = null!;
    public string EmployeeNumber { get; set; } = null!;
    public string ResidentialId { get; set; } = null!;
    public string ClockSn { get; set; } = null!;

    public DateTimeOffset? StartAt { get; set; }
    public DateTimeOffset? BreakInAt { get; set; }
    public DateTimeOffset? BreakOutAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }

    public string StatusCheck { get; set; } = null!;
    public string StatusBreak { get; set; } = null!;
    public List<string> Warnings { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public string ProjectionStatus { get; set; } = null!;
    public long Revision { get; set; }
    public bool IsDeleted { get; set; }
    public string? StartDeviceSn { get; set; }
    public long? StartSerialNumber { get; set; }
    public string? BreakInDeviceSn { get; set; }
    public long? BreakInSerialNumber { get; set; }
    public string? BreakOutDeviceSn { get; set; }
    public long? BreakOutSerialNumber { get; set; }
    public string? EndDeviceSn { get; set; }
    public long? EndSerialNumber { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
