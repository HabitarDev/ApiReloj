namespace Dominio;

public class Reloj
{
    private int idReloj;
    private int puerto;
    private string secretKey = null!;

    public Reloj()
    {
    }

    public Reloj(
        int idReloj,
        int puerto,
        string secretKey
        )
    {

        this.idReloj = idReloj;
        this.puerto = puerto;
        this.secretKey = secretKey;
    }

    public int IdReloj
    {
        get => idReloj;
        set => idReloj = value;
    }

    public int Puerto
    {
        get => puerto;
        set => puerto = value;
    }

    public string SecretKey
    {
        get => secretKey;
        set => secretKey = value;
    }
}
