namespace Models.WebApi;

public class BackfillPollRunRequestDto
{
    public int? ResidentialId { get; set; }
    public int? RelojId { get; set; }
    public string Trigger { get; set; } = "manual";
}

public class BackfillPollRunsQueryDto
{
    public string? Status { get; set; }
    public int Limit { get; set; } = 50;
    public int Offset { get; set; } = 0;
}

public class PollIngestResultDto
{
    public int Inserted { get; set; }
    public int Duplicates { get; set; }
    public int Ignored { get; set; }
    public DateTimeOffset? MaxEventTimeUtc { get; set; }
}

public class BackfillPollClockResultDto
{
    public int RelojId { get; set; }
    public string? DeviceSn { get; set; }

    public string Status { get; set; } = null!;
    public string? Note { get; set; }
    public string? Error { get; set; }

    public DateTimeOffset? CursorBefore { get; set; }
    public DateTimeOffset? CursorAfter { get; set; }

    public int WindowsProcessed { get; set; }
    public int PagesProcessed { get; set; }
    public int Inserted { get; set; }
    public int Duplicates { get; set; }
    public int Ignored { get; set; }
}

public class BackfillPollRunResultDto
{
    public string RunId { get; set; } = null!;
    public string Trigger { get; set; } = null!;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset FinishedAtUtc { get; set; }

    public string Status { get; set; } = null!;
    public string? Error { get; set; }

    public int TotalClocks { get; set; }
    public int TotalWindows { get; set; }
    public int TotalPages { get; set; }
    public int Inserted { get; set; }
    public int Duplicates { get; set; }
    public int Ignored { get; set; }

    public List<BackfillPollClockResultDto> Clocks { get; set; } = [];
}

public class BackfillPollRunSummaryDto
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
}

public class BackfillPollStatusDto
{
    public bool IsRunning { get; set; }
    public string? LastRunId { get; set; }
    public string? LastTrigger { get; set; }
    public DateTimeOffset? LastStartedAtUtc { get; set; }
    public DateTimeOffset? LastFinishedAtUtc { get; set; }
    public string? LastStatus { get; set; }
    public string? LastError { get; set; }
    public int LastTotalClocks { get; set; }
    public int LastInserted { get; set; }
    public int LastDuplicates { get; set; }
    public int LastIgnored { get; set; }
}
