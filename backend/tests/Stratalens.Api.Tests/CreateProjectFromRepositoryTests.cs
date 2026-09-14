using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stratalens.Api.Contracts;
using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;

namespace Stratalens.Api.Tests;

// T26: POST /api/v1/projects/from-repository. Stubea IProviderConnector (mismo patrón
// WithWebHostBuilder de GitHubRepositoriesTests, T25) con un fixture backend-only (sin
// package.json, para que ReactTypeScriptAnalyzer no intente correr el subproceso Node) que
// el CSharpAnalyzer real (Roslyn, en proceso) detecta como Controller→Service→Repository→
// DbContext — el mismo patrón de fixture que AnalyzeRepositoryPipelineTests (T10).
public class CreateProjectFromRepositoryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CreateProjectFromRepositoryTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly IReadOnlyDictionary<string, string> BackendFixture = new Dictionary<string, string>
    {
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

    private HttpClient ClientWithStubConnector(
        Guid userId, IReadOnlyList<RepositorySummary> repos, IReadOnlyDictionary<string, string>? files = null)
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll(typeof(IProviderConnector));
                services.AddScoped<IProviderConnector>(_ => new StubConnector(repos, files ?? BackendFixture));
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.ToString());
        return client;
    }

    [Fact]
    public async Task Create_ConRepoValido_CreaProyectoYLoAnalizaDeInmediato()
    {
        // Arrange
        var repos = new List<RepositorySummary> { new("carlos", "demo-repo", "main", false) };
        var client = ClientWithStubConnector(Guid.NewGuid(), repos);

        // Act
        var createResp = await client.PostAsJsonAsync(
            "/api/v1/projects/from-repository", new { owner = "carlos", name = "demo-repo" });

        // Assert: 201 con el proyecto ya nombrado como el repo.
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<CreateProjectResponse>();
        Assert.Equal("demo-repo", created!.Name);
        Assert.NotEmpty(created.IngestKey);

        // Assert: el análisis síncrono ya dejó un grafo con nodos y edges.
        var graphResp = await client.GetAsync($"/api/v1/projects/{created.Id}/graph");
        Assert.Equal(HttpStatusCode.OK, graphResp.StatusCode);
        var graph = await graphResp.Content.ReadFromJsonAsync<GraphResponse>();

        Assert.NotEmpty(graph!.Nodes);
        Assert.Contains(graph.Nodes, n => n.Name == "Backend");
        Assert.Contains(graph.Nodes, n => n.Name == "PostgreSQL");
        Assert.NotEmpty(graph.Edges);
    }

    [Fact]
    public async Task Create_ElMismoRepoDosVeces_LaSegundaDevuelve409YNoDuplica()
    {
        // Arrange
        var repos = new List<RepositorySummary> { new("carlos", "demo-repo", "main", false) };
        var client = ClientWithStubConnector(Guid.NewGuid(), repos);
        var body = new { owner = "carlos", name = "demo-repo" };

        // Act
        var firstResp = await client.PostAsJsonAsync("/api/v1/projects/from-repository", body);
        var secondResp = await client.PostAsJsonAsync("/api/v1/projects/from-repository", body);

        // Assert
        Assert.Equal(HttpStatusCode.Created, firstResp.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResp.StatusCode);
    }

    [Fact]
    public async Task Create_ConRepoQueNoEsDelUsuario_Devuelve404()
    {
        // El connector solo "conoce" repos de otro owner: ListRepositoriesAsync ya filtra
        // por affiliation=owner (T25), así que un repo ajeno nunca aparece en la lista.
        var repos = new List<RepositorySummary> { new("otro-usuario", "repo-ajeno", "main", false) };
        var client = ClientWithStubConnector(Guid.NewGuid(), repos);

        var resp = await client.PostAsJsonAsync(
            "/api/v1/projects/from-repository", new { owner = "carlos", name = "no-existe" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // Stub mínimo: sirve la lista de repos y el árbol/contenido del fixture backend.
    private sealed class StubConnector : IProviderConnector
    {
        private readonly IReadOnlyList<RepositorySummary> _repos;
        private readonly IReadOnlyDictionary<string, string> _files;

        public StubConnector(IReadOnlyList<RepositorySummary> repos, IReadOnlyDictionary<string, string> files)
        {
            _repos = repos;
            _files = files;
        }

        public Task<IReadOnlyList<RepositorySummary>> ListRepositoriesAsync(CancellationToken ct = default) =>
            Task.FromResult(_repos);

        public Task<IReadOnlyList<RepositoryFile>> GetFileTreeAsync(RepositoryReference repo, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RepositoryFile>>(
                _files.Keys.Select(p => new RepositoryFile(p, "blob")).ToList());

        public Task<string> GetFileContentAsync(RepositoryReference repo, string path, CancellationToken ct = default) =>
            Task.FromResult(_files[path]);
    }
}
