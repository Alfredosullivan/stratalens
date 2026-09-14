using Stratalens.Domain.Enums;
using Stratalens.Domain.Exceptions;

namespace Stratalens.Domain.Entities;

// Representa una relación dirigida entre dos nodos del grafo de arquitectura.
// Sus invariantes (RULES.md) hacen imposible construir un Edge en estado inválido:
// no existe ningún camino público que produzca una relación sin origen o con
// una confianza incoherente respecto a cómo fue detectada.
public class Edge
{
    // Propiedades de solo lectura: una vez creado, un Edge no muta.
    // Esto protege sus invariantes durante toda su vida (no basta con validar al nacer
    // si luego alguien puede reescribir Confidence con un setter público).
    public Guid Id { get; }
    public Guid ProjectId { get; }
    public Guid SourceNodeId { get; }   // nodo origen (extremo del grafo)
    public Guid TargetNodeId { get; }   // nodo destino (extremo del grafo)
    public string Type { get; } = null!;    // ej. "HTTP/REST", "SQL", "Cache" (EF lo rellena al rehidratar)
    public string Source { get; } = null!;  // origen de detección: archivo/config/trace (EF lo rellena)
    public int Confidence { get; }      // 0..100
    public EdgeSourceType SourceType { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

    // Constructor privado SOLO para EF Core: lo usa al rehidratar desde la DB.
    // No valida ni genera Id/valores nuevos; EF rellena las propiedades por sus
    // backing fields después de construir. Nadie más debe usarlo → sigue siendo
    // imposible crear un Edge inválido desde fuera.
    private Edge() { }

    // Constructor privado: la única forma de crear un Edge NUEVO es a través de los
    // factory methods de abajo, que garantizan las invariantes según el origen.
    private Edge(
        Guid projectId,
        Guid sourceNodeId,
        Guid targetNodeId,
        string type,
        string source,
        int confidence,
        EdgeSourceType sourceType,
        IReadOnlyDictionary<string, string>? metadata)
    {
        Id = Guid.NewGuid();
        ProjectId = projectId;
        SourceNodeId = sourceNodeId;
        TargetNodeId = targetNodeId;
        Type = type;
        Source = source;
        Confidence = confidence;
        SourceType = sourceType;
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    // Crea un Edge detectado por un analyzer estático (código/configuración).
    // Nunca es un hecho confirmado, por eso su Confidence debe ser < 100.
    public static Edge FromStaticAnalysis(
        Guid projectId,
        Guid sourceNodeId,
        Guid targetNodeId,
        string type,
        string source,
        int confidence,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        // Invariante 1: toda relación detectada debe poder explicarse (de dónde salió).
        if (string.IsNullOrWhiteSpace(source))
            throw new DomainException("Un Edge estático debe tener un Source (archivo/config de origen).");

        if (string.IsNullOrWhiteSpace(type))
            throw new DomainException("Un Edge debe tener un Type (ej. 'HTTP/REST', 'SQL').");

        // Invariante 2: un edge inferido nunca se presenta como certeza absoluta.
        if (confidence < 0 || confidence >= 100)
            throw new DomainException("La confianza de un Edge estático debe estar entre 0 y 99.");

        return new Edge(projectId, sourceNodeId, targetNodeId, type, source, confidence, EdgeSourceType.Static, metadata);
    }

    // Crea un Edge confirmado por un trace real de OpenTelemetry (hecho observado).
    // Al provenir de una observación runtime, su Confidence es 100.
    // Invariante 3: este es el ÚNICO método que produce Confidence = 100, así que
    // "Confidence = 100 sólo puede originarse desde runtime" se cumple por construcción.
    public static Edge FromRuntimeTrace(
        Guid projectId,
        Guid sourceNodeId,
        Guid targetNodeId,
        string type,
        string traceSource,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(traceSource))
            throw new DomainException("Un Edge de runtime debe referenciar el trace que lo confirmó.");

        if (string.IsNullOrWhiteSpace(type))
            throw new DomainException("Un Edge debe tener un Type (ej. 'HTTP/REST', 'SQL').");

        return new Edge(projectId, sourceNodeId, targetNodeId, type, traceSource, 100, EdgeSourceType.Runtime, metadata);
    }
}
