namespace Stratalens.Application.Models;

// Un span de OpenTelemetry ya normalizado al vocabulario del grafo.
// Es el "evento normalizado" (Contexto maestro, sección 21) que alimenta el modo LIVE:
// distintas fuentes de telemetría pueden traducirse a esta forma común.
public record TelemetrySpan(
    string TraceId,
    string SpanId,
    string? ParentSpanId,  // null si es el span raíz del trace
    string SourceNode,     // nodo/servicio que originó el span
    string? TargetNode,    // nodo/servicio destino, si aplica
    string Operation,      // ej. "POST /api/login", "SELECT users"
    DateTime StartedAt,    // en UTC
    double DurationMs,
    string Status);        // ej. "OK", "ERROR"
