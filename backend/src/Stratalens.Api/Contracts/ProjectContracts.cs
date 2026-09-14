namespace Stratalens.Api.Contracts;

// DTOs explícitos de la Api. Nunca exponemos entidades de EF/Domain directamente
// en las respuestas (regla de RULES.md): así la forma del contrato HTTP no queda
// atada a cómo persistimos internamente.

public record CreateProjectRequest(string Name);

// T26: crea el Project a partir de un repo elegido de la lista de T25 (Owner/Name tal
// como los devuelve GET /github/repositories).
public record CreateProjectFromRepositoryRequest(string Owner, string Name);

// RepositoryOwner/RepositoryName nullable: null en un proyecto manual (T24), presentes
// si viene de GitHub — el dato que el dashboard de T28 necesita para distinguir ambos.
public record ProjectDto(Guid Id, string Name, DateTime CreatedAt, string? RepositoryOwner, string? RepositoryName);

// Respuesta de crear proyecto: incluye la clave de ingesta EN CLARO. Es la única vez
// que se devuelve (en reposo solo guardamos su hash); el cliente debe guardarla.
public record CreateProjectResponse(Guid Id, string Name, DateTime CreatedAt, string IngestKey);

// Respuesta del grafo: plana (nodos + edges), lista para consumirse en React Flow.
// Nunca exponemos las entidades Node/Edge de dominio directamente (regla de RULES.md).
public record GraphResponse(IReadOnlyList<GraphNodeDto> Nodes, IReadOnlyList<GraphEdgeDto> Edges);

public record GraphNodeDto(
    Guid Id,
    string Name,
    string Type,
    string Category,
    IReadOnlyDictionary<string, string> Metadata);

// Source/Target son los ids de los nodos (nombres que React Flow espera en un edge).
// SourceFile es el ORIGEN de detección (archivo/config), que es cosa distinta.
public record GraphEdgeDto(
    Guid Id,
    Guid Source,
    Guid Target,
    string Type,
    string SourceFile,
    int Confidence,
    string SourceType);
