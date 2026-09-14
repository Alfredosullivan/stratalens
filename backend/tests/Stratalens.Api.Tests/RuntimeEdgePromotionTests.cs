using System.Net.Http.Headers;
using System.Net.Http.Json;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Stratalens.Api.Auth;
using Stratalens.Api.Contracts;
using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Entities;
using Stratalens.Domain.Enums;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;

namespace Stratalens.Api.Tests;

// Tests de integración de T20: al ingestar un trace, los hops observados se promueven a
// Edges de runtime (Confidence=100, SourceType=Runtime), superponiéndose al estático del
// mismo par; y un hop cuyo extremo no casa con un nodo del grafo NO crea un edge falso.
public class RuntimeEdgePromotionTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string OtlpContentType = "application/x-protobuf";

    private readonly CustomWebApplicationFactory _factory;

    public RuntimeEdgePromotionTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(Guid ProjectId, string IngestKey)> CrearProyectoAsync()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, Guid.NewGuid().ToString());

        var resp = await client.PostAsJsonAsync("/api/v1/projects", new { name = "Con grafo" });
        resp.EnsureSuccessStatusCode();
        var created = await resp.Content.ReadFromJsonAsync<CreateProjectResponse>();
        return (created!.Id, created.IngestKey);
    }

    // Siembra el grafo del proyecto usando el repositorio real (sobre el Postgres de test).
    private async Task SeedGraphAsync(Guid projectId, IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges)
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGraphRepository>();
        await repo.SaveGraphAsync(projectId, new AnalysisResult(nodes, edges));
    }

    private async Task<ProjectGraph> GetGraphAsync(Guid projectId)
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGraphRepository>();
        return await repo.GetGraphAsync(projectId);
    }

    private async Task IngestarAsync(string ingestKey, ExportTraceServiceRequest payload)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(IngestKeyAuthenticationHandler.HeaderName, ingestKey);

        var content = new ByteArrayContent(payload.ToByteArray());
        content.Headers.ContentType = new MediaTypeHeaderValue(OtlpContentType);

        var resp = await client.PostAsync("/api/v1/telemetry/traces", content);
        resp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Ingesta_PromueveHopARuntimeEdge_ySuperaElEstatico()
    {
        // Arrange: proyecto con grafo Backend --(SQL, estático)--> PostgreSQL. El nombre
        // del backend casa con el service.name del trace ("demo-backend"); el de la DB
        // casa por Type ("postgresql" ≈ "PostgreSQL").
        var (projectId, ingestKey) = await CrearProyectoAsync();
        var backend = new Node(projectId, "demo-backend", "AspNetCore", NodeCategory.Application);
        var database = new Node(projectId, "PostgreSQL", "PostgreSQL", NodeCategory.Database);
        var staticEdge = Edge.FromStaticAnalysis(
            projectId, backend.Id, database.Id, "SQL", "AppDbContext.cs", 85);
        await SeedGraphAsync(projectId, new[] { backend, database }, new[] { staticEdge });

        // Act: ingestamos un trace con el hop demo-backend → postgresql.
        var traceId = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        await IngestarAsync(ingestKey, ConstruirPayloadOtlp(traceId, targetDbSystem: "postgresql"));

        // Assert: queda UN solo edge del par, ahora de runtime y con Confidence 100.
        var graph = await GetGraphAsync(projectId);
        var edge = Assert.Single(graph.Edges);
        Assert.Equal(backend.Id, edge.SourceNodeId);
        Assert.Equal(database.Id, edge.TargetNodeId);
        Assert.Equal(EdgeSourceType.Runtime, edge.SourceType);
        Assert.Equal(100, edge.Confidence);
        Assert.Equal("SQL", edge.Type);
    }

    [Fact]
    public async Task Ingesta_HopSinNodoCorrespondiente_NoCreaEdge()
    {
        // Arrange: el grafo solo tiene el backend; NO hay nodo para la DB.
        var (projectId, ingestKey) = await CrearProyectoAsync();
        var backend = new Node(projectId, "demo-backend", "AspNetCore", NodeCategory.Application);
        await SeedGraphAsync(projectId, new[] { backend }, Array.Empty<Edge>());

        // Act: el trace apunta a "redis", que no tiene nodo en el grafo.
        var traceId = Enumerable.Range(20, 16).Select(i => (byte)i).ToArray();
        await IngestarAsync(ingestKey, ConstruirPayloadOtlp(traceId, targetDbSystem: "redis"));

        // Assert: el hop sin match no inventó ningún edge.
        var graph = await GetGraphAsync(projectId);
        Assert.Empty(graph.Edges);
    }

    // Trace de 2 spans: raíz SERVER "GET /pedidos" (service.name=demo-backend, sin target)
    // + hijo CLIENT con db.system=<targetDbSystem> (el hop a promover).
    private static ExportTraceServiceRequest ConstruirPayloadOtlp(byte[] traceId, string targetDbSystem)
    {
        var startNanos = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000UL;

        var rootSpan = new OtlpSpan
        {
            TraceId = ByteString.CopyFrom(traceId),
            SpanId = ByteString.CopyFrom(new byte[] { 1, 1, 1, 1, 1, 1, 1, 1 }),
            Name = "GET /pedidos",
            Kind = OtlpSpan.Types.SpanKind.Server,
            StartTimeUnixNano = startNanos,
            EndTimeUnixNano = startNanos + 40_000_000UL,
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
            EndTimeUnixNano = startNanos + 13_000_000UL,
            Attributes = { new KeyValue { Key = "db.system", Value = new AnyValue { StringValue = targetDbSystem } } },
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
}
