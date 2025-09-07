using System.Security.Cryptography;
using System.Text;
using URLResume.Dominio.Entidades;
using URLResume.Dominio.Servicos;
using URLResume.Infraestrutura.Repositorios;

namespace URLResume.Aplicacao.Servicos;

public class EncurtadorDeUrl : IEncurtadorDeUrl
{
    private readonly IUrlRepositorio _repositorio;

    public EncurtadorDeUrl(IUrlRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    public UrlRegistro Encurtar(string urlOriginal)
    {
        // Gera um código curto determinístico por hora para evitar explosão de colisoes
        var codigo = GerarCodigo(urlOriginal);

        var existente = _repositorio.Obter(codigo);
        if (existente is not null)
            return existente;

        var novo = new UrlRegistro(codigo, urlOriginal);
        return _repositorio.Salvar(novo);
    }

    public UrlRegistro? ObterPorCodigo(string codigo)
    {
        return _repositorio.Obter(codigo);
    }

    public void RegistrarAcesso(string codigo)
    {
        var registro = _repositorio.Obter(codigo);
        if (registro is null) return;
        registro.RegistrarAcesso();
        _repositorio.Atualizar(registro);
    }

    private static string GerarCodigo(string url)
    {
        // Usa HMAC + Base62 com salt temporal curto para bons códigos curtos
        var salt = DateTime.UtcNow.ToString("yyyyMMddHH");
        var chave = Encoding.UTF8.GetBytes("urlresume-salt-simples");
        using var hmac = new HMACSHA256(chave);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(url + salt));
        // Pega primeiros 6 bytes -> 8-10 chars base62
        var curto = hash.Take(6).ToArray();
        return Base62(curto);
    }

    private static string Base62(ReadOnlySpan<byte> dados)
    {
        const string alfabeto = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        // Converte bytes em inteiro grande e divide em base 62
        var valor = new System.Numerics.BigInteger(dados.ToArray().Append((byte)0).ToArray());
        var sb = new StringBuilder();
        while (valor > 0)
        {
            valor = System.Numerics.BigInteger.DivRem(valor, 62, out var resto);
            sb.Insert(0, alfabeto[(int)resto]);
        }
        return sb.Length == 0 ? "0" : sb.ToString();
    }
}

