using System.Text.Json;
using StackExchange.Redis;
using URLResume.Dominio.Entidades;

namespace URLResume.Infraestrutura.Repositorios;

public class CacheUrlRepositorio : IUrlRepositorio
{
    private readonly IUrlRepositorio _inner;
    private readonly IDatabase _cache;
    private readonly TimeSpan _ttl;

    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public CacheUrlRepositorio(IUrlRepositorio inner, IConnectionMultiplexer multiplexer, IConfiguration config)
    {
        _inner = inner;
        _cache = multiplexer.GetDatabase();
        var ttlSeconds =  int.TryParse(config["Redis:DefaultTtlSeconds"], out var s) ? s : 86400; // 24h default
        _ttl = TimeSpan.FromSeconds(Math.Max(60, ttlSeconds));
    }

    public UrlRegistro Salvar(UrlRegistro registro)
    {
        var saved = _inner.Salvar(registro);
        SetCache(saved);
        return saved;
    }

    public UrlRegistro? Obter(string codigo)
    {
        var key = Key(codigo);
        var cached = _cache.StringGet(key);
        if (cached.HasValue)
        {
            try
            {
                var dto = JsonSerializer.Deserialize<UrlCacheDto>(cached!, _jsonOptions);
                if (dto is not null)
                    return new UrlRegistro(codigo, dto.UrlOriginal, dto.CriadoEm, dto.Acessos);
            }
            catch { /* ignore cache errors */ }
        }

        var fromDb = _inner.Obter(codigo);
        if (fromDb is not null)
            SetCache(fromDb);
        return fromDb;
    }

    public void Atualizar(UrlRegistro registro)
    {
        _inner.Atualizar(registro);
        SetCache(registro);
    }

    private void SetCache(UrlRegistro registro)
    {
        try
        {
            var dto = new UrlCacheDto
            {
                UrlOriginal = registro.UrlOriginal,
                CriadoEm = registro.CriadoEm,
                Acessos = registro.Acessos
            };
            var payload = JsonSerializer.Serialize(dto, _jsonOptions);
            _cache.StringSet(Key(registro.Codigo), payload, _ttl, When.Always, CommandFlags.FireAndForget);
        }
        catch { /* ignore cache errors */ }
    }

    private static string Key(string codigo) => $"url:{codigo}";

    private sealed class UrlCacheDto
    {
        public string UrlOriginal { get; set; } = string.Empty;
        public DateTime CriadoEm { get; set; }
        public long Acessos { get; set; }
    }
}

