using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Enums;
using Stratalens.Infrastructure.Analyzers;

namespace Stratalens.Infrastructure.Tests;

// Test del analyzer de C# (T8) contra un fixture .NET real (Clean Architecture en
// miniatura). No hay red ni GitHub: un StubConnector sirve el contenido de los archivos
// que definimos abajo. Verificamos que detecta la cadena Controller → Service →
// Repository → DbContext y que los edges salen con Confidence < 100 (inferencia estática).
public class CSharpAnalyzerTests
{
    private static readonly RepositoryReference Repo = new("carlos", "demo", "main");

    [Fact]
    public void CanAnalyze_EsTrue_SoloSiHayCsproj()
    {
        var analyzer = new CSharpAnalyzer();

        var conCsproj = new List<RepositoryFile>
        {
            new("src/Demo.csproj", "blob"),
            new("src/Program.cs", "blob")
        };
        var sinCsproj = new List<RepositoryFile> { new("README.md", "blob") };

        Assert.True(analyzer.CanAnalyze(conCsproj));
        Assert.False(analyzer.CanAnalyze(sinCsproj));
    }

    [Fact]
    public async Task Analyze_DetectaCadenaControllerServiceRepositoryDbContext()
    {
        // --- Arrange ---
        var files = Fixture.Keys.Select(path => new RepositoryFile(path, "blob")).ToList();
        var connector = new StubConnector(Fixture);
        var analyzer = new CSharpAnalyzer();
        var projectId = Guid.NewGuid();

        // --- Act ---
        var result = await analyzer.AnalyzeAsync(projectId, Repo, connector, files);

        // --- Assert: nodos ---
        Assert.Equal(4, result.Nodes.Count);
        var byType = result.Nodes.ToDictionary(n => n.Type, n => n);

        Assert.Equal("ProductsController", byType["Controller"].Name);
        Assert.Equal("ProductService", byType["Service"].Name);
        Assert.Equal("ProductRepository", byType["Repository"].Name);
        Assert.Equal("AppDbContext", byType["DbContext"].Name);

        // Todos son código y llevan su archivo de origen en la metadata.
        Assert.All(result.Nodes, n => Assert.Equal(NodeCategory.Code, n.Category));
        Assert.Equal(
            "src/Api/Controllers/ProductsController.cs",
            byType["Controller"].Metadata["source"]);

        // --- Assert: edges (la cadena de dependencias) ---
        Assert.Equal(3, result.Edges.Count);

        var typeById = result.Nodes.ToDictionary(n => n.Id, n => n.Type);
        var relations = result.Edges
            .Select(e => (From: typeById[e.SourceNodeId], To: typeById[e.TargetNodeId]))
            .ToHashSet();

        Assert.Contains(("Controller", "Service"), relations);
        Assert.Contains(("Service", "Repository"), relations);
        Assert.Contains(("Repository", "DbContext"), relations);

        // Toda relación estática: confianza alta pero nunca 100, con su Source no vacío.
        Assert.All(result.Edges, e =>
        {
            Assert.Equal(EdgeSourceType.Static, e.SourceType);
            Assert.Equal(90, e.Confidence);
            Assert.False(string.IsNullOrWhiteSpace(e.Source));
        });
    }

    // --- Fixture: proyecto .NET mínimo con Clean Architecture ---
    // La inyección es por INTERFAZ (IProductService, IProductRepository), como en código
    // real: así el test comprueba que el analyzer resuelve la interfaz al nodo concreto.
    private static readonly Dictionary<string, string> Fixture = new()
    {
        ["src/Demo.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>",

        ["src/Api/Controllers/ProductsController.cs"] = """
            using Microsoft.AspNetCore.Mvc;
            namespace Demo.Api.Controllers;

            [ApiController]
            [Route("api/products")]
            public class ProductsController : ControllerBase
            {
                private readonly IProductService _service;
                public ProductsController(IProductService service) => _service = service;
            }
            """,

        ["src/Application/ProductService.cs"] = """
            namespace Demo.Application;

            public interface IProductService { }

            public class ProductService : IProductService
            {
                private readonly IProductRepository _repository;
                public ProductService(IProductRepository repository) => _repository = repository;
            }
            """,

        ["src/Infrastructure/ProductRepository.cs"] = """
            namespace Demo.Infrastructure;

            public interface IProductRepository { }

            public class ProductRepository : IProductRepository
            {
                private readonly AppDbContext _db;
                public ProductRepository(AppDbContext db) => _db = db;
            }
            """,

        ["src/Infrastructure/AppDbContext.cs"] = """
            using Microsoft.EntityFrameworkCore;
            namespace Demo.Infrastructure;

            public class AppDbContext : DbContext { }
            """
    };

    // Connector de stub: solo sirve el contenido de archivo desde el fixture en memoria.
    // Los otros métodos no se usan en este test (el analyzer recibe la lista de archivos).
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
