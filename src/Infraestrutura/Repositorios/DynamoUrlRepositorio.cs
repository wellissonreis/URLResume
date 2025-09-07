using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using URLResume.Dominio.Entidades;

namespace URLResume.Infraestrutura.Repositorios;

public class DynamoUrlRepositorio : IUrlRepositorio
{
    private readonly IAmazonDynamoDB _dynamo;
    private readonly string _tabela;

    // Mapeamento para a tabela com chave composta: PK = "id", SK = "urls"
    private const string PkName = "id";
    private const string SkName = "urls";
    private const string SkFixedValue = "details"; // valor fixo para a SK

    public DynamoUrlRepositorio(IAmazonDynamoDB dynamo, IConfiguration config)
    {
        _dynamo = dynamo;
        _tabela = config["DynamoDB:TableName"] ?? "UrlResumeUrls";
    }

    public UrlRegistro Salvar(UrlRegistro registro)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            [PkName] = new AttributeValue { S = registro.Codigo },
            [SkName] = new AttributeValue { S = SkFixedValue },
            ["Codigo"] = new AttributeValue { S = registro.Codigo },
            ["UrlOriginal"] = new AttributeValue { S = registro.UrlOriginal },
            ["CriadoEm"] = new AttributeValue { S = registro.CriadoEm.ToString("o") },
            ["Acessos"] = new AttributeValue { N = registro.Acessos.ToString() }
        };

        _ = _dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = _tabela,
            Item = item,
            ConditionExpression = "attribute_not_exists(#pk) AND attribute_not_exists(#sk)",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#pk"] = PkName,
                ["#sk"] = SkName
            }
        }).GetAwaiter().GetResult();

        return registro;
    }

    public UrlRegistro? Obter(string codigo)
    {
        var resposta = _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _tabela,
            Key = new Dictionary<string, AttributeValue>
            {
                [PkName] = new AttributeValue { S = codigo },
                [SkName] = new AttributeValue { S = SkFixedValue }
            },
            ConsistentRead = true
        }).GetAwaiter().GetResult();

        if (resposta.Item == null || resposta.Item.Count == 0)
            return null;

        var item = resposta.Item;
        var url = item.TryGetValue("UrlOriginal", out var u) ? u.S : string.Empty;
        var criado = item.TryGetValue("CriadoEm", out var c) && DateTime.TryParse(c.S, out var dt) ? dt : DateTime.UtcNow;
        var acessos = item.TryGetValue("Acessos", out var a) && long.TryParse(a.N, out var n) ? n : 0;
        return new UrlRegistro(codigo, url, criado, acessos);
    }

    public void Atualizar(UrlRegistro registro)
    {
        _ = _dynamo.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tabela,
            Key = new Dictionary<string, AttributeValue>
            {
                [PkName] = new AttributeValue { S = registro.Codigo },
                [SkName] = new AttributeValue { S = SkFixedValue }
            },
            UpdateExpression = "SET Acessos = :acessos",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":acessos"] = new AttributeValue { N = registro.Acessos.ToString() }
            }
        }).GetAwaiter().GetResult();
    }
}
