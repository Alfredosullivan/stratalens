using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Application.Services;
using Stratalens.Application.UseCases;
using Stratalens.Domain.Entities;
using Stratalens.Domain.Enums;
using Stratalens.Infrastructure.Analyzers;
using Stratalens.Infrastructure.Tests.TestDoubles;

namespace Stratalens.Infrastructure.Tests;

// Pipeline end-to-end con la forma EXACTA del bug reportado por Carlos: monorepo con
// backend Express+pg en la raíz y frontend React en JSX puro (sin TypeScript) en client/
// — mismo layout que airbnb-finance-assistant. Antes de este fix, ningún analyzer
// reconocía nada de esto y el grafo quedaba vacío.
public class NodeExpressPipelineTests
{
    private static readonly RepositoryReference Repo = new("carlos", "airbnb-finance-assistant", "main");

    [Fact]
    public async Task Pipeline_ConBackendExpressYFrontendJsx_ProduceGrafoGrueso()
    {
        // --- Arrange ---
        var connector = new StubConnector(Fixture);
        var graphRepo = new InMemoryGraphRepository();

        var project = Project.CreateManual(Guid.NewGuid(), "airbnb-finance-assistant");
        await graphRepo.AddProjectAsync(project);

        var analyzers = new ILanguageAnalyzer[]
        {
            new NodeExpressAnalyzer(),
            new ReactTypeScriptAnalyzer(new FakeRunner(FrontendAnalysisJson)),
            new DockerAnalyzer()
        };

        var useCase = new AnalyzeRepositoryUseCase(
            graphRepo, connector, analyzers, new SystemGraphBuilder());

        // --- Act ---
        var graph = await useCase.ExecuteAsync(project.Id, Repo);

        // --- Assert: 4 nodos gruesos (Frontend/Backend/PostgreSQL + Docker, T30) ---
        Assert.Equal(4, graph.Nodes.Count);
        var byName = graph.Nodes.ToDictionary(n => n.Name, n => n);

        Assert.Equal(NodeCategory.Application, byName["Frontend"].Category);
        Assert.Equal("React", byName["Frontend"].Type);
        Assert.Equal(NodeCategory.Application, byName["Backend"].Category);
        Assert.Equal("Express", byName["Backend"].Type); // distinto de "AspNetCore"
        Assert.Equal(NodeCategory.Database, byName["PostgreSQL"].Category);
        Assert.Equal(NodeCategory.Infrastructure, byName["Docker"].Category);
        Assert.Equal("Dockerfile", byName["Docker"].Metadata["source"]);

        // --- Assert: edges de alto nivel, mismas confidences que el resto del sistema ---
        Assert.Equal(3, graph.Edges.Count); // +1 Backend→Docker
        var nameById = graph.Nodes.ToDictionary(n => n.Id, n => n.Name);

        var frontToBack = graph.Edges.Single(e => e.Type == "HTTP/REST");
        Assert.Equal("Frontend", nameById[frontToBack.SourceNodeId]);
        Assert.Equal("Backend", nameById[frontToBack.TargetNodeId]);

        var backToDb = graph.Edges.Single(e => e.Type == "SQL");
        Assert.Equal("Backend", nameById[backToDb.SourceNodeId]);
        Assert.Equal("PostgreSQL", nameById[backToDb.TargetNodeId]);

        var backToDocker = graph.Edges.Single(e => e.Type == "containerized");
        Assert.Equal("Backend", nameById[backToDocker.SourceNodeId]);
        Assert.Equal("Docker", nameById[backToDocker.TargetNodeId]);
        Assert.Equal("Dockerfile", backToDocker.Source);
    }

    // El frontend JSX no pasa por el subproceso Node real en este test (ya probado en
    // ReactTypeScriptAnalyzerTests) — el FakeRunner simula lo que devolvería.
    private const string FrontendAnalysisJson = """
        {
          "components": [
            { "name": "Dashboard", "kind": "page", "file": "client/src/pages/Dashboard.jsx" },
            { "name": "reportService", "kind": "service", "file": "client/src/services/reportService.js" }
          ],
          "imports": [
            { "fromFile": "client/src/pages/Dashboard.jsx", "toFile": "client/src/services/reportService.js" }
          ],
          "apiCalls": [
            { "fromFile": "client/src/services/reportService.js", "method": "GET", "url": "/api/reports" }
          ]
        }
        """;

    // Monorepo real: backend Express+pg en la raíz, frontend React JSX en client/, CADA
    // UNO con su propio package.json — misma forma que airbnb-finance-assistant.
    private static readonly Dictionary<string, string> Fixture = new()
    {
        ["package.json"] = """{ "name": "demo", "dependencies": { "express": "5.2.1", "pg": "8.20.0" } }""",
        ["src/controllers/report.controller.js"] = "// controller",
        ["src/routes/report.routes.js"] = "// routes",
        ["Dockerfile"] = "FROM node:22-alpine",

        ["client/package.json"] = """{ "name": "client", "dependencies": { "react": "19.0.0" } }""",
        ["client/src/pages/Dashboard.jsx"] = "export function Dashboard() { return null; }",
        ["client/src/services/reportService.js"] = "export async function getReports() {}"
    };

    // --- Stubs de infraestructura ---

    private sealed class FakeRunner : INodeAnalyzerRunner
    {
        private readonly string _json;
        public FakeRunner(string json) => _json = json;
        public Task<string> RunAsync(IReadOnlyList<NodeAnalyzerFile> files, CancellationToken ct = default) =>
            Task.FromResult(_json);
    }

    private sealed class StubConnector : IProviderConnector
    {
        private readonly IReadOnlyDictionary<string, string> _files;
        public StubConnector(IReadOnlyDictionary<string, string> files) => _files = files;

        public Task<IReadOnlyList<RepositoryFile>> GetFileTreeAsync(RepositoryReference repo, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RepositoryFile>>(
                _files.Keys.Select(p => new RepositoryFile(p, "blob")).ToList());

        public Task<string> GetFileContentAsync(RepositoryReference repo, string path, CancellationToken ct = default) =>
            Task.FromResult(_files[path]);

        public Task<IReadOnlyList<RepositorySummary>> ListRepositoriesAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
