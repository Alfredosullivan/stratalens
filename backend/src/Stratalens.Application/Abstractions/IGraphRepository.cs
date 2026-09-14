using Stratalens.Application.Models;
using Stratalens.Domain.Entities;

namespace Stratalens.Application.Abstractions;

// Contrato de acceso a la persistencia del grafo. Application define QUÉ operaciones
// existen; Infrastructure decide CÓMO (EF Core + PostgreSQL, en T5).
// Ni Application ni Domain saben qué base de datos hay detrás: si mañana cambia,
// solo se reimplementa esta interfaz en Infrastructure.
public interface IGraphRepository
{
    Task<Project?> GetProjectAsync(Guid projectId, CancellationToken ct = default);

    // Dashboard de "mis proyectos" (T28). Ordenado por CreatedAt descendente: el más
    // reciente primero, el orden natural para volver a encontrar lo que analizaste hoy.
    Task<IReadOnlyList<Project>> GetProjectsByOwnerAsync(Guid ownerUserId, CancellationToken ct = default);

    Task AddProjectAsync(Project project, CancellationToken ct = default);

    // Reemplaza el grafo de un proyecto con el resultado de un análisis.
    // Idempotente por proyecto: correr el análisis dos veces no duplica nodos.
    Task SaveGraphAsync(Guid projectId, AnalysisResult graph, CancellationToken ct = default);

    // Recupera el grafo persistido de un proyecto, para mostrarlo (endpoint de T11).
    Task<ProjectGraph> GetGraphAsync(Guid projectId, CancellationToken ct = default);

    // Guarda el hash de la clave de ingesta de un proyecto (la clave en claro nunca llega aquí).
    Task SetIngestKeyHashAsync(Guid projectId, string ingestKeyHash, CancellationToken ct = default);

    // Devuelve el Id del proyecto cuya clave de ingesta coincide con el hash dado, o null.
    // Lo usa la autenticación de ingesta para resolver a qué proyecto pertenece la clave.
    Task<Guid?> GetProjectIdByIngestKeyHashAsync(string ingestKeyHash, CancellationToken ct = default);

    // Recupera los traces persistidos de un proyecto, con sus spans (para el endpoint de
    // lectura de T17). Cada Trace es un aggregate root con su colección de Span.
    Task<IReadOnlyList<Trace>> GetTracesAsync(Guid projectId, CancellationToken ct = default);

    // Añade UN edge sin tocar el resto del grafo (aditivo, a diferencia de SaveGraphAsync
    // que reemplaza todo). Lo usa la promoción de edges de runtime (T20).
    Task AddEdgeAsync(Edge edge, CancellationToken ct = default);

    // Elimina un edge por su Id. Lo usa la promoción para superponer un edge de runtime
    // sobre el estático del mismo par (un edge confirmado deja de ser inferencia).
    Task RemoveEdgeAsync(Guid edgeId, CancellationToken ct = default);

    // True si el usuario ya tiene un Project para ese repo exacto (T26: un usuario no
    // puede tener dos proyectos para el mismo owner/name). Chequeo "mejor esfuerzo" para
    // dar un 409 rápido y claro; el índice único en Infrastructure es el backstop real
    // ante una condición de carrera.
    Task<bool> ExistsProjectForRepositoryAsync(
        Guid ownerUserId, string repositoryOwner, string repositoryName, CancellationToken ct = default);
}
