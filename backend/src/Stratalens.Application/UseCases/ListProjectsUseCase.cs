using Stratalens.Application.Abstractions;
using Stratalens.Domain.Entities;

namespace Stratalens.Application.UseCases;

// Dashboard de "mis proyectos" (T28). Sin ownership check explícito: acá la ownership
// ES el filtro (WHERE OwnerUserId = ...), no una verificación posterior sobre un recurso
// puntual como en GetProjectUseCase.
public class ListProjectsUseCase
{
    private readonly IGraphRepository _graph;

    public ListProjectsUseCase(IGraphRepository graph)
    {
        _graph = graph;
    }

    public Task<IReadOnlyList<Project>> ExecuteAsync(Guid ownerUserId, CancellationToken ct = default) =>
        _graph.GetProjectsByOwnerAsync(ownerUserId, ct);
}
