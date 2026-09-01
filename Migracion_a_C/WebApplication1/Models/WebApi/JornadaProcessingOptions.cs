namespace Models.WebApi;

public class JornadaProcessingOptions
{
    public const string SectionName = "JornadaProcessing";

    public int WorkerIntervalMinutes { get; set; } = 5;
    public int WorkerIntervalSeconds { get; set; } = 2;
    public int BatchSize { get; set; } = 20;
    public int MaxAttempts { get; set; } = 8;
    public int RetryBaseSeconds { get; set; } = 5;
    public int IncompleteTimeoutHours { get; set; } = 24;

    public JornadaAttendanceMapOptions AttendanceStatusMap { get; set; } = new();
}
