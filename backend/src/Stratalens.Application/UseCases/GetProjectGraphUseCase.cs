using Stratalens.Application.Abstractions;
using Stratalens.Application.Exceptions;
using Stratalens.Application.Models;

namespace Stratalens.Application.UseCases;

// Devuelve el grafo de un proyecto, aplicando el OWNERSHIP CHECK.
//
// El check vive aquí (Application) y no en el controller a propósito: "un usuario solo
// puede ver sus propios proyectos" es una regla de negocio, no un detalle de HTTP. El
// controller solo traduce la NotFoundException a un 404.
public class GetProjectGraphUseCase
{
    private readonly IGraphRepository _graph;

    public GetProjectGraphUseCase(IGraphRepository graph)
    {
        _graph = graph;
    }

    public async Task<ProjectGraph> ExecuteAsync(Guid projectId, Guid currentUserId, CancellationToken ct = default)
    {
        var project = await _graph.GetProjectAsync(projectId, ct);

        // 404 tanto si no existe como si es de otro usuario: no revelamos la existencia
        // de proyectos ajenos (misma respuesta para ambos casos).
        if (project is null || project.OwnerUserId != currentUserId)
        {
            throw new NotFoundException($"El proyecto {projectId} no existe o no es accesible.");
        }

        return await _graph.GetGraphAsync(projectId, ct);
    }
}
