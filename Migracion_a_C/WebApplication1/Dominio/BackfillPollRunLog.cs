namespace Dominio;

public class BackfillPollRunLog
{
    public string RunId { get; set; } = null!;
    public string Trigger { get; set; } = null!;

    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }

    public string Status { get; set; } = null!;
    public string? Error { get; set; }

    public int TotalClocks { get; set; }
    public int TotalWindows { get; set; }
    public int TotalPages { get; set; }
    public int Inserted { get; set; }
    public int Duplicates { get; set; }
    public int Ignored { get; set; }

    public string ClocksJson { get; set; } = "[]";
}
