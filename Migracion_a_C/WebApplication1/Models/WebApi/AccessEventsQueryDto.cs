namespace Models.WebApi;

public class AccessEventsQueryDto
{
    public int? ResidentialId { get; set; }
    public string? DeviceSn { get; set; }
    public string? EmployeeNumber { get; set; }

    // Filtros opcionales por tipo de evento.
    public int? Major { get; set; }
    public int? Minor { get; set; }
    public string? AttendanceStatus { get; set; }

    public DateTimeOffset? FromUtc { get; set; }
    public DateTimeOffset? ToUtc { get; set; }

    public int Limit { get; set; } = 100;
    public int Offset { get; set; } = 0;
}
