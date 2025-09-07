using URLResume.Dominio.Entidades;

namespace URLResume.Infraestrutura.Repositorios;

public interface IUrlRepositorio
{
    UrlRegistro Salvar(UrlRegistro registro);
    UrlRegistro? Obter(string codigo);
    void Atualizar(UrlRegistro registro);
}

