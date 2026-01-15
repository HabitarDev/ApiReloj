namespace Models.WebApi;

public class CrearRelojRequest
{
    public int _idReloj { get; set; }
    public int _puerto { get; set; }
    public string _secretKey { get; set; } = null!;
    public int _residentialId { get; set; }   
}
