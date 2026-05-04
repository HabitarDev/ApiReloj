namespace Models.WebApi;

public class CrearRelojRequest
{
    public string _idReloj { get; set; } = null!;
    public int _puerto { get; set; }
    public string _residentialId { get; set; } = null!;
}
