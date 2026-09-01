namespace Models.WebApi;

public static class BackfillPollTriggers
{
    public const string Manual = "manual";
    public const string Scheduled = "scheduled";

    public static bool IsValid(string? value) => value is Manual or Scheduled;
}

public static class BackfillPollRunStatuses
{
    public const string Running = "running";
    public const string Ok = "ok";
    public const string PartialError = "partial_error";
    public const string Error = "error";
}

public static class BackfillPollClockStatuses
{
    public const string Pending = "pending";
    public const string Ok = "ok";
    public const string Skipped = "skipped";
    public const string Error = "error";
}
