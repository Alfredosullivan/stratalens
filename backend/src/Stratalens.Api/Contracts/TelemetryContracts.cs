namespace Stratalens.Api.Contracts;

// DTOs de lectura de traces (T17). Nunca exponemos las entidades de dominio Trace/Span
// directamente (regla de RULES.md): la forma del contrato HTTP es independiente del modelo.

// Un trace con sus spans y su duración total (la del span raíz, que abarca toda la request).
public record TraceDto(
    string TraceId,
    double DurationMs,
    IReadOnlyList<SpanDto> Spans);

public record SpanDto(
    string SpanId,
    string? ParentSpanId,
    string SourceNode,
    string? TargetNode,
    string Operation,
    DateTime StartedAt,
    double DurationMs,
    string Status);
