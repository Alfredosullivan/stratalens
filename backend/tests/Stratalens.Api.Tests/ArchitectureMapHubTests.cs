using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Stratalens.Api.Contracts;

namespace Stratalens.Api.Tests;

// Tests del criterio de T18: un cliente SignalR REAL contra la Api en memoria.
// El dueño se une al grupo de su proyecto; un usuario ajeno es rechazado.
public class ArchitectureMapHubTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ArchitectureMapHubTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // Crea un HubConnection contra el TestServer autenticado como 'userId'.
    // - LongPolling: sobre TestServer es el transporte fiable (WebSocket da flakiness).
    // - HttpMessageHandlerFactory: enruta la conexión al servidor en memoria (sin socket real).
    // - Header X-Test-User: el mismo que lee TestAuthHandler para simular la sesión.
    private HubConnection HubConnectionForUser(Guid userId)
    {
        var server = _factory.Server;
        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "hubs/architecture-map"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Headers.Add(TestAuthHandler.UserHeader, userId.ToString());
            })
            .Build();
    }

    // Crea un proyecto vía la API REST como 'userId' y devuelve su Id (helper AAA).
    private async Task<Guid> CreateProjectForUserAsync(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.ToString());

        var resp = await client.PostAsJsonAsync("/api/v1/projects", new { name = "Proyecto LIVE" });
        resp.EnsureSuccessStatusCode();
        var created = await resp.Content.ReadFromJsonAsync<CreateProjectResponse>();
        return created!.Id;
    }

    [Fact]
    public async Task JoinProject_DelDueño_TieneÉxito()
    {
        // Arrange: el dueño crea su proyecto y abre una conexión al hub.
        var owner = Guid.NewGuid();
        var projectId = await CreateProjectForUserAsync(owner);

        await using var connection = HubConnectionForUser(owner);
        await connection.StartAsync();

        // Act + Assert: unirse a su propio proyecto no lanza y la conexión sigue viva.
        await connection.InvokeAsync("JoinProject", projectId);
        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task JoinProject_DeOtroUsuario_EsRechazado()
    {
        // Arrange: el dueño crea el proyecto; un intruso abre su propia conexión.
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var projectId = await CreateProjectForUserAsync(owner);

        await using var connection = HubConnectionForUser(intruder);
        await connection.StartAsync();

        // Act + Assert: el ownership check (GetProjectUseCase) lanza NotFoundException,
        // el hub la traduce a HubException y su mensaje SÍ llega al cliente.
        var ex = await Assert.ThrowsAsync<HubException>(
            () => connection.InvokeAsync("JoinProject", projectId));
        Assert.Contains("no existe o no es accesible", ex.Message);
    }

    [Fact]
    public async Task Conexión_SinAutenticar_EsRechazada()
    {
        // Arrange: conexión sin header de usuario → TestAuthHandler no autentica.
        var server = _factory.Server;
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "hubs/architecture-map"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();

        // Act + Assert: [Authorize] en el hub rechaza el negotiate (401) → StartAsync falla.
        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }
}
