using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Application.Services;

namespace Stratalens.Application.UseCases;

// Caso de uso de ingesta: valida que el proyecto existe, persiste los spans y, por cada
// trace del batch, (1) emite el evento LIVE por SignalR (T19) y (2) promueve los hops
// observados a edges de runtime en el grafo (T20). La emisión usa el puerto
// ILiveTraceNotifier (Application no conoce SignalR); la promoción usa RuntimeEdgePromoter.
public class IngestTelemetryUseCase
{
    private readonly IGraphRepository _graph;
    private readonly ITelemetryIngestor _ingestor;
    private readonly ILiveTraceNotifier _liveNotifier;
    private readonly RuntimeEdgePromoter _edgePromoter;

    public IngestTelemetryUseCase(
        IGraphRepository graph,
        ITelemetryIngestor ingestor,
        ILiveTraceNotifier liveNotifier,
        RuntimeEdgePromoter edgePromoter)
    {
        _graph = graph;
        _ingestor = ingestor;
        _liveNotifier = liveNotifier;
        _edgePromoter = edgePromoter;
    }

    public async Task ExecuteAsync(
        Guid projectId,
        IReadOnlyList<TelemetrySpan> spans,
        CancellationToken ct = default)
    {
        _ = await _graph.GetProjectAsync(projectId, ct)
            ?? throw new InvalidOperationException($"El proyecto {projectId} no existe.");

        // Primero persistimos: solo notificamos/promovemos traces REALES ya guardados.
        await _ingestor.IngestAsync(projectId, spans, ct);

        // Agrupamos una sola vez por trace (mismo agrupado por TraceId que usa el ingestor).
        foreach (var group in spans.GroupBy(s => s.TraceId))
        {
            var traceSpans = group.ToList();

            // (1) Evento LIVE al mapa abierto (T19).
            await _liveNotifier.TraceIngestedAsync(projectId, BuildLiveEvent(group.Key, traceSpans), ct);

            // (2) Promoción de los hops a edges de runtime en el grafo (T20).
            await _edgePromoter.PromoteAsync(projectId, group.Key, traceSpans, ct);
        }
    }

    // Proyecta los spans de UN trace a su evento LIVE.
    private static LiveTraceEvent BuildLiveEvent(string traceId, IReadOnlyList<TelemetrySpan> spans)
    {
        // Orden temporal: así los hops salen en el orden del recorrido real.
        var ordered = spans.OrderBy(s => s.StartedAt).ToList();

        // El span raíz (sin padre) define la duración total y el status del trace, igual
        // que el DTO de lectura (GetProjectTraces). Fallback al primero por si el batch no
        // trae raíz explícita (la ingesta ya lo habría rechazado antes).
        var root = ordered.FirstOrDefault(s => s.ParentSpanId is null) ?? ordered[0];

        var hops = ordered
            .Select(s => new LiveTraceHop(
                s.SourceNode, s.TargetNode, s.Operation, s.DurationMs, s.Status))
            .ToList();

        return new LiveTraceEvent(traceId, root.DurationMs, root.Status, hops);
    }
}
