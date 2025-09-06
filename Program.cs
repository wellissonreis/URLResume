using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Extensions.NETCore.Setup;
using URLResume.Aplicacao.Servicos;
using URLResume.Dominio.Servicos;
using URLResume.Infraestrutura.Repositorios;
using URLResume.Requisicoes;
using URLResume.Respostas;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Provider de armazenamento (Memory ou DynamoDB)
var storageProvider = builder.Configuration["Storage:Provider"] ?? (builder.Environment.IsDevelopment() ? "Memory" : "DynamoDB");

// AWS DynamoDB (registrado somente quando provider = DynamoDB)
if (string.Equals(storageProvider, "DynamoDB", StringComparison.OrdinalIgnoreCase))
{
    // Usa o pacote AWSSDK.Extensions.NETCore.Setup para ler AWS Options de configuração (Profile, Region, etc.)
    builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
    builder.Services.AddAWSService<IAmazonDynamoDB>();
}

// Redis (StackExchange.Redis)
var redisConn = builder.Configuration["Redis:ConnectionString"] ?? Environment.GetEnvironmentVariable("REDIS_CONNECTION") ?? "localhost:6379";
if (!redisConn.Contains("abortConnect", StringComparison.OrdinalIgnoreCase))
{
    redisConn += ",abortConnect=false,connectRetry=5,connectTimeout=5000,syncTimeout=5000";
}
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));

// DI: Repositório (DynamoDB ou Memória) + Decorator de Cache (Redis) e serviço de encurtamento
builder.Services.AddSingleton<IUrlRepositorio>(sp =>
{
    IUrlRepositorio inner;
    if (string.Equals(storageProvider, "DynamoDB", StringComparison.OrdinalIgnoreCase))
    {
        var dynamo = sp.GetRequiredService<IAmazonDynamoDB>();
        var config = sp.GetRequiredService<IConfiguration>();
        inner = new DynamoUrlRepositorio(dynamo, config);
    }
    else
    {
        inner = new MemoriaUrlRepositorio();
    }

    var mux = sp.GetRequiredService<IConnectionMultiplexer>();
    var cfg = sp.GetRequiredService<IConfiguration>();
    return new CacheUrlRepositorio(inner, mux, cfg);
});
builder.Services.AddSingleton<IEncurtadorDeUrl, EncurtadorDeUrl>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Redireciona HTTPS apenas em Produção para facilitar uso local
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

// Endpoint de saúde
app.MapGet("/", () => Results.Ok(new { status = "ok" }))
   .WithOpenApi(op =>
   {
       op.Summary = "Verifica saúde da aplicação";
       return op;
   });

// Encurtar URL
app.MapPost("/encurtar", (HttpContext http, IEncurtadorDeUrl servico, EncurtarUrlRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req?.Url))
        return Results.BadRequest(new { erro = "Informe uma URL válida." });

    if (!Uri.TryCreate(req.Url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        return Results.BadRequest(new { erro = "URL inválida. Use http ou https." });

    var registro = servico.Encurtar(uri.ToString());

    var host = http.Request.Headers.TryGetValue("X-Forwarded-Host", out var xfHost) && !string.IsNullOrWhiteSpace(xfHost)
        ? xfHost.ToString()
        : http.Request.Host.Value;
    var proto = http.Request.Headers.TryGetValue("X-Forwarded-Proto", out var xfProto) && !string.IsNullOrWhiteSpace(xfProto)
        ? xfProto.ToString()
        : http.Request.Scheme;

    var urlEncurtada = $"{proto}://{host}/{registro.Codigo}";

    return Results.Ok(new EncurtarUrlResponse
    {
        Codigo = registro.Codigo,
        UrlOriginal = registro.UrlOriginal,
        UrlEncurtada = urlEncurtada
    });
})
.WithOpenApi(op =>
{
    op.Summary = "Gera uma URL encurtada";
    return op;
});

// Redirecionar por código
app.MapGet("/{codigo}", (string codigo, IEncurtadorDeUrl servico) =>
{
    var encontrado = servico.ObterPorCodigo(codigo);
    if (encontrado is null)
        return Results.NotFound(new { erro = "Código não encontrado." });

    servico.RegistrarAcesso(codigo);
    return Results.Redirect(encontrado.UrlOriginal, permanent: false);
})
.WithOpenApi(op =>
{
    op.Summary = "Redireciona para a URL original";
    return op;
});

// Estatísticas simples
app.MapGet("/estatisticas/{codigo}", (string codigo, IEncurtadorDeUrl servico) =>
{
    var encontrado = servico.ObterPorCodigo(codigo);
    if (encontrado is null)
        return Results.NotFound(new { erro = "Código não encontrado." });

    return Results.Ok(new
    {
        codigo = encontrado.Codigo,
        urlOriginal = encontrado.UrlOriginal,
        acessos = encontrado.Acessos,
        criadoEm = encontrado.CriadoEm
    });
})
.WithOpenApi(op =>
{
    op.Summary = "Retorna estatísticas do código";
    return op;
});

app.Run();
