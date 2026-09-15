using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Enums;
using Stratalens.Infrastructure.Analyzers;

namespace Stratalens.Infrastructure.Tests;

// Tests del analyzer de cloud (T38): detecta el proveedor por el SDK del manifiesto. Clave:
// los SDK npm de cloud son paquetes con PREFIJO (@aws-sdk/client-s3), no coincidencia exacta.
public class CloudAnalyzerTests
{
    private static readonly RepositoryReference Repo = new("carlos", "demo", "main");

    [Fact]
    public void CanAnalyze_EsTrue_ConManifiesto()
    {
        var analyzer = new CloudAnalyzer();
        Assert.True(analyzer.CanAnalyze(new List<RepositoryFile> { new("package.json", "blob") }));
        Assert.True(analyzer.CanAnalyze(new List<RepositoryFile> { new("src/Api/Api.csproj", "blob") }));
    }

    [Fact]
    public void CanAnalyze_EsFalse_SinManifiesto()
    {
        var analyzer = new CloudAnalyzer();
        Assert.False(analyzer.CanAnalyze(new List<RepositoryFile> { new("src/index.js", "blob") }));
    }

    // El caso importante: SDK modular de AWS como paquete con prefijo (@aws-sdk/client-s3).
    [Fact]
    public async Task Analyze_ConAwsSdkModular_DetectaAws()
    {
        var fixture = new Dictionary<string, string>
        {
            ["package.json"] = """{ "name": "demo", "dependencies": { "express": "5.2.1", "@aws-sdk/client-s3": "3.600.0" } }"""
        };
        var result = await Analyze(fixture);

        var node = Assert.Single(result.Nodes);
        Assert.Equal("AWS", node.Name);
        Assert.Equal("Cloud", node.Type);
        Assert.Equal(NodeCategory.Deployment, node.Category);
        Assert.Equal("package.json", node.Metadata["source"]);
        Assert.Empty(result.Edges); // el edge Backend→Cloud lo crea SystemGraphBuilder
    }

    // aws-sdk v2 (paquete de nombre exacto, no scoped) también cuenta.
    [Fact]
    public async Task Analyze_ConAwsSdkV2_DetectaAws()
    {
        var fixture = new Dictionary<string, string>
        {
            ["package.json"] = """{ "name": "demo", "dependencies": { "aws-sdk": "2.1600.0" } }"""
        };
        var result = await Analyze(fixture);

        Assert.Equal("AWS", Assert.Single(result.Nodes).Name);
    }

    [Fact]
    public async Task Analyze_ConGoogleCloud_DetectaGcp()
    {
        var fixture = new Dictionary<string, string>
        {
            ["package.json"] = """{ "name": "demo", "dependencies": { "@google-cloud/storage": "7.11.0" } }"""
        };
        var result = await Analyze(fixture);

        Assert.Equal("GCP", Assert.Single(result.Nodes).Name);
    }

    // .NET: prefijo de paquete NuGet en un .csproj (substring).
    [Fact]
    public async Task Analyze_ConAzureSdkEnCsproj_DetectaAzure()
    {
        var fixture = new Dictionary<string, string>
        {
            ["src/Api/Api.csproj"] = """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <ItemGroup><PackageReference Include="Azure.Storage.Blobs" Version="12.20.0" /></ItemGroup>
                </Project>
                """
        };
        var result = await Analyze(fixture);

        var node = Assert.Single(result.Nodes);
        Assert.Equal("Azure", node.Name);
        Assert.Equal("src/Api/Api.csproj", node.Metadata["source"]);
    }

    // Nunca inventar (RULES.md): sin SDK de cloud, no hay nodo.
    [Fact]
    public async Task Analyze_SinSdkDeCloud_NoProduceNada()
    {
        var fixture = new Dictionary<string, string>
        {
            ["package.json"] = """{ "name": "demo", "dependencies": { "express": "5.2.1", "pg": "8.20.0" } }"""
        };
        var result = await Analyze(fixture);

        Assert.Empty(result.Nodes);
        Assert.Empty(result.Edges);
    }

    private static async Task<AnalysisResult> Analyze(Dictionary<string, string> fixture)
    {
        var analyzer = new CloudAnalyzer();
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
