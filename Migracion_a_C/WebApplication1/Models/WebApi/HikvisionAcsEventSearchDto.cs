using System.Text.Json.Serialization;

namespace Models.WebApi;

public class HikvisionAcsEventSearchRequestDto
{
    [JsonPropertyName("AcsEventCond")]
    public HikvisionAcsEventCondDto AcsEventCond { get; set; } = new();
}

public class HikvisionAcsEventCondDto
{
    [JsonPropertyName("searchID")]
    public string SearchId { get; set; } = null!;

    [JsonPropertyName("searchResultPosition")]
    public int SearchResultPosition { get; set; }

    [JsonPropertyName("maxResults")]
    public int MaxResults { get; set; }

    [JsonPropertyName("startTime")]
    public string StartTime { get; set; } = null!;

    [JsonPropertyName("endTime")]
    public string EndTime { get; set; } = null!;

    [JsonPropertyName("timeReverseOrder")]
    public bool TimeReverseOrder { get; set; }

    [JsonPropertyName("isAttendanceInfo")]
    public bool IsAttendanceInfo { get; set; } = true;
}

public class HikvisionAcsEventSearchResponseDto
{
    [JsonPropertyName("AcsEvent")]
    public HikvisionAcsEventResultDto? AcsEvent { get; set; }
}

public class HikvisionAcsEventResultDto
{
    [JsonPropertyName("searchID")]
    public string? SearchId { get; set; }

    [JsonPropertyName("responseStatusStrg")]
    public string? ResponseStatusStrg { get; set; }

    [JsonPropertyName("numOfMatches")]
    public int NumOfMatches { get; set; }

    [JsonPropertyName("totalMatches")]
    public int TotalMatches { get; set; }

    [JsonPropertyName("InfoList")]
    public List<HikvisionAcsEventInfoDto> InfoList { get; set; } = [];
}

public class HikvisionAcsEventInfoDto
{
    [JsonPropertyName("major")]
    public int? Major { get; set; }

    [JsonPropertyName("minor")]
    public int? Minor { get; set; }

    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("employeeNoString")]
    public string? EmployeeNoString { get; set; }

    [JsonPropertyName("serialNo")]
    public long? SerialNo { get; set; }

    [JsonPropertyName("attendanceStatus")]
    public string? AttendanceStatus { get; set; }

    [JsonPropertyName("currentVerifyMode")]
    public string? CurrentVerifyMode { get; set; }
}
