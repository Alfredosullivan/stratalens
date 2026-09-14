using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Stratalens.Api.Contracts;
using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Entities;
using Stratalens.Domain.Enums;

namespace Stratalens.Api.Tests;

// Tests de integración de T11: GET /api/v1/projects/{id}/graph.
// Crea un proyecto por HTTP, siembra su grafo directamente en el repositorio (el pipeline
// de análisis ya se prueba end-to-end en T10), y verifica que el endpoint devuelve el
// grafo esperado y respeta el ownership (404 para proyectos ajenos).
public class ProjectGraphTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ProjectGraphTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientForUser(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.ToString());
        return client;
    }

    // Persiste un grafo Frontend → Backend → PostgreSQL para un proyecto ya existente.
    private async Task SeedGraphAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        var graph = scope.ServiceProvider.GetRequiredService<IGraphRepository>();

        var frontend = new Node(projectId, "Frontend", "React", NodeCategory.Application);
        var backend = new Node(projectId, "Backend", "AspNetCore", NodeCategory.Application);
        var database = new Node(projectId, "PostgreSQL", "PostgreSQL", NodeCategory.Database);

        var frontToBack = Edge.FromStaticAnalysis(
            projectId, frontend.Id, backend.Id, "HTTP/REST", "src/services/productService.ts", 80);
        var backToDb = Edge.FromStaticAnalysis(
            projectId, backend.Id, database.Id, "SQL", "Infrastructure/AppDbContext.cs", 85);

        await graph.SaveGraphAsync(
            projectId,
            new AnalysisResult(new[] { frontend, backend, database }, new[] { frontToBack, backToDb }));
    }

    [Fact]
    public async Task GetGraph_DelDueno_DevuelveElGrafoPersistido()
    {
        var userId = Guid.NewGuid();
        var client = ClientForUser(userId);

        // Crear proyecto por HTTP y sembrar su grafo.
        var createResp = await client.PostAsJsonAsync("/api/v1/projects", new { name = "Demo" });
        createResp.EnsureSuccessStatusCode();
        var project = await createResp.Content.ReadFromJsonAsync<ProjectDto>();
        await SeedGraphAsync(project!.Id);

        // Pedir el grafo.
        var response = await client.GetAsync($"/api/v1/projects/{project.Id}/graph");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var graph = await response.Content.ReadFromJsonAsync<GraphResponse>();

        Assert.NotNull(graph);
        Assert.Equal(3, graph!.Nodes.Count);
        Assert.Contains(graph.Nodes, n => n.Name == "Frontend" && n.Category == "Application");
        Assert.Contains(graph.Nodes, n => n.Name == "PostgreSQL" && n.Category == "Database");

        Assert.Equal(2, graph.Edges.Count);
        var httpEdge = Assert.Single(graph.Edges, e => e.Type == "HTTP/REST");
        Assert.Equal(80, httpEdge.Confidence);
        Assert.Equal("Static", httpEdge.SourceType);
        Assert.False(string.IsNullOrWhiteSpace(httpEdge.SourceFile));
    }

    [Fact]
    public async Task GetGraph_DeOtroUsuario_Devuelve404()
    {
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();

        var ownerClient = ClientForUser(owner);
        var createResp = await ownerClient.PostAsJsonAsync("/api/v1/projects", new { name = "Privado" });
        createResp.EnsureSuccessStatusCode();
        var project = await createResp.Content.ReadFromJsonAsync<ProjectDto>();
        await SeedGraphAsync(project!.Id);

        // Otro usuario intenta ver el grafo → 404 (no filtramos existencia).
        var intruderClient = ClientForUser(intruder);
        var response = await intruderClient.GetAsync($"/api/v1/projects/{project.Id}/graph");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetGraph_SinAutenticar_Devuelve401()
    {
        var client = _factory.CreateClient(); // sin header de usuario
        var response = await client.GetAsync($"/api/v1/projects/{Guid.NewGuid()}/graph");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
