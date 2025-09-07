using URLResume.Dominio.Entidades;

namespace URLResume.Dominio.Servicos;

public interface IEncurtadorDeUrl
{
    UrlRegistro Encurtar(string urlOriginal);
    UrlRegistro? ObterPorCodigo(string codigo);
    void RegistrarAcesso(string codigo);
}

