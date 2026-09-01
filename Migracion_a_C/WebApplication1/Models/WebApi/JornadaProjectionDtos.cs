namespace Models.WebApi;

public class JornadaRebuildRequestDto
{
    public string EmployeeNumber { get; set; } = null!;
    public string ResidentialId { get; set; } = null!;
    public DateTimeOffset? FromUtc { get; set; }
}

public class JornadaProjectionStateDto
{
    public string EmployeeNumber { get; set; } = null!;
    public string ResidentialId { get; set; } = null!;
    public DateTimeOffset? DirtyFromUtc { get; set; }
    public string Status { get; set; } = null!;
    public long RequestedRevision { get; set; }
    public long AppliedRevision { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
