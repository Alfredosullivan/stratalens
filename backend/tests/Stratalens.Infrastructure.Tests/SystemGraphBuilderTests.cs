using Stratalens.Application.Models;
using Stratalens.Application.Services;
using Stratalens.Domain.Entities;
using Stratalens.Domain.Enums;

namespace Stratalens.Infrastructure.Tests;

// SystemGraphBuilder es una función pura (sin I/O) — se prueba directo, sin pipeline
// completo, para el caso puntual del edge Backend→Docker (T30).
public class SystemGraphBuilderTests
{
    [Fact]
    public void Build_ConDockerYSinBackendDetectado_DejaElNodoSueltoSinEdge()
    {
        // Nunca inventar el otro extremo de un edge (RULES.md): sin Backend, no hay de
        // quién decir "esto se containeriza".
        var projectId = Guid.NewGuid();
        var dockerNode = new Node(projectId, "Docker", "Docker", NodeCategory.Infrastructure,
            new Dictionary<string, string> { ["source"] = "Dockerfile" });

        var resultsByLanguage = new Dictionary<string, AnalysisResult>
        {
            ["docker"] = new AnalysisResult(new List<Node> { dockerNode }, new List<Edge>())
        };

        var graph = new SystemGraphBuilder().Build(projectId, new List<RepositoryFile>(), resultsByLanguage);

        var node = Assert.Single(graph.Nodes);
        Assert.Equal("Docker", node.Name);
        Assert.Empty(graph.Edges);
    }
}
