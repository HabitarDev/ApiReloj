namespace Models.WebApi;

public class JornadaAttendanceMapOptions
{
    public List<string> CheckIn { get; set; } = ["checkIn"];
    public List<string> BreakIn { get; set; } = ["breakIn"];
    public List<string> BreakOut { get; set; } = ["breakOut"];
    public List<string> CheckOut { get; set; } = ["checkOut"];
}
