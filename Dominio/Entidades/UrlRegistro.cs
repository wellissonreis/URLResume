namespace URLResume.Dominio.Entidades;

public class UrlRegistro
{
    public string Codigo { get; }
    public string UrlOriginal { get; }
    public DateTime CriadoEm { get; private set; } = DateTime.UtcNow;
    public long Acessos { get; private set; }

    public UrlRegistro(string codigo, string urlOriginal)
    {
        Codigo = codigo;
        UrlOriginal = urlOriginal;
    }

    public UrlRegistro(string codigo, string urlOriginal, DateTime criadoEm, long acessos)
    {
        Codigo = codigo;
        UrlOriginal = urlOriginal;
        CriadoEm = criadoEm;
        Acessos = acessos;
    }

    public void RegistrarAcesso()
    {
        Acessos++;
    }
}
