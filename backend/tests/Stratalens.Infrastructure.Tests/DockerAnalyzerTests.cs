using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Enums;
using Stratalens.Infrastructure.Analyzers;

namespace Stratalens.Infrastructure.Tests;

// Tests del analyzer de Docker (T30): primer analyzer de infraestructura.
public class DockerAnalyzerTests
{
    private static readonly RepositoryReference Repo = new("carlos", "demo", "main");

    [Fact]
    public void CanAnalyze_EsTrue_ConDockerfile()
    {
        var analyzer = new DockerAnalyzer();
        var files = new List<RepositoryFile> { new("Dockerfile", "blob"), new("index.js", "blob") };

        Assert.True(analyzer.CanAnalyze(files));
    }

    [Fact]
    public void CanAnalyze_EsTrue_ConDockerComposeYml()
    {
        var analyzer = new DockerAnalyzer();
        var files = new List<RepositoryFile> { new("docker-compose.yml", "blob") };

        Assert.True(analyzer.CanAnalyze(files));
    }

    // Nunca inventar infraestructura que no está (RULES.md): sin ningún archivo Docker,
    // no hay nada que detectar.
    [Fact]
    public void CanAnalyze_EsFalse_SinArchivosDocker()
    {
        var analyzer = new DockerAnalyzer();
        var files = new List<RepositoryFile> { new("package.json", "blob"), new("src/index.js", "blob") };

        Assert.False(analyzer.CanAnalyze(files));
    }

    [Fact]
    public async Task Analyze_ConDockerfile_ProduceUnNodoDeInfraestructura()
    {
        var analyzer = new DockerAnalyzer();
        var files = new List<RepositoryFile> { new("Dockerfile", "blob") };

        var result = await analyzer.AnalyzeAsync(Guid.NewGuid(), Repo, StubConnector, files);

        var node = Assert.Single(result.Nodes);
        Assert.Equal("Docker", node.Name);
        Assert.Equal("Docker", node.Type);
        Assert.Equal(NodeCategory.Infrastructure, node.Category);
        Assert.Equal("Dockerfile", node.Metadata["source"]);
        Assert.Empty(result.Edges); // decisión T30: nodo suelto, sin edge todavía
    }

    // Con los dos archivos presentes, el Dockerfile es la evidencia preferida (más directa)
    // y se produce UN solo nodo, no uno por archivo.
    [Fact]
    public async Task Analyze_ConDockerfileYCompose_PrefiereElDockerfileYNoDuplica()
    {
        var analyzer = new DockerAnalyzer();
        var files = new List<RepositoryFile>
        {
            new("docker-compose.yml", "blob"),
            new("backend/Dockerfile", "blob")
        };

        var result = await analyzer.AnalyzeAsync(Guid.NewGuid(), Repo, StubConnector, files);

        var node = Assert.Single(result.Nodes);
        Assert.Equal("backend/Dockerfile", node.Metadata["source"]);
    }

    private static IProviderConnector StubConnector => new NotUsedConnector();

    // El analyzer no toca el connector (no necesita contenido) — este stub solo confirma eso.
    private sealed class NotUsedConnector : IProviderConnector
    {
        public Task<IReadOnlyList<RepositoryFile>> GetFileTreeAsync(RepositoryReference repo, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<string> GetFileContentAsync(RepositoryReference repo, string path, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RepositorySummary>> ListRepositoriesAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
