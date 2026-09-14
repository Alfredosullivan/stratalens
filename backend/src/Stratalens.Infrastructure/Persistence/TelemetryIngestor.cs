using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Entities;

namespace Stratalens.Infrastructure.Persistence;

// Implementación de ITelemetryIngestor con EF Core + PostgreSQL.
// Traduce los TelemetrySpan (evento normalizado de Application) a los aggregates
// Trace/Span del dominio y los persiste. A diferencia del grafo (que se reemplaza),
// la telemetría es ACUMULATIVA: cada ingesta agrega traces nuevos, no borra los previos.
public class TelemetryIngestor : ITelemetryIngestor
{
    private readonly StratalensDbContext _db;

    public TelemetryIngestor(StratalensDbContext db)
    {
        _db = db;
    }

    public async Task IngestAsync(Guid projectId, IReadOnlyList<TelemetrySpan> spans, CancellationToken ct = default)
    {
        // Agrupar por TraceId: cada grupo de spans es un Trace del dominio.
        // Trace.Create valida las invariantes (un único span raíz por trace); si el batch
        // no trae un trace completo, falla aquí — es la frontera del dominio haciendo su trabajo.
        // ASUNCIÓN DEL MVP: cada batch trae el trace completo (cierto para el demo de T17).
        var traces = spans
            .GroupBy(s => s.TraceId)
            .Select(group => Trace.Create(
                projectId,
                group.Key,
                group.Select(MapToSpan)))
            .ToList();

        _db.Traces.AddRange(traces);
        await _db.SaveChangesAsync(ct);
    }

    // Mapea el evento normalizado (Application) a la entidad de dominio (Domain).
    // El constructor de Span revalida sus invariantes (DurationMs >= 0, StartedAt en UTC),
    // así que ningún dato inválido llega a la DB aunque el emisor mande basura.
    private static Span MapToSpan(TelemetrySpan s) => new(
        s.SpanId,
        s.ParentSpanId,
        s.SourceNode,
        s.TargetNode,
        s.Operation,
        s.StartedAt,
        s.DurationMs,
        s.Status);
}
