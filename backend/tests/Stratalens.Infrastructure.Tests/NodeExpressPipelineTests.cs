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
            new DockerAnalyzer(),
            new AuthAnalyzer(),
            new MessageBusAnalyzer(),
            new WorkersAnalyzer(),
            new CloudAnalyzer()
        };

        var useCase = new AnalyzeRepositoryUseCase(
            graphRepo, connector, analyzers, new SystemGraphBuilder());

        // --- Act ---
        var graph = await useCase.ExecuteAsync(project.Id, Repo);

        // --- Assert: 10 nodos = 8 gruesos (Frontend/Backend/PostgreSQL/Docker/JWT/RabbitMQ/
        // Workers/AWS) + 2 hijos del Backend (los controllers/routes, ParentNodeId — T34) ---
        Assert.Equal(10, graph.Nodes.Count);
        var byName = graph.Nodes.ToDictionary(n => n.Name, n => n);

        Assert.Equal(NodeCategory.Application, byName["Frontend"].Category);
        Assert.Equal("React", byName["Frontend"].Type);
        Assert.Equal(NodeCategory.Application, byName["Backend"].Category);
        Assert.Equal("Express", byName["Backend"].Type); // distinto de "AspNetCore"
        Assert.Null(byName["Backend"].ParentNodeId);      // el Backend es raíz
        Assert.Equal(NodeCategory.Database, byName["PostgreSQL"].Category);
        Assert.Equal(NodeCategory.Infrastructure, byName["Docker"].Category);
        Assert.Equal("Dockerfile", byName["Docker"].Metadata["source"]);
        Assert.Equal(NodeCategory.Security, byName["JWT"].Category); // T32
        Assert.Equal("package.json", byName["JWT"].Metadata["source"]);
        Assert.Equal(NodeCategory.Infrastructure, byName["RabbitMQ"].Category); // T36
        Assert.Equal("MessageBus", byName["RabbitMQ"].Type);
        Assert.Equal("package.json", byName["RabbitMQ"].Metadata["source"]);
        Assert.Equal(NodeCategory.Worker, byName["Workers"].Category); // T37
        Assert.Equal("BullMQ", byName["Workers"].Metadata["framework"]);
        Assert.Equal(NodeCategory.Deployment, byName["AWS"].Category); // T38
        Assert.Equal("Cloud", byName["AWS"].Type);

        // Los 2 hijos del Backend: report.controller y report.routes, ambos con
        // ParentNodeId = Backend.Id y Category=Code (T34). El marcador de DB (PgPool) NO
        // aparece como hijo (se volvió el nodo PostgreSQL).
        var backendId = byName["Backend"].Id;
        var children = graph.Nodes.Where(n => n.ParentNodeId == backendId).ToList();
        Assert.Equal(2, children.Count);
        Assert.All(children, c => Assert.Equal(NodeCategory.Code, c.Category));
        Assert.Contains(children, c => c.Name == "report.controller");
        Assert.Contains(children, c => c.Name == "report.routes");
        Assert.DoesNotContain(graph.Nodes, n => n.Type == "PgPool");

        // --- Assert: edges de alto nivel, mismas confidences que el resto del sistema ---
        Assert.Equal(7, graph.Edges.Count); // +1 Backend→Cloud (T38)
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

        // Security → Backend ("validates"): la seguridad valida las requests hacia el backend
        // (narrativa Archify "Validate Token"), no una dependencia de librería.
        var securityToBackend = graph.Edges.Single(e => e.Type == "validates");
        Assert.Equal("JWT", nameById[securityToBackend.SourceNodeId]);
        Assert.Equal("Backend", nameById[securityToBackend.TargetNodeId]);
        Assert.Equal("package.json", securityToBackend.Source);
        Assert.Equal(90, securityToBackend.Confidence);

        // Backend → MessageBus ("messaging", T36): el backend usa el bus (neutral, no afirma
        // publica/consume). RabbitMQ detectado por la dependencia amqplib del package.json.
        var backToBus = graph.Edges.Single(e => e.Type == "messaging");
        Assert.Equal("Backend", nameById[backToBus.SourceNodeId]);
        Assert.Equal("RabbitMQ", nameById[backToBus.TargetNodeId]);
        Assert.Equal("package.json", backToBus.Source);
        Assert.Equal(90, backToBus.Confidence);

        // Backend → Workers ("background jobs", T37): BullMQ detectado por la dependencia.
        var backToWorkers = graph.Edges.Single(e => e.Type == "background jobs");
        Assert.Equal("Backend", nameById[backToWorkers.SourceNodeId]);
        Assert.Equal("Workers", nameById[backToWorkers.TargetNodeId]);
        Assert.Equal("package.json", backToWorkers.Source);
        Assert.Equal(90, backToWorkers.Confidence);

        // Backend → Cloud ("cloud services", T38): AWS detectado por @aws-sdk/client-s3.
        var backToCloud = graph.Edges.Single(e => e.Type == "cloud services");
        Assert.Equal("Backend", nameById[backToCloud.SourceNodeId]);
        Assert.Equal("AWS", nameById[backToCloud.TargetNodeId]);
        Assert.Equal("package.json", backToCloud.Source);
        Assert.Equal(90, backToCloud.Confidence);
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
        ["package.json"] = """{ "name": "demo", "dependencies": { "express": "5.2.1", "pg": "8.20.0", "jsonwebtoken": "9.0.2", "amqplib": "0.10.4", "bullmq": "5.7.0", "@aws-sdk/client-s3": "3.600.0" } }""",
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
