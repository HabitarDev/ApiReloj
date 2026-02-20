namespace Models.WebApi;

public class JornadaProcessingOptions
{
    public const string SectionName = "JornadaProcessing";

    public int WorkerIntervalMinutes { get; set; } = 5;
    public int IncompleteTimeoutHours { get; set; } = 24;

    public JornadaAttendanceMapOptions AttendanceStatusMap { get; set; } = new();
}
