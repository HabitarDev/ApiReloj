namespace Dominio;

public class Jornada
{
    public string JornadaId { get; set; } = null!;
    public string EmployeeNumber { get; set; } = null!;
    public string ResidentialId { get; set; } = null!;

    // Se conserva por compatibilidad. Representa el reloj del primer evento
    // que identifica la jornada; la salida puede ocurrir en otro reloj.
    public string ClockSn { get; set; } = null!;

    public DateTimeOffset? StartAt { get; set; }
    public DateTimeOffset? BreakInAt { get; set; }
    public DateTimeOffset? BreakOutAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }

    public string StatusCheck { get; set; } = JornadaStatuses.Incomplete;
    public string StatusBreak { get; set; } = JornadaStatuses.Incomplete;

    public string? IdentityDeviceSn { get; set; }
    public long? IdentitySerialNumber { get; set; }

    public string? StartDeviceSn { get; set; }
    public long? StartSerialNumber { get; set; }
    public string? BreakInDeviceSn { get; set; }
    public long? BreakInSerialNumber { get; set; }
    public string? BreakOutDeviceSn { get; set; }
    public long? BreakOutSerialNumber { get; set; }
    public string? EndDeviceSn { get; set; }
    public long? EndSerialNumber { get; set; }

    public string WarningsJson { get; set; } = "[]";
    public string ErrorsJson { get; set; } = "[]";
    public string ProjectionStatus { get; set; } = JornadaProjectionStatuses.Ready;
    public long Revision { get; set; } = 1;
    public bool IsDeleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
