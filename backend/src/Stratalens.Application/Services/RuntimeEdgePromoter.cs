using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Entities;
using Stratalens.Domain.Enums;

namespace Stratalens.Application.Services;

// Promueve los hops observados de un trace a Edges de RUNTIME (Confidence=100). Es la
// materialización de "Static Discovery vs Runtime Observability": un edge confirmado por
// un trace real deja de ser inferencia. Vive en Application porque decidir "qué hop
// confirma qué relación y con qué política de promoción" es lógica de negocio.
public class RuntimeEdgePromoter
{
    private readonly IGraphRepository _graph;

    public RuntimeEdgePromoter(IGraphRepository graph)
    {
        _graph = graph;
    }

    // Promueve los hops de UN trace (sus spans ya normalizados) sobre el grafo del proyecto.
    public async Task PromoteAsync(
        Guid projectId,
        string traceId,
        IReadOnlyList<TelemetrySpan> spans,
        CancellationToken ct = default)
    {
        var graph = await _graph.GetGraphAsync(projectId, ct);
        if (graph.Nodes.Count == 0)
            return; // sin grafo estático no hay nodos que confirmar

        var resolver = new GraphNodeResolver(graph.Nodes);
        var traceSource = $"trace:{traceId}"; // Source del edge = el trace que lo confirmó

        // Un trace puede repetir el mismo par (ej. varias queries a la DB): deduplicamos
        // para crear un solo edge de runtime por par origen→destino.
        var promoted = new HashSet<(Guid Source, Guid Target)>();

        foreach (var span in spans)
        {
            // Solo los hops con destino son una relación entre dos nodos (el span raíz
            // SERVER no tiene TargetNode).
            if (string.IsNullOrWhiteSpace(span.TargetNode))
                continue;

            var source = resolver.Resolve(span.SourceNode);
            var target = resolver.Resolve(span.TargetNode);

            // Fallback (Opción A): si cualquiera de los dos extremos no casa con un nodo
            // existente, NO se promueve — nunca inventamos un edge.
            if (source is null || target is null || source.Id == target.Id)
                continue;

            if (!promoted.Add((source.Id, target.Id)))
                continue;

            // Tipo del edge por la categoría del destino, coherente con el grafo estático
            // (Backend→DB = "SQL"; el resto se trata como llamada HTTP/REST).
            var type = target.Category == NodeCategory.Database ? "SQL" : "HTTP/REST";

            var existing = graph.Edges
                .Where(e => e.SourceNodeId == source.Id && e.TargetNodeId == target.Id)
                .ToList();

            // Idempotente: si ya hay un edge de runtime para el par, no repetimos trabajo.
            if (existing.Any(e => e.SourceType == EdgeSourceType.Runtime))
                continue;

            // Promoción (Decisión 2): el edge estático del mismo par deja de ser inferencia
            // → lo quitamos y dejamos solo el de runtime (no mostramos el par dos veces con
            // confianzas contradictorias).
            foreach (var stale in existing)
                await _graph.RemoveEdgeAsync(stale.Id, ct);

            var runtimeEdge = Edge.FromRuntimeTrace(projectId, source.Id, target.Id, type, traceSource);
            await _graph.AddEdgeAsync(runtimeEdge, ct);
        }
    }
}
