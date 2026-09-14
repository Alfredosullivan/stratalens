using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stratalens.Api.Auth;
using Stratalens.Api.Contracts;
using Stratalens.Domain.Entities;
using Stratalens.Infrastructure.Persistence;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;

namespace Stratalens.Api.Tests;

// Tests de integración de T16: POST /api/v1/telemetry/traces con un payload OTLP protobuf REAL.
// Ejercen el pipeline completo: autenticación por clave de ingesta, parseo OTLP, normalización
// y persistencia. Usan la Api real (WebApplicationFactory) contra Postgres efímero.
public class TelemetryIngestionTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string OtlpContentType = "application/x-protobuf";

    private readonly CustomWebApplicationFactory _factory;

    public TelemetryIngestionTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // Crea un proyecto por HTTP (como un usuario) y devuelve su id + su clave de ingesta en claro.
    private async Task<(Guid ProjectId, string IngestKey)> CrearProyectoConClaveAsync()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, Guid.NewGuid().ToString());

        var resp = await client.PostAsJsonAsync("/api/v1/projects", new { name = "Demo instrumentado" });
        resp.EnsureSuccessStatusCode();
        var created = await resp.Content.ReadFromJsonAsync<CreateProjectResponse>();

        Assert.NotNull(created);
        Assert.False(string.IsNullOrWhiteSpace(created!.IngestKey));
        return (created.Id, created.IngestKey);
    }

    // Construye un ExportTraceServiceRequest OTLP con un trace de 2 spans (raíz + hijo a la DB).
    private static ExportTraceServiceRequest ConstruirPayloadOtlp(byte[] traceId)
    {
        var startNanos = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000UL;

        var rootSpan = new OtlpSpan
        {
            TraceId = ByteString.CopyFrom(traceId),
            SpanId = ByteString.CopyFrom(new byte[] { 1, 1, 1, 1, 1, 1, 1, 1 }),
            // ParentSpanId vacío ⇒ raíz.
            Name = "GET /pedidos",
            Kind = OtlpSpan.Types.SpanKind.Server,
            StartTimeUnixNano = startNanos,
            EndTimeUnixNano = startNanos + 40_000_000UL, // +40 ms
            Status = new Status { Code = Status.Types.StatusCode.Ok }
        };

        var childSpan = new OtlpSpan
        {
            TraceId = ByteString.CopyFrom(traceId),
            SpanId = ByteString.CopyFrom(new byte[] { 2, 2, 2, 2, 2, 2, 2, 2 }),
            ParentSpanId = ByteString.CopyFrom(new byte[] { 1, 1, 1, 1, 1, 1, 1, 1 }),
            Name = "SELECT pedidos",
            Kind = OtlpSpan.Types.SpanKind.Client,
            StartTimeUnixNano = startNanos + 1_000_000UL,
            EndTimeUnixNano = startNanos + 13_000_000UL, // 12 ms
            Attributes = { new KeyValue { Key = "db.system", Value = new AnyValue { StringValue = "postgresql" } } },
            Status = new Status { Code = Status.Types.StatusCode.Ok }
        };

        return new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "demo-backend" } }
                        }
                    },
                    ScopeSpans = { new ScopeSpans { Spans = { rootSpan, childSpan } } }
                }
            }
        };
    }

    private static HttpContent OtlpContent(ExportTraceServiceRequest request)
    {
        var content = new ByteArrayContent(request.ToByteArray());
        content.Headers.ContentType = new MediaTypeHeaderValue(OtlpContentType);
        return content;
    }

    [Fact]
    public async Task PostTraces_ConClaveValida_PersisteElTraceEnElProyectoCorrecto()
    {
        // Arrange
        var (projectId, ingestKey) = await CrearProyectoConClaveAsync();
        var traceIdBytes = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        var payload = ConstruirPayloadOtlp(traceIdBytes);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(IngestKeyAuthenticationHandler.HeaderName, ingestKey);

        // Act
        var response = await client.PostAsync("/api/v1/telemetry/traces", OtlpContent(payload));

        // Assert HTTP
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert persistencia: el trace quedó guardado con sus 2 spans, en ESTE proyecto.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StratalensDbContext>();

        var expectedTraceId = Convert.ToHexString(traceIdBytes).ToLowerInvariant();
        var trace = await db.Traces.AsNoTracking()
            .Include(t => t.Spans)
            .SingleOrDefaultAsync(t => t.ProjectId == projectId && t.TraceId == expectedTraceId);

        Assert.NotNull(trace);
        Assert.Equal(2, trace!.Spans.Count);
        Assert.Single(trace.Spans, s => s.IsRoot);                    // un único raíz sobrevive
        Assert.All(trace.Spans, s => Assert.Equal("demo-backend", s.SourceNode)); // service.name → SourceNode

        var child = trace.Spans.Single(s => !s.IsRoot);
        Assert.Equal("SELECT pedidos", child.Operation);
        Assert.Equal("postgresql", child.TargetNode);                 // db.system → TargetNode
        Assert.Equal(12, child.DurationMs);                           // 13ms - 1ms de offset
    }

    [Fact]
    public async Task GetTraces_DelDueno_DevuelveElTraceIngestadoConSuDuracion()
    {
        // Arrange: creamos proyecto, guardamos su user (para leer como dueño) e ingestamos.
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.ToString());

        var createResp = await client.PostAsJsonAsync("/api/v1/projects", new { name = "Con traces" });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<CreateProjectResponse>();

        var traceIdBytes = Enumerable.Range(10, 16).Select(i => (byte)i).ToArray();
        var ingestClient = _factory.CreateClient();
        ingestClient.DefaultRequestHeaders.Add(IngestKeyAuthenticationHandler.HeaderName, created!.IngestKey);
        var ingest = await ingestClient.PostAsync(
            "/api/v1/telemetry/traces", OtlpContent(ConstruirPayloadOtlp(traceIdBytes)));
        ingest.EnsureSuccessStatusCode();

        // Act: el dueño lee sus traces por HTTP.
        var response = await client.GetAsync($"/api/v1/projects/{created.Id}/traces");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var traces = await response.Content.ReadFromJsonAsync<List<TraceDto>>();

        Assert.NotNull(traces);
        var trace = Assert.Single(traces!);
        Assert.Equal(Convert.ToHexString(traceIdBytes).ToLowerInvariant(), trace.TraceId);
        Assert.Equal(2, trace.Spans.Count);
        Assert.Equal(40, trace.DurationMs); // duración del span raíz (SERVER), 40 ms
    }

    [Fact]
    public async Task PostTraces_SinClave_Devuelve401()
    {
        var payload = ConstruirPayloadOtlp(Enumerable.Range(0, 16).Select(i => (byte)i).ToArray());
        var client = _factory.CreateClient(); // sin header de clave

        var response = await client.PostAsync("/api/v1/telemetry/traces", OtlpContent(payload));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostTraces_ConClaveInvalida_Devuelve401()
    {
        var payload = ConstruirPayloadOtlp(Enumerable.Range(0, 16).Select(i => (byte)i).ToArray());
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(IngestKeyAuthenticationHandler.HeaderName, "om_ing_clave_falsa");

        var response = await client.PostAsync("/api/v1/telemetry/traces", OtlpContent(payload));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
