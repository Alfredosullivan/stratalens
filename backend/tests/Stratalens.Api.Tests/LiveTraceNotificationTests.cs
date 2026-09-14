using System.Net.Http.Headers;
using System.Net.Http.Json;
using Google.Protobuf;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Stratalens.Api.Auth;
using Stratalens.Api.Contracts;
using Stratalens.Api.Realtime;
using Stratalens.Application.Models;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;

namespace Stratalens.Api.Tests;

// Tests de integración de T19: al ingestar un trace se emite el evento LIVE 'TraceIngested'
// al grupo del proyecto, y NO a clientes de otros proyectos. Cliente SignalR real
// (LongPolling sobre TestServer) + POST OTLP real, como en los tests de T16/T18.
public class LiveTraceNotificationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string OtlpContentType = "application/x-protobuf";

    private readonly CustomWebApplicationFactory _factory;

    public LiveTraceNotificationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // Crea un proyecto por HTTP y devuelve id + clave de ingesta + id del usuario dueño
    // (necesitamos el userId para que la conexión SignalR pueda unirse a su grupo).
    private async Task<(Guid ProjectId, string IngestKey, Guid OwnerId)> CrearProyectoAsync()
    {
        var ownerId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, ownerId.ToString());

        var resp = await client.PostAsJsonAsync("/api/v1/projects", new { name = "Proyecto LIVE" });
        resp.EnsureSuccessStatusCode();
        var created = await resp.Content.ReadFromJsonAsync<CreateProjectResponse>();
        return (created!.Id, created.IngestKey, ownerId);
    }

    // Conexión SignalR autenticada como 'userId' (mismo patrón que ArchitectureMapHubTests).
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

    // POST OTLP con un trace de 2 spans (raíz SERVER 40ms + hijo a la DB 12ms).
    private async Task IngestarTraceAsync(string ingestKey, byte[] traceId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(IngestKeyAuthenticationHandler.HeaderName, ingestKey);

        var content = new ByteArrayContent(ConstruirPayloadOtlp(traceId).ToByteArray());
        content.Headers.ContentType = new MediaTypeHeaderValue(OtlpContentType);

        var resp = await client.PostAsync("/api/v1/telemetry/traces", content);
        resp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Ingesta_EmiteEventoLiveAlGrupoDelProyecto()
    {
        // Arrange: proyecto + cliente SignalR del dueño suscrito a su grupo.
        var (projectId, ingestKey, ownerId) = await CrearProyectoAsync();

        await using var connection = HubConnectionForUser(ownerId);
        var received = new TaskCompletionSource<LiveTraceEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<LiveTraceEvent>(SignalRLiveTraceNotifier.EventName, ev => received.TrySetResult(ev));
        await connection.StartAsync();
        await connection.InvokeAsync("JoinProject", projectId);

        // Act: ingestamos un trace real.
        var traceIdBytes = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        await IngestarTraceAsync(ingestKey, traceIdBytes);

        // Assert: el cliente suscrito recibe el evento con el traceId y la duración correctos.
        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(received.Task, completed); // llegó dentro del tiempo

        var ev = await received.Task;
        Assert.Equal(Convert.ToHexString(traceIdBytes).ToLowerInvariant(), ev.TraceId);
        Assert.Equal(40, ev.TotalDurationMs);  // duración del span raíz
        Assert.Equal("OK", ev.Status);
        Assert.Equal(2, ev.Hops.Count);        // raíz + hop a la DB
    }

    [Fact]
    public async Task Ingesta_NoLlegaAClientesDeOtroProyecto()
    {
        // Arrange: dos proyectos, cada uno con su dueño y su conexión suscrita.
        var (projectA, keyA, ownerA) = await CrearProyectoAsync();
        var (projectB, _, ownerB) = await CrearProyectoAsync();

        await using var connA = HubConnectionForUser(ownerA);
        var receivedA = new TaskCompletionSource<LiveTraceEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        connA.On<LiveTraceEvent>(SignalRLiveTraceNotifier.EventName, ev => receivedA.TrySetResult(ev));
        await connA.StartAsync();
        await connA.InvokeAsync("JoinProject", projectA);

        await using var connB = HubConnectionForUser(ownerB);
        var receivedB = new TaskCompletionSource<LiveTraceEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        connB.On<LiveTraceEvent>(SignalRLiveTraceNotifier.EventName, ev => receivedB.TrySetResult(ev));
        await connB.StartAsync();
        await connB.InvokeAsync("JoinProject", projectB);

        // Act: ingestamos SOLO en el proyecto A.
        await IngestarTraceAsync(keyA, Enumerable.Range(0, 16).Select(i => (byte)i).ToArray());

        // Assert: A recibe (prueba que el evento se emite de verdad)...
        var completedA = await Task.WhenAny(receivedA.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(receivedA.Task, completedA);

        // ...y B NO recibe, aunque le demos margen (aislamiento por grupo).
        var completedB = await Task.WhenAny(receivedB.Task, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.NotSame(receivedB.Task, completedB);
    }

    // Construye un ExportTraceServiceRequest OTLP: raíz SERVER "GET /pedidos" (40ms) +
    // hijo CLIENT "SELECT pedidos" a postgresql (12ms). Igual forma que el test de T16.
    private static ExportTraceServiceRequest ConstruirPayloadOtlp(byte[] traceId)
    {
        var startNanos = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000UL;

        var rootSpan = new OtlpSpan
        {
            TraceId = ByteString.CopyFrom(traceId),
            SpanId = ByteString.CopyFrom(new byte[] { 1, 1, 1, 1, 1, 1, 1, 1 }),
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
}
