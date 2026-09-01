namespace Dominio;

public class JornadaProjectionState
{
    public string EmployeeNumber { get; set; } = null!;
    public string ResidentialId { get; set; } = null!;
    public DateTimeOffset? DirtyFromUtc { get; set; }
    public string Status { get; set; } = JornadaProjectionStateStatuses.Pending;
    public long RequestedRevision { get; set; } = 1;
    public long AppliedRevision { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
