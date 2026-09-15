using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Enums;
using Stratalens.Infrastructure.Analyzers;

namespace Stratalens.Infrastructure.Tests;

// Tests del analyzer de workers (T37): detecta procesamiento en background por dependencia
// real de una librería DEDICADA de jobs. El nodo se llama siempre "Workers"; la librería
// concreta va en metadata. Construido con fixtures — sin repo real.
public class WorkersAnalyzerTests
{
    private static readonly RepositoryReference Repo = new("carlos", "demo", "main");

    [Fact]
    public void CanAnalyze_EsTrue_ConManifiesto()
    {
        var analyzer = new WorkersAnalyzer();

        Assert.True(analyzer.CanAnalyze(new List<RepositoryFile> { new("package.json", "blob") }));
        Assert.True(analyzer.CanAnalyze(new List<RepositoryFile> { new("src/Api/Api.csproj", "blob") }));
    }

    [Fact]
    public void CanAnalyze_EsFalse_SinManifiesto()
    {
        var analyzer = new WorkersAnalyzer();
        Assert.False(analyzer.CanAnalyze(new List<RepositoryFile> { new("src/index.js", "blob") }));
    }

    [Fact]
    public async Task Analyze_ConBullmq_ProduceNodoWorkersConFrameworkEnMetadata()
    {
        var fixture = new Dictionary<string, string>
        {
            ["package.json"] = """{ "name": "demo", "dependencies": { "express": "5.2.1", "bullmq": "5.7.0" } }"""
        };
        var result = await Analyze(fixture);

        var node = Assert.Single(result.Nodes);
        Assert.Equal("Workers", node.Name);   // nombre genérico, no "BullMQ"
        Assert.Equal("Workers", node.Type);
        Assert.Equal(NodeCategory.Worker, node.Category);
        Assert.Equal("package.json", node.Metadata["source"]);
        Assert.Equal("BullMQ", node.Metadata["framework"]); // la lib concreta va en metadata
        Assert.Empty(result.Edges); // el edge Backend→Workers lo crea SystemGraphBuilder
    }

    // .NET: Hangfire en un .csproj (substring).
    [Fact]
    public async Task Analyze_ConHangfireEnCsproj_DetectaHangfire()
    {
        var fixture = new Dictionary<string, string>
        {
            ["src/Api/Api.csproj"] = """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <ItemGroup><PackageReference Include="Hangfire" Version="1.8.14" /></ItemGroup>
                </Project>
                """
        };
        var result = await Analyze(fixture);

        var node = Assert.Single(result.Nodes);
        Assert.Equal("Workers", node.Name);
        Assert.Equal("Hangfire", node.Metadata["framework"]);
        Assert.Equal("src/Api/Api.csproj", node.Metadata["source"]);
    }

    // Nunca inventar (RULES.md): sin librería dedicada de jobs, no hay nodo. Un cron trivial
    // (node-cron) NO cuenta — está deliberadamente fuera de la lista por falso positivo alto.
    [Fact]
    public async Task Analyze_SinLibreriaDeWorkers_NoProduceNada()
    {
        var fixture = new Dictionary<string, string>
        {
            ["package.json"] = """{ "name": "demo", "dependencies": { "express": "5.2.1", "node-cron": "3.0.3" } }"""
        };
        var result = await Analyze(fixture);

        Assert.Empty(result.Nodes);
        Assert.Empty(result.Edges);
    }

    [Fact]
    public async Task Analyze_EnMonorepo_AtribuyeElSourceAlManifiestoConLaDependencia()
    {
        var fixture = new Dictionary<string, string>
        {
            ["package.json"] = """{ "name": "demo", "dependencies": { "agenda": "5.0.0" } }""",
            ["client/package.json"] = """{ "name": "client", "dependencies": { "react": "19.0.0" } }"""
        };
        var result = await Analyze(fixture);

        var node = Assert.Single(result.Nodes);
        Assert.Equal("package.json", node.Metadata["source"]);
        Assert.Equal("Agenda", node.Metadata["framework"]);
    }

    private static async Task<AnalysisResult> Analyze(Dictionary<string, string> fixture)
    {
        var analyzer = new WorkersAnalyzer();
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
