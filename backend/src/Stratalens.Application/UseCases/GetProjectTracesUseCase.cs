using Stratalens.Application.Abstractions;
using Stratalens.Application.Exceptions;
using Stratalens.Domain.Entities;

namespace Stratalens.Application.UseCases;

// Devuelve los traces persistidos de un proyecto, aplicando el OWNERSHIP CHECK.
//
// Mismo patrón que GetProjectGraphUseCase: el check de "solo el dueño ve sus traces"
// es regla de negocio y vive aquí, no en el controller. El controller solo traduce la
// NotFoundException a un 404 (sin revelar la existencia de proyectos ajenos).
public class GetProjectTracesUseCase
{
    private readonly IGraphRepository _graph;

    public GetProjectTracesUseCase(IGraphRepository graph)
    {
        _graph = graph;
    }

    public async Task<IReadOnlyList<Trace>> ExecuteAsync(Guid projectId, Guid currentUserId, CancellationToken ct = default)
    {
        var project = await _graph.GetProjectAsync(projectId, ct);

        if (project is null || project.OwnerUserId != currentUserId)
        {
            throw new NotFoundException($"El proyecto {projectId} no existe o no es accesible.");
        }

        return await _graph.GetTracesAsync(projectId, ct);
    }
}
