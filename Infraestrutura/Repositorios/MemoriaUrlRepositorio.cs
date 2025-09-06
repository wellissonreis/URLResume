using System.Collections.Concurrent;
using URLResume.Dominio.Entidades;

namespace URLResume.Infraestrutura.Repositorios;

public class MemoriaUrlRepositorio : IUrlRepositorio
{
    private readonly ConcurrentDictionary<string, UrlRegistro> _armazenamento = new();

    public UrlRegistro Salvar(UrlRegistro registro)
    {
        _armazenamento[registro.Codigo] = registro;
        return registro;
    }

    public UrlRegistro? Obter(string codigo)
    {
        return _armazenamento.TryGetValue(codigo, out var registro) ? registro : null;
    }

    public void Atualizar(UrlRegistro registro)
    {
        _armazenamento[registro.Codigo] = registro;
    }
}

