using Microsoft.AspNetCore.SignalR;
using Stratalens.Api.Hubs;
using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;

namespace Stratalens.Api.Realtime;

// Adaptador (Ports & Adapters) del puerto ILiveTraceNotifier sobre SignalR.
// Vive en Api porque es la única capa que puede ver ArchitectureMapHub sin invertir
// la dirección de dependencias (Infrastructure → Application, nunca → Api).
// Usa IHubContext para emitir DESDE FUERA del hub (el hub solo maneja conexiones entrantes).
public class SignalRLiveTraceNotifier : ILiveTraceNotifier
{
    // Nombre del evento que el cliente escucha: connection.on("TraceIngested", ...).
    // Centralizado para que cliente (tests/frontend) y servidor usen el mismo string.
    public const string EventName = "TraceIngested";

    private readonly IHubContext<ArchitectureMapHub> _hub;

    public SignalRLiveTraceNotifier(IHubContext<ArchitectureMapHub> hub)
    {
        _hub = hub;
    }

    public Task TraceIngestedAsync(Guid projectId, LiveTraceEvent liveEvent, CancellationToken ct = default) =>
        // Solo al grupo del proyecto: el evento nunca llega a mapas de otros proyectos.
        _hub.Clients
            .Group(ArchitectureMapHub.GroupName(projectId))
            .SendAsync(EventName, liveEvent, ct);
}
