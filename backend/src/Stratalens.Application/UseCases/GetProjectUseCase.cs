using Stratalens.Application.Abstractions;
using Stratalens.Application.Exceptions;
using Stratalens.Domain.Entities;

namespace Stratalens.Application.UseCases;

// Devuelve un proyecto aplicando el OWNERSHIP CHECK, igual que GetProjectGraphUseCase.
// El check vive en Application (no en el controller) porque "un usuario solo ve sus
// proyectos" es una regla de negocio. El controller solo traduce la NotFoundException
// a un 404 (sin revelar la existencia de proyectos ajenos).
public class GetProjectUseCase
{
    private readonly IGraphRepository _graph;

    public GetProjectUseCase(IGraphRepository graph)
    {
        _graph = graph;
    }

    public async Task<Project> ExecuteAsync(Guid projectId, Guid currentUserId, CancellationToken ct = default)
    {
        var project = await _graph.GetProjectAsync(projectId, ct);

        // 404 tanto si no existe como si es de otro usuario (misma respuesta para ambos).
        if (project is null || project.OwnerUserId != currentUserId)
        {
            throw new NotFoundException($"El proyecto {projectId} no existe o no es accesible.");
        }

        return project;
    }
}
