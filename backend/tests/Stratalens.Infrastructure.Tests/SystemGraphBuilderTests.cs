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

    [Fact]
    public void Build_ConBackendYNodosFinos_CuelgaControllersDelBackendYExcluyeElMarcadorDeDb()
    {
        // T34: los Controllers/Services detectados se cuelgan del Backend (ParentNodeId), pero
        // el marcador de DB (DbContext) NO es hijo — es evidencia del nodo PostgreSQL.
        var projectId = Guid.NewGuid();
        var controller = new Node(projectId, "OrdersController", "Controller", NodeCategory.Code,
            new Dictionary<string, string> { ["source"] = "Controllers/OrdersController.cs" });
        var dbContext = new Node(projectId, "AppDbContext", "DbContext", NodeCategory.Code,
            new Dictionary<string, string> { ["source"] = "Data/AppDbContext.cs" });

        var resultsByLanguage = new Dictionary<string, AnalysisResult>
        {
            ["csharp"] = new AnalysisResult(new List<Node> { controller, dbContext }, new List<Edge>())
        };

        var graph = new SystemGraphBuilder().Build(projectId, new List<RepositoryFile>(), resultsByLanguage);

        var backend = Assert.Single(graph.Nodes, n => n.Name == "Backend");
        Assert.Null(backend.ParentNodeId); // el Backend es raíz

        // El Controller cuelga del Backend; el DbContext no aparece como hijo (se volvió PostgreSQL).
        var child = Assert.Single(graph.Nodes, n => n.Type == "Controller");
        Assert.Equal("OrdersController", child.Name);
        Assert.Equal(backend.Id, child.ParentNodeId);
        Assert.DoesNotContain(graph.Nodes, n => n.Type == "DbContext");
        Assert.Contains(graph.Nodes, n => n.Name == "PostgreSQL");
    }

    [Fact]
    public void Build_ConCloudYSinBackendDetectado_DejaElNodoSueltoSinEdge()
    {
        // Mismo principio: sin Backend no hay de quién decir "usa este cloud".
        var projectId = Guid.NewGuid();
        var cloudNode = new Node(projectId, "AWS", "Cloud", NodeCategory.Deployment,
            new Dictionary<string, string> { ["source"] = "package.json" });

        var resultsByLanguage = new Dictionary<string, AnalysisResult>
        {
            ["cloud"] = new AnalysisResult(new List<Node> { cloudNode }, new List<Edge>())
        };

        var graph = new SystemGraphBuilder().Build(projectId, new List<RepositoryFile>(), resultsByLanguage);

        var node = Assert.Single(graph.Nodes);
        Assert.Equal("AWS", node.Name);
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public void Build_ConWorkersYSinBackendDetectado_DejaElNodoSueltoSinEdge()
    {
        // Mismo principio: sin Backend no hay de quién decir "despacha jobs a estos workers".
        var projectId = Guid.NewGuid();
        var workersNode = new Node(projectId, "Workers", "Workers", NodeCategory.Worker,
            new Dictionary<string, string> { ["source"] = "package.json", ["framework"] = "BullMQ" });

        var resultsByLanguage = new Dictionary<string, AnalysisResult>
        {
            ["workers"] = new AnalysisResult(new List<Node> { workersNode }, new List<Edge>())
        };

        var graph = new SystemGraphBuilder().Build(projectId, new List<RepositoryFile>(), resultsByLanguage);

        var node = Assert.Single(graph.Nodes);
        Assert.Equal("Workers", node.Name);
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public void Build_ConMessageBusYSinBackendDetectado_DejaElNodoSueltoSinEdge()
    {
        // Mismo principio que Docker/Security: sin Backend no hay de quién decir "usa este bus".
        var projectId = Guid.NewGuid();
        var busNode = new Node(projectId, "RabbitMQ", "MessageBus", NodeCategory.Infrastructure,
            new Dictionary<string, string> { ["source"] = "package.json" });

        var resultsByLanguage = new Dictionary<string, AnalysisResult>
        {
            ["messagebus"] = new AnalysisResult(new List<Node> { busNode }, new List<Edge>())
        };

        var graph = new SystemGraphBuilder().Build(projectId, new List<RepositoryFile>(), resultsByLanguage);

        var node = Assert.Single(graph.Nodes);
        Assert.Equal("RabbitMQ", node.Name);
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public void Build_ConSecurityYSinBackendDetectado_DejaElNodoSueltoSinEdge()
    {
        // Mismo principio que Docker: sin Backend no hay de quién decir "esto está protegido
        // por JWT" — el nodo Security aparece suelto, sin edge inventado (RULES.md).
        var projectId = Guid.NewGuid();
        var securityNode = new Node(projectId, "JWT", "JWT", NodeCategory.Security,
            new Dictionary<string, string> { ["source"] = "package.json" });

        var resultsByLanguage = new Dictionary<string, AnalysisResult>
        {
            ["auth"] = new AnalysisResult(new List<Node> { securityNode }, new List<Edge>())
        };

        var graph = new SystemGraphBuilder().Build(projectId, new List<RepositoryFile>(), resultsByLanguage);

        var node = Assert.Single(graph.Nodes);
        Assert.Equal("JWT", node.Name);
        Assert.Empty(graph.Edges);
    }
}
