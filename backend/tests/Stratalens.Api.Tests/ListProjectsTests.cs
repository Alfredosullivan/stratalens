using System.Net.Http.Json;
using Stratalens.Api.Contracts;

namespace Stratalens.Api.Tests;

// T28: GET /api/v1/projects. No confundir con GetById (T6): este es el listado/dashboard.
public class ListProjectsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ListProjectsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientForUser(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.ToString());
        return client;
    }

    [Fact]
    public async Task ListMine_DevuelveSoloLosProyectosDelUsuarioAutenticado()
    {
        // Arrange: User A tiene 2 proyectos, User B tiene 1.
        var clientA = ClientForUser(Guid.NewGuid());
        var clientB = ClientForUser(Guid.NewGuid());

        await clientA.PostAsJsonAsync("/api/v1/projects", new { name = "Proyecto A1" });
        await clientA.PostAsJsonAsync("/api/v1/projects", new { name = "Proyecto A2" });
        await clientB.PostAsJsonAsync("/api/v1/projects", new { name = "Proyecto B1" });

        // Act
        var listA = await (await clientA.GetAsync("/api/v1/projects")).Content.ReadFromJsonAsync<List<ProjectDto>>();
        var listB = await (await clientB.GetAsync("/api/v1/projects")).Content.ReadFromJsonAsync<List<ProjectDto>>();

        // Assert: cada uno ve solo lo suyo.
        Assert.Equal(2, listA!.Count);
        Assert.All(listA, p => Assert.StartsWith("Proyecto A", p.Name));

        Assert.Single(listB!);
        Assert.Equal("Proyecto B1", listB![0].Name);
        // Proyecto manual: sin repo conectado.
        Assert.Null(listB[0].RepositoryOwner);
    }

    [Fact]
    public async Task ListMine_SinProyectos_DevuelveListaVacia()
    {
        var client = ClientForUser(Guid.NewGuid());
        var list = await (await client.GetAsync("/api/v1/projects")).Content.ReadFromJsonAsync<List<ProjectDto>>();
        Assert.Empty(list!);
    }

    [Fact]
    public async Task ListMine_SinAutenticar_Devuelve401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/projects");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
