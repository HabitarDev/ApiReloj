namespace Models.WebApi;

public class BackfillPollingOptions
{
    public const string SectionName = "BackfillPolling";

    public int WorkerIntervalMinutes { get; set; } = 30;
    public int WindowMinutes { get; set; } = 30;
    public int MaxResultsPerPage { get; set; } = 30;
    public int HttpTimeoutSeconds { get; set; } = 30;
    public bool RunOnStartup { get; set; } = true;

    public DateTimeOffset BootstrapStartUtc { get; set; } =
        new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public int MaxWindowsPerRun { get; set; } = 5000;
}
