namespace Dominio;

public class Reloj
{
    private int _idReloj;
    private int _puerto;
    private string _secretKey = null!;

    public Reloj()
    {
    }

    public Reloj(
        int idReloj,
        int puerto,
        string secretKey
    )
    {

        this._idReloj = idReloj;
        this._puerto = puerto;
        this._secretKey = secretKey;
    }

    public int IdReloj
    {
        get => _idReloj;
        set => _idReloj = value;
    }

    public int Puerto
    {
        get => _puerto;
        set => _puerto = value;
    }

    public string SecretKey
    {
        get => _secretKey;
        set => _secretKey = value;
    }
}