using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Enums;
using Stratalens.Infrastructure.Analyzers;

namespace Stratalens.Infrastructure.Tests;

// Tests del analyzer de message bus (T36): detecta un broker por dependencia real y MAPEA la
// librería a su broker concreto (amqplib→RabbitMQ, kafkajs→Kafka...). Construido con fixtures
// en memoria — no hace falta un repo real con mensajería para verificar la lógica.
public class MessageBusAnalyzerTests
{
    private static readonly RepositoryReference Repo = new("carlos", "demo", "main");

    [Fact]
    public void CanAnalyze_EsTrue_ConManifiesto()
    {
        var analyzer = new MessageBusAnalyzer();

        Assert.True(analyzer.CanAnalyze(new List<RepositoryFile> { new("package.json", "blob") }));
        Assert.True(analyzer.CanAnalyze(new List<RepositoryFile> { new("src/Api/Api.csproj", "blob") }));
    }

    [Fact]
    public void CanAnalyze_EsFalse_SinManifiesto()
    {
        var analyzer = new MessageBusAnalyzer();
        var files = new List<RepositoryFile> { new("src/index.js", "blob"), new("README.md", "blob") };

        Assert.False(analyzer.CanAnalyze(files));
    }

    [Fact]
    public async Task Analyze_ConAmqplib_DetectaRabbitMq()
    {
        var fixture = new Dictionary<string, string>
        {
            ["package.json"] = """{ "name": "demo", "dependencies": { "express": "5.2.1", "amqplib": "0.10.4" } }"""
        };
        var result = await Analyze(fixture);

        var node = Assert.Single(result.Nodes);
        Assert.Equal("RabbitMQ", node.Name);       // broker concreto, no "Message Bus" genérico
        Assert.Equal("MessageBus", node.Type);
        Assert.Equal(NodeCategory.Infrastructure, node.Category);
        Assert.Equal("package.json", node.Metadata["source"]);
        Assert.Empty(result.Edges); // el edge Backend→MessageBus lo crea SystemGraphBuilder
    }

    [Fact]
    public async Task Analyze_ConKafkajs_DetectaKafka()
    {
        var fixture = new Dictionary<string, string>
        {
            ["package.json"] = """{ "name": "demo", "dependencies": { "kafkajs": "2.2.4" } }"""
        };
        var result = await Analyze(fixture);

        Assert.Equal("Kafka", Assert.Single(result.Nodes).Name);
    }

    // .NET: broker concreto (Confluent.Kafka) en un .csproj (substring, sin parsear XML).
    [Fact]
    public async Task Analyze_ConConfluentKafkaEnCsproj_DetectaKafka()
    {
        var fixture = new Dictionary<string, string>
        {
            ["src/Api/Api.csproj"] = """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <ItemGroup>
                    <PackageReference Include="Confluent.Kafka" Version="2.5.0" />
                  </ItemGroup>
                </Project>
                """
        };
        var result = await Analyze(fixture);

        var node = Assert.Single(result.Nodes);
        Assert.Equal("Kafka", node.Name);
        Assert.Equal("src/Api/Api.csproj", node.Metadata["source"]);
    }

    // MassTransit abstrae el broker → nombre genérico "Message Bus" (no un broker concreto).
    [Fact]
    public async Task Analyze_ConMassTransit_UsaNombreGenerico()
    {
        var fixture = new Dictionary<string, string>
        {
            ["src/Api/Api.csproj"] = """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <ItemGroup><PackageReference Include="MassTransit" Version="8.2.0" /></ItemGroup>
                </Project>
                """
        };
        var result = await Analyze(fixture);

        Assert.Equal("Message Bus", Assert.Single(result.Nodes).Name);
    }

    // Nunca inventar (RULES.md): sin dependencia de bus, no hay nodo.
    [Fact]
    public async Task Analyze_SinDependenciaDeBus_NoProduceNada()
    {
        var fixture = new Dictionary<string, string>
        {
            ["package.json"] = """{ "name": "demo", "dependencies": { "express": "5.2.1", "pg": "8.20.0" } }"""
        };
        var result = await Analyze(fixture);

        Assert.Empty(result.Nodes);
        Assert.Empty(result.Edges);
    }

    // Monorepo: el backend (raíz) tiene el broker, el frontend no → detecta igual y atribuye
    // el Source al manifiesto correcto.
    [Fact]
    public async Task Analyze_EnMonorepo_AtribuyeElSourceAlManifiestoConLaDependencia()
    {
        var fixture = new Dictionary<string, string>
        {
            ["package.json"] = """{ "name": "demo", "dependencies": { "amqplib": "0.10.4" } }""",
            ["client/package.json"] = """{ "name": "client", "dependencies": { "react": "19.0.0" } }"""
        };
        var result = await Analyze(fixture);

        Assert.Equal("package.json", Assert.Single(result.Nodes).Metadata["source"]);
    }

    private static async Task<AnalysisResult> Analyze(Dictionary<string, string> fixture)
    {
        var analyzer = new MessageBusAnalyzer();
        var connector = new StubConnector(fixture);
        var files = fixture.Keys.Select(p => new RepositoryFile(p, "blob")).ToList();
        return await analyzer.AnalyzeAsync(Guid.NewGuid(), Repo, connector, files);
    }

    private sealed class StubConnector : IProviderConnector
    {
        private readonly IReadOnlyDictionary<string, string> _files;
        public StubConnector(IReadOnlyDictionary<string, string> files) => _files = files;

        public Task<string> GetFileContentAsync(RepositoryReference repo, string path, CancellationToken ct = default) =>
            Task.FromResult(_files[path]);

        public Task<IReadOnlyList<RepositoryFile>> GetFileTreeAsync(RepositoryReference repo, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RepositorySummary>> ListRepositoriesAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
