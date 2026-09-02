namespace Dominio;

public static class JornadaStatuses
{
    public const string Ok = "OK";
    public const string Incomplete = "INCOMPLETE";
    public const string Error = "ERROR";
    public const string NoBreak = "NO_BREAK";
}

public static class JornadaProjectionStatuses
{
    public const string Ready = "READY";
}

public static class JornadaProjectionStateStatuses
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Ready = "READY";
    public const string Error = "ERROR";
}

public static class JornadaIssueCodes
{
    public const string DuplicateCheckInIgnored = "DUPLICATE_CHECK_IN_IGNORED";
    public const string DuplicateCheckOutIgnored = "DUPLICATE_CHECK_OUT_IGNORED";
    public const string DuplicateBreakInIgnored = "DUPLICATE_BREAK_IN_IGNORED";
    public const string DuplicateBreakOutIgnored = "DUPLICATE_BREAK_OUT_IGNORED";
    public const string SecondBreakIgnored = "SECOND_BREAK_IGNORED";
    public const string MissingCheckIn = "MISSING_CHECK_IN";
    public const string MissingCheckOut = "MISSING_CHECK_OUT";
    public const string MissingBreakIn = "MISSING_BREAK_IN";
    public const string MissingBreakOut = "MISSING_BREAK_OUT";
    public const string MaximumDurationExceeded = "MAXIMUM_DURATION_EXCEEDED";
}
