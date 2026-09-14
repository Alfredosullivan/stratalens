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

// T27: POST /api/v1/projects/{id}/analyze. Usa un connector con contenido MUTABLE (mismo
// objeto reutilizado entre llamadas vía closure) para simular "el repo cambió entre
// análisis" sin depender de Node/TS (solo fixtures C#, detectadas por Roslyn real —
// mismo espíritu que CreateProjectFromRepositoryTests, T26).
public class ReanalyzeProjectTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReanalyzeProjectTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // V1: solo un Controller → csharp detecta algo → SystemGraphBuilder crea SOLO "Backend"
    // (sin DbContext no hay "PostgreSQL" ni edge, ver SystemGraphBuilder.Build).
    private static readonly IReadOnlyDictionary<string, string> FixtureSinDb = new Dictionary<string, string>
    {
        ["backend/Demo.Api.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>",
        ["backend/Controllers/ProductsController.cs"] = """
            using Microsoft.AspNetCore.Mvc;
            namespace Demo.Api;
            [ApiController]
            public class ProductsController : ControllerBase
            {
            }
            """
    };

    // V2: mismo backend + Service/Repository/DbContext → ahora SÍ aparece "PostgreSQL" y
    // el edge Backend→PostgreSQL. El "contenido nuevo" que el re-análisis debe reflejar.
    private static readonly IReadOnlyDictionary<string, string> FixtureConDb = new Dictionary<string, string>
    {
        ["backend/Demo.Api.csproj"] = FixtureSinDb["backend/Demo.Api.csproj"],
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

    private (HttpClient Client, MutableStubConnector Connector) ClientWithMutableConnector(
        Guid userId, IReadOnlyList<RepositorySummary> repos, IReadOnlyDictionary<string, string> initialFiles)
    {
        var connector = new MutableStubConnector(repos, initialFiles);

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll(typeof(IProviderConnector));
                services.AddScoped<IProviderConnector>(_ => connector);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.ToString());
        return (client, connector);
    }

    [Fact]
    public async Task Reanalyze_ConRepoCambiado_ReemplazaElGrafoSinDuplicar()
    {
        // Arrange: crea el proyecto con el fixture SIN DbContext.
        var repos = new List<RepositorySummary> { new("carlos", "demo-repo", "main", false) };
        var (client, connector) = ClientWithMutableConnector(Guid.NewGuid(), repos, FixtureSinDb);

        var createResp = await client.PostAsJsonAsync(
            "/api/v1/projects/from-repository", new { owner = "carlos", name = "demo-repo" });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<CreateProjectResponse>();

        var firstGraph = await (await client.GetAsync($"/api/v1/projects/{created!.Id}/graph"))
            .Content.ReadFromJsonAsync<GraphResponse>();
        Assert.Single(firstGraph!.Nodes); // solo "Backend", sin DB todavía.

        // Act: el repo "cambia" (ahora tiene DbContext) y se re-analiza.
        connector.Files = FixtureConDb;
        var reanalyzeResp = await client.PostAsync($"/api/v1/projects/{created.Id}/analyze", content: null);

        // Assert: 200 con el grafo YA actualizado en la respuesta.
        Assert.Equal(HttpStatusCode.OK, reanalyzeResp.StatusCode);
        var updatedGraph = await reanalyzeResp.Content.ReadFromJsonAsync<GraphResponse>();

        Assert.Equal(2, updatedGraph!.Nodes.Count); // Backend + PostgreSQL, no duplicado.
        Assert.Single(updatedGraph.Nodes, n => n.Name == "Backend");
        Assert.Single(updatedGraph.Nodes, n => n.Name == "PostgreSQL");
        Assert.Single(updatedGraph.Edges);

        // El grafo persistido coincide con el de la respuesta (no quedó a medio reemplazar).
        var persistedGraph = await (await client.GetAsync($"/api/v1/projects/{created.Id}/graph"))
            .Content.ReadFromJsonAsync<GraphResponse>();
        Assert.Equal(2, persistedGraph!.Nodes.Count);
    }

    [Fact]
    public async Task Reanalyze_DeProyectoAjeno_Devuelve404()
    {
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var repos = new List<RepositorySummary> { new("carlos", "demo-repo", "main", false) };
        var (ownerClient, _) = ClientWithMutableConnector(owner, repos, FixtureSinDb);

        var createResp = await ownerClient.PostAsJsonAsync(
            "/api/v1/projects/from-repository", new { owner = "carlos", name = "demo-repo" });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<CreateProjectResponse>();

        var intruderClient = _factory.CreateClient();
        intruderClient.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, intruder.ToString());

        var resp = await intruderClient.PostAsync($"/api/v1/projects/{created!.Id}/analyze", content: null);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Reanalyze_DeProyectoManualSinRepo_Devuelve404()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, Guid.NewGuid().ToString());

        var createResp = await client.PostAsJsonAsync("/api/v1/projects", new { name = "Manual" });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ProjectDto>();

        var resp = await client.PostAsync($"/api/v1/projects/{created!.Id}/analyze", content: null);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // Stub con contenido mutable: el mismo objeto se reutiliza entre llamadas HTTP del
    // test (registrado por instancia vía closure), así "cambiar Files" simula que el
    // repo real cambió entre el análisis inicial y el re-análisis.
    private sealed class MutableStubConnector : IProviderConnector
    {
        private readonly IReadOnlyList<RepositorySummary> _repos;
        public IReadOnlyDictionary<string, string> Files { get; set; }

        public MutableStubConnector(IReadOnlyList<RepositorySummary> repos, IReadOnlyDictionary<string, string> files)
        {
            _repos = repos;
            Files = files;
        }

        public Task<IReadOnlyList<RepositorySummary>> ListRepositoriesAsync(CancellationToken ct = default) =>
            Task.FromResult(_repos);

        public Task<IReadOnlyList<RepositoryFile>> GetFileTreeAsync(RepositoryReference repo, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RepositoryFile>>(
                Files.Keys.Select(p => new RepositoryFile(p, "blob")).ToList());

        public Task<string> GetFileContentAsync(RepositoryReference repo, string path, CancellationToken ct = default) =>
            Task.FromResult(Files[path]);
    }
}
