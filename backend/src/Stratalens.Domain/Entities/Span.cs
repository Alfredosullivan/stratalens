using Stratalens.Domain.Exceptions;

namespace Stratalens.Domain.Entities;

// Un Span es un tramo individual dentro de un Trace (Glosario de PRODUCT.md):
// una operación con su duración (ej. "Backend procesando la request", "SELECT users").
// Igual que Node/Edge, se construye validado: es imposible tener un Span con duración
// negativa, sin operación, o con una fecha que no esté en UTC.
// No vive suelto: pertenece siempre a un Trace, que es su aggregate root (ver Trace.cs).
public class Span
{
    // Solo lectura: un Span no muta tras crearse, igual que Node/Edge.
    public Guid Id { get; }
    public string SpanId { get; } = null!;      // id del span en OpenTelemetry (EF lo rellena al rehidratar)
    public string? ParentSpanId { get; }        // null si es el span raíz del trace
    public string SourceNode { get; } = null!;  // nodo/servicio que originó el span (EF lo rellena)
    public string? TargetNode { get; }          // nodo/servicio destino, si aplica
    public string Operation { get; } = null!;   // ej. "POST /api/login", "SELECT users" (EF lo rellena)
    public DateTime StartedAt { get; }          // siempre en UTC (ver constructor)
    public double DurationMs { get; }           // duración del tramo, en milisegundos (>= 0)
    public string Status { get; } = null!;      // ej. "OK", "ERROR" (EF lo rellena)

    // Constructor privado SOLO para EF Core (rehidratación desde la DB). Ver Edge.cs.
    private Span() { }

    // Constructor público que valida: es imposible obtener un Span en estado inválido.
    public Span(
        string spanId,
        string? parentSpanId,
        string sourceNode,
        string? targetNode,
        string operation,
        DateTime startedAt,
        double durationMs,
        string status)
    {
        if (string.IsNullOrWhiteSpace(spanId))
            throw new DomainException("Un Span debe tener un SpanId.");

        if (string.IsNullOrWhiteSpace(sourceNode))
            throw new DomainException("Un Span debe tener un SourceNode (quién lo originó).");

        if (string.IsNullOrWhiteSpace(operation))
            throw new DomainException("Un Span debe tener una Operation (ej. 'POST /api/login').");

        if (string.IsNullOrWhiteSpace(status))
            throw new DomainException("Un Span debe tener un Status (ej. 'OK', 'ERROR').");

        // Invariante: la duración de un tramo nunca puede ser negativa.
        if (durationMs < 0)
            throw new DomainException("La DurationMs de un Span no puede ser negativa.");

        // Invariante: StartedAt debe estar en UTC. Rechazamos Local/Unspecified en vez de
        // convertir: un DateTime con Kind=Unspecified no lleva huso, y ToUniversalTime()
        // asumiría la hora local del servidor, corrompiendo la duración/el orden en silencio.
        if (startedAt.Kind != DateTimeKind.Utc)
            throw new DomainException("StartedAt de un Span debe estar en UTC (DateTimeKind.Utc).");

        Id = Guid.NewGuid();
        SpanId = spanId;
        ParentSpanId = parentSpanId;
        SourceNode = sourceNode;
        TargetNode = targetNode;
        Operation = operation;
        StartedAt = startedAt;
        DurationMs = durationMs;
        Status = status;
    }

    // Un Span es raíz cuando no tiene padre. Lo usa Trace para validar la invariante
    // de "exactamente un span raíz", que solo tiene sentido mirando el conjunto.
    public bool IsRoot => ParentSpanId is null;
}
