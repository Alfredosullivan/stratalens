namespace Stratalens.Application.Models;

// Evento del modo LIVE que viaja al frontend por SignalR cuando se ingesta un trace.
// Es la proyección del "evento normalizado" (Contexto maestro, sección 21) lista para
// animar el mapa: el recorrido de la request (hops ordenados), su duración total y su status.
public record LiveTraceEvent(
    string TraceId,
    double TotalDurationMs,  // duración del span raíz: abarca toda la request
    string Status,           // status del span raíz (ej. "OK", "ERROR")
    IReadOnlyList<LiveTraceHop> Hops);

// Un salto del recorrido observado: de un nodo/servicio al siguiente (o a la DB).
// Ordenados por tiempo de inicio para reflejar el flujo real de la request.
public record LiveTraceHop(
    string SourceNode,
    string? TargetNode,   // null si el span no representa una llamada saliente
    string Operation,
    double DurationMs,
    string Status);
