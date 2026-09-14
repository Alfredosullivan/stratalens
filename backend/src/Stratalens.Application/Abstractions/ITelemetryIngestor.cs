using Stratalens.Application.Models;

namespace Stratalens.Application.Abstractions;

// Ingesta de telemetría: recibe spans OTLP ya normalizados y los persiste,
// para alimentar el modo LIVE. Detrás puede haber PostgreSQL (MVP) o, más
// adelante, un backend OpenTelemetry real (Tempo/Jaeger) sin tocar Application.
public interface ITelemetryIngestor
{
    Task IngestAsync(Guid projectId, IReadOnlyList<TelemetrySpan> spans, CancellationToken ct = default);
}
