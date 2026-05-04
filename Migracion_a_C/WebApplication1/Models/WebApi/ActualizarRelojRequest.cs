namespace Models.WebApi;

public class ActualizarRelojRequest
{
    public string _idReloj { get; set; } = null!;
    public int _puerto { get; set; }
    public string _deviceSn { get; set; }
}