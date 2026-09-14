using Stratalens.Domain.Exceptions;

namespace Stratalens.Domain.Entities;

// Un Trace es la secuencia de spans que representa el recorrido completo de una request
// por el sistema (Glosario de PRODUCT.md), identificada por su traceId de OpenTelemetry.
// Es el AGGREGATE ROOT de la telemetría: posee sus Spans y es el único lugar donde se
// valida la invariante que abarca al conjunto — "un Trace tiene exactamente un span raíz".
// Un Span aislado no puede comprobar esa regla por sí solo; por eso toda construcción de
// un trace nuevo pasa por el factory Create y nadie puede agregar spans por fuera.
public class Trace
{
    // Solo lectura: un Trace no muta tras crearse, igual que Node/Edge.
    public Guid Id { get; }
    public Guid ProjectId { get; }
    public string TraceId { get; } = null!;   // traceId de OpenTelemetry (EF lo rellena al rehidratar)

    // Colección encapsulada: se expone como IReadOnlyList para que nadie de fuera pueda
    // agregar/quitar spans y saltarse la invariante del raíz. El campo es el que EF Core
    // rehidrata al leer de la DB.
    private readonly List<Span> _spans = new();
    public IReadOnlyList<Span> Spans => _spans;

    // Constructor privado SOLO para EF Core (rehidratación desde la DB). Ver Edge.cs.
    private Trace() { }

    // Constructor privado: la única forma de crear un Trace NUEVO es a través del factory
    // Create, que garantiza las invariantes del agregado antes de construir.
    private Trace(Guid projectId, string traceId, List<Span> spans)
    {
        Id = Guid.NewGuid();
        ProjectId = projectId;
        TraceId = traceId;
        _spans = spans;
    }

    // Única forma de crear un Trace nuevo. Valida las invariantes del agregado.
    public static Trace Create(Guid projectId, string traceId, IEnumerable<Span> spans)
    {
        if (projectId == Guid.Empty)
            throw new DomainException("Un Trace debe pertenecer a un Project (ProjectId válido).");

        if (string.IsNullOrWhiteSpace(traceId))
            throw new DomainException("Un Trace debe tener un TraceId (el de OpenTelemetry).");

        if (spans is null)
            throw new DomainException("Un Trace debe tener al menos un Span.");

        var spanList = spans.ToList();

        // Invariante: un trace sin spans no representa ningún recorrido.
        if (spanList.Count == 0)
            throw new DomainException("Un Trace debe tener al menos un Span.");

        // Invariante clave (T14 / RULES.md): exactamente un span raíz (ParentSpanId nulo).
        // Solo puede comprobarse mirando todos los spans juntos → por eso vive en el
        // aggregate root y no en el constructor de Span.
        var rootCount = spanList.Count(s => s.IsRoot);
        if (rootCount != 1)
            throw new DomainException(
                $"Un Trace debe tener exactamente un span raíz (ParentSpanId nulo); encontrados: {rootCount}.");

        return new Trace(projectId, traceId, spanList);
    }
}
