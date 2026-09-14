using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Application.Services;
using Stratalens.Application.UseCases;
using Stratalens.Domain.Entities;
using Stratalens.Domain.Enums;
using Stratalens.Infrastructure.Analyzers;
using Stratalens.Infrastructure.Tests.TestDoubles;

namespace Stratalens.Infrastructure.Tests;

// Test del pipeline completo (T10): corre AnalyzeRepositoryUseCase con los analyzers
// REALES (Roslyn para C#, mapeo real para React con un runner falso) contra un repo
// combinado (React + ASP.NET Core + Postgres) y verifica que el grafo PERSISTIDO es el
// grueso esperado: Frontend → Backend → PostgreSQL.
//
// Es determinista y sin dependencias externas: el connector sirve un fixture en memoria
// y el subproceso Node se sustituye por su salida JSON (la integración real de Node ya se
// probó en ReactTypeScriptAnalyzerTests).
public class AnalyzeRepositoryPipelineTests
{
    private static readonly RepositoryReference Repo = new("carlos", "demo", "main");

    [Fact]
    public async Task Pipeline_ProduceGrafoGrueso_FrontendBackendPostgreSQL()
    {
        // --- Arrange ---
        var connector = new StubConnector(Fixture);
        var graphRepo = new InMemoryGraphRepository();

        var project = Project.CreateManual(Guid.NewGuid(), "Demo");
        await graphRepo.AddProjectAsync(project);

        var analyzers = new ILanguageAnalyzer[]
        {
            new CSharpAnalyzer(),
            new ReactTypeScriptAnalyzer(new FakeRunner(ReactAnalysisJson))
        };

        var useCase = new AnalyzeRepositoryUseCase(
            graphRepo, connector, analyzers, new SystemGraphBuilder());

        // --- Act ---
        var graph = await useCase.ExecuteAsync(project.Id, Repo);

        // --- Assert: 3 nodos gruesos ---
        Assert.Equal(3, graph.Nodes.Count);
        var byName = graph.Nodes.ToDictionary(n => n.Name, n => n);

        Assert.Equal(NodeCategory.Application, byName["Frontend"].Category);
        Assert.Equal("React", byName["Frontend"].Type);
        Assert.Equal(NodeCategory.Application, byName["Backend"].Category);
        Assert.Equal("AspNetCore", byName["Backend"].Type);
        Assert.Equal(NodeCategory.Database, byName["PostgreSQL"].Category);

        // --- Assert: 2 edges de alto nivel con su evidencia ---
        Assert.Equal(2, graph.Edges.Count);
        var typeById = graph.Nodes.ToDictionary(n => n.Id, n => n.Name);

        var frontToBack = graph.Edges.Single(e => e.Type == "HTTP/REST");
        Assert.Equal("Frontend", typeById[frontToBack.SourceNodeId]);
        Assert.Equal("Backend", typeById[frontToBack.TargetNodeId]);
        Assert.Equal(80, frontToBack.Confidence);
        Assert.Equal(EdgeSourceType.Static, frontToBack.SourceType);

        var backToDb = graph.Edges.Single(e => e.Type == "SQL");
        Assert.Equal("Backend", typeById[backToDb.SourceNodeId]);
        Assert.Equal("PostgreSQL", typeById[backToDb.TargetNodeId]);
        Assert.Equal(85, backToDb.Confidence);

        // Toda relación estática lleva Source no vacío (regla del modelo de grafo).
        Assert.All(graph.Edges, e => Assert.False(string.IsNullOrWhiteSpace(e.Source)));
    }

    // Salida JSON que "produciría" el subproceso Node para el fixture de frontend.
    private const string ReactAnalysisJson = """
        {
          "components": [
            { "name": "ProductsPage", "kind": "page", "file": "frontend/src/pages/ProductsPage.tsx" },
            { "name": "productService", "kind": "service", "file": "frontend/src/services/productService.ts" }
          ],
          "imports": [
            { "fromFile": "frontend/src/pages/ProductsPage.tsx", "toFile": "frontend/src/services/productService.ts" }
          ],
          "apiCalls": [
            { "fromFile": "frontend/src/services/productService.ts", "method": "GET", "url": "/api/products" }
          ]
        }
        """;

    // Fixture: repo combinado React (frontend/) + ASP.NET Core (backend/).
    private static readonly Dictionary<string, string> Fixture = new()
    {
        ["frontend/package.json"] = """{ "name": "demo", "dependencies": { "react": "19.0.0" } }""",
        ["frontend/src/pages/ProductsPage.tsx"] = "export function ProductsPage() { return null; }",
        ["frontend/src/services/productService.ts"] = "export async function getProducts() {}",

        ["backend/Demo.Api.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>",
        ["backend/Controllers/ProductsController.cs"] = """
            using Microsoft.AspNetCore.Mvc;
            namespace Demo.Api;
            [ApiController]
            public class ProductsController : ControllerBase
            {
                public ProductsController(IProductService service) { }
            }
            """,
        ["backend/Application/ProductService.cs"] = """
            namespace Demo.Application;
            public interface IProductService { }
            public class ProductService : IProductService
            {
                public ProductService(IProductRepository repository) { }
            }
            """,
        ["backend/Infrastructure/ProductRepository.cs"] = """
            namespace Demo.Infrastructure;
            public interface IProductRepository { }
            public class ProductRepository : IProductRepository
            {
                public ProductRepository(AppDbContext db) { }
            }
            """,
        ["backend/Infrastructure/AppDbContext.cs"] = """
            using Microsoft.EntityFrameworkCore;
            namespace Demo.Infrastructure;
            public class AppDbContext : DbContext { }
            """
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
