namespace Models.WebApi;

public class AccessEventRawEnvelopeDto
{
    public string SchemaVersion { get; set; } = "v1";
    public string Source { get; set; } = null!;
    public string Format { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public bool HasPicture { get; set; }
    public DateTimeOffset CapturedAtUtc { get; set; }
    public string Payload { get; set; } = null!;
}
