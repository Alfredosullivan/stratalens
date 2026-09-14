using Stratalens.Application.Exceptions;
using Stratalens.Application.Models;

namespace Stratalens.Application.UseCases;

// Re-analiza un proyecto ya conectado a un repo (T27). Reusa GetProjectUseCase para el
// ownership check (mismo patrón que CreateProjectFromRepositoryUseCase reusando
// AnalyzeRepositoryUseCase: un use case orquestando otro, no reimplementando su lógica).
public class ReanalyzeProjectUseCase
{
    private readonly GetProjectUseCase _getProject;
    private readonly AnalyzeRepositoryUseCase _analyze;

    public ReanalyzeProjectUseCase(GetProjectUseCase getProject, AnalyzeRepositoryUseCase analyze)
    {
        _getProject = getProject;
        _analyze = analyze;
    }

    public async Task<ProjectGraph> ExecuteAsync(Guid projectId, Guid currentUserId, CancellationToken ct = default)
    {
        // 404 si no existe o es de otro usuario (el mismo NotFoundException que el resto).
        var project = await _getProject.ExecuteAsync(projectId, currentUserId, ct);

        if (project.RepositoryOwner is null)
        {
            // Proyecto manual (T24: CreateManual deja los 3 campos en null): no hay nada
            // que re-analizar. Mismo 404 que "no existe/no es tuyo" — no distinguimos el
            // motivo en la respuesta HTTP, igual que el resto de los endpoints de la Api.
            throw new NotFoundException($"El proyecto {projectId} no tiene un repositorio conectado.");
        }

        // All-or-nothing (invariante de Project, T24): si RepositoryOwner no es null,
        // RepositoryName y RepositoryReference tampoco lo son.
        var reference = new RepositoryReference(
            project.RepositoryOwner, project.RepositoryName!, project.RepositoryReference!);

        return await _analyze.ExecuteAsync(project.Id, reference, ct);
    }
}
