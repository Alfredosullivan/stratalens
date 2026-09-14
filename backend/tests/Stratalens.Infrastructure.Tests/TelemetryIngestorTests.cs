using Microsoft.EntityFrameworkCore;
using Stratalens.Application.Models;
using Stratalens.Domain.Entities;
using Stratalens.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Stratalens.Infrastructure.Tests;

// Test de INTEGRACIÓN: Postgres real efímero en Docker + migraciones reales.
// Verifica que TelemetryIngestor persiste los spans agrupados en su Trace correcto
// (criterio de aceptación de T15).
public class TelemetryIngestorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .Build();

    private StratalensDbContext _db = null!;

    private static readonly DateTime StartUtc = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);

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

    // Helper: crea un proyecto real para asociar la telemetría (la FK/lógica lo exige).
    private async Task<Project> CrearProyectoAsync()
    {
        var project = Project.CreateManual(Guid.NewGuid(), "Demo App");
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return project;
    }

    [Fact]
    public async Task Ingest_AgrupaSpansPorTrace_YLosPersiste()
    {
        // Arrange: dos traces distintos mezclados en un mismo batch, como llegarían del endpoint.
        var project = await CrearProyectoAsync();
        var ingestor = new TelemetryIngestor(_db);

        var spans = new List<TelemetrySpan>
        {
            // Trace A: raíz + un hijo.
            new("trace-A", "a-root", null, "Frontend", "Backend", "GET /pedidos", StartUtc, 40, "OK"),
            new("trace-A", "a-child", "a-root", "Backend", "PostgreSQL", "SELECT pedidos", StartUtc, 12, "OK"),
            // Trace B: solo su raíz.
            new("trace-B", "b-root", null, "Frontend", "Backend", "POST /login", StartUtc, 25, "OK"),
        };

        // Act
        await ingestor.IngestAsync(project.Id, spans);

        // Assert: se recuperan DOS traces, cada uno con sus spans agrupados.
        var traces = await _db.Traces
            .AsNoTracking()
            .Include(t => t.Spans)
            .Where(t => t.ProjectId == project.Id)
            .ToListAsync();

        Assert.Equal(2, traces.Count);

        var traceA = traces.Single(t => t.TraceId == "trace-A");
        Assert.Equal(2, traceA.Spans.Count);
        Assert.Single(traceA.Spans, s => s.IsRoot);   // exactamente un raíz sobrevive el round-trip

        var traceB = traces.Single(t => t.TraceId == "trace-B");
        Assert.Single(traceB.Spans);
    }

    [Fact]
    public async Task Ingest_PreservaLosDatosDelSpan_EnRoundTrip()
    {
        // Arrange
        var project = await CrearProyectoAsync();
        var ingestor = new TelemetryIngestor(_db);

        var spans = new List<TelemetrySpan>
        {
            new("trace-X", "x-root", null, "Frontend", "Backend", "POST /api/login", StartUtc, 33.5, "OK"),
        };

        // Act
        await ingestor.IngestAsync(project.Id, spans);

        // Assert: los campos vuelven de la DB tal como entraron, y StartedAt sigue en UTC.
        var span = await _db.Set<Span>().AsNoTracking().SingleAsync();
        Assert.Equal("x-root", span.SpanId);
        Assert.Null(span.ParentSpanId);
        Assert.Equal("Frontend", span.SourceNode);
        Assert.Equal("Backend", span.TargetNode);
        Assert.Equal("POST /api/login", span.Operation);
        Assert.Equal(33.5, span.DurationMs);
        Assert.Equal("OK", span.Status);
        Assert.Equal(DateTimeKind.Utc, span.StartedAt.Kind);
        Assert.Equal(StartUtc, span.StartedAt);
    }

    [Fact]
    public async Task Ingest_EsAcumulativo_NoBorraTracesPrevios()
    {
        // Arrange: primera ingesta.
        var project = await CrearProyectoAsync();
        var ingestor = new TelemetryIngestor(_db);
        await ingestor.IngestAsync(project.Id, new List<TelemetrySpan>
        {
            new("trace-1", "s1", null, "Frontend", "Backend", "GET /", StartUtc, 10, "OK"),
        });

        // Act: segunda ingesta de OTRO trace.
        await ingestor.IngestAsync(project.Id, new List<TelemetrySpan>
        {
            new("trace-2", "s2", null, "Frontend", "Backend", "GET /home", StartUtc, 15, "OK"),
        });

        // Assert: la telemetría se acumula (a diferencia del grafo, que se reemplaza).
        var count = await _db.Traces.CountAsync(t => t.ProjectId == project.Id);
        Assert.Equal(2, count);
    }
}
