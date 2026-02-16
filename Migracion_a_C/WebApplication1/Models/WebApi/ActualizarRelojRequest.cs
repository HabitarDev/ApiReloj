namespace Models.WebApi;

public class ActualizarRelojRequest
{
    public int _idReloj { get; set; }
    public int _puerto { get; set; }
    public string _deviceSn { get; set; }
}