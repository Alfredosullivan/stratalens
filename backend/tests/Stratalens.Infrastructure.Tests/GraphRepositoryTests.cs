using Microsoft.EntityFrameworkCore;
using Stratalens.Application.Models;
using Stratalens.Domain.Entities;
using Stratalens.Domain.Enums;
using Stratalens.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Stratalens.Infrastructure.Tests;

// Test de INTEGRACIÓN: no usa mocks. Levanta un Postgres real (efímero) en Docker,
// aplica las migraciones reales, y verifica que GraphRepository persiste y recupera
// un grafo de verdad. IAsyncLifetime lo maneja: InitializeAsync antes de los tests,
// DisposeAsync al final (el contenedor se autodestruye).
public class GraphRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .Build();

    private StratalensDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<StratalensDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _db = new StratalensDbContext(options);
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task SaveGraph_PersisteYRecupera_NodesYEdge()
    {
        // Arrange
        var repo = new GraphRepository(_db);
        var project = Project.CreateManual(Guid.NewGuid(), "Demo App");
        await repo.AddProjectAsync(project);

        var frontend = new Node(project.Id, "React Frontend", "React", NodeCategory.Application);
        var backend = new Node(project.Id, "API", "AspNetCoreApi", NodeCategory.Application);
        var edge = Edge.FromStaticAnalysis(
            project.Id, frontend.Id, backend.Id, "HTTP/REST", "src/services/api.ts", 90);

        // Act
        await repo.SaveGraphAsync(
            project.Id, new AnalysisResult(new[] { frontend, backend }, new[] { edge }));
        var graph = await repo.GetGraphAsync(project.Id);

        // Assert: los datos vuelven de la DB tal como se guardaron.
        Assert.Equal(2, graph.Nodes.Count);
        Assert.Single(graph.Edges);

        var recovered = graph.Edges[0];
        Assert.Equal("HTTP/REST", recovered.Type);
        Assert.Equal("src/services/api.ts", recovered.Source);
        Assert.Equal(90, recovered.Confidence);
        Assert.Equal(EdgeSourceType.Static, recovered.SourceType);
        Assert.Equal(frontend.Id, recovered.SourceNodeId);
        Assert.Equal(backend.Id, recovered.TargetNodeId);
    }

    [Fact]
    public async Task SaveGraph_EsIdempotente_ReemplazaGrafoAnterior()
    {
        // Arrange: guardo un grafo, luego lo reemplazo por otro más pequeño.
        var repo = new GraphRepository(_db);
        var project = Project.CreateManual(Guid.NewGuid(), "Otra App");
        await repo.AddProjectAsync(project);

        var n1 = new Node(project.Id, "N1", "React", NodeCategory.Application);
        var n2 = new Node(project.Id, "N2", "PostgreSQL", NodeCategory.Database);
        await repo.SaveGraphAsync(project.Id, new AnalysisResult(new[] { n1, n2 }, Array.Empty<Edge>()));

        // Act: segundo análisis con un solo nodo.
        var n3 = new Node(project.Id, "N3", "Redis", NodeCategory.Database);
        await repo.SaveGraphAsync(project.Id, new AnalysisResult(new[] { n3 }, Array.Empty<Edge>()));
        var graph = await repo.GetGraphAsync(project.Id);

        // Assert: no se acumulan; queda solo el resultado del último análisis.
        Assert.Single(graph.Nodes);
        Assert.Equal("N3", graph.Nodes[0].Name);
    }
}
