using System.Net;
using System.Net.Http.Json;
using Stratalens.Api.Contracts;

namespace Stratalens.Api.Tests;

// Tests del criterio de T6: aislamiento entre usuarios (ownership) y auth requerida.
public class ProjectsAuthorizationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ProjectsAuthorizationTests(CustomWebApplicationFactory factory)
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
    public async Task GetProject_DeOtroUsuario_Devuelve404()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        // User A crea un proyecto.
        var clientA = ClientForUser(userA);
        var createResp = await clientA.PostAsJsonAsync("/api/v1/projects", new { name = "Proyecto de A" });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ProjectDto>();

        // User A SÍ puede verlo.
        var ownResp = await clientA.GetAsync($"/api/v1/projects/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, ownResp.StatusCode);

        // User B NO puede verlo → 404 (no filtramos existencia de proyectos ajenos).
        var clientB = ClientForUser(userB);
        var crossResp = await clientB.GetAsync($"/api/v1/projects/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, crossResp.StatusCode);
    }

    [Fact]
    public async Task GetProject_SinAutenticar_Devuelve401()
    {
        var client = _factory.CreateClient(); // sin header de usuario
        var resp = await client.GetAsync($"/api/v1/projects/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task CreateProject_ConNombreVacio_Devuelve400()
    {
        var client = ClientForUser(Guid.NewGuid());
        var resp = await client.PostAsJsonAsync("/api/v1/projects", new { name = "" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
