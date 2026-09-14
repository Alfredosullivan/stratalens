using Stratalens.Application.Abstractions;
using Stratalens.Application.Exceptions;
using Stratalens.Application.Models;
using Stratalens.Domain.Entities;

namespace Stratalens.Application.UseCases;

// Crea un Project a partir de un repo elegido por el usuario y lo analiza de inmediato
// (T26: ejecución síncrona, sin colas). Reusa AnalyzeRepositoryUseCase entero en vez de
// duplicar el pipeline: esto ES la orquestación de "crear + analizar", no una reimplementación.
public class CreateProjectFromRepositoryUseCase
{
    private readonly IGraphRepository _graph;
    private readonly IProviderConnector _connector;
    private readonly IIngestKeyService _ingestKeys;
    private readonly AnalyzeRepositoryUseCase _analyze;

    public CreateProjectFromRepositoryUseCase(
        IGraphRepository graph,
        IProviderConnector connector,
        IIngestKeyService ingestKeys,
        AnalyzeRepositoryUseCase analyze)
    {
        _graph = graph;
        _connector = connector;
        _ingestKeys = ingestKeys;
        _analyze = analyze;
    }

    public async Task<CreateProjectResult> ExecuteAsync(
        Guid ownerUserId, string repositoryOwner, string repositoryName, CancellationToken ct = default)
    {
        // Resolvemos contra GitHub PRIMERO: así "existe/es tuyo" y el casing canónico
        // (owner/name tal como los devuelve GitHub) salen de la misma fuente de verdad,
        // y el chequeo de duplicados de abajo nunca compara contra lo que el usuario
        // tipeó (evita falsos negativos por mayúsculas/minúsculas).
        var repos = await _connector.ListRepositoriesAsync(ct);
        var summary = repos.FirstOrDefault(r =>
            string.Equals(r.Owner, repositoryOwner, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.Name, repositoryName, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException($"El repositorio {repositoryOwner}/{repositoryName} no existe o no es accesible.");

        // "Mejor esfuerzo": evita correr el análisis completo si ya existe. La garantía
        // real ante una condición de carrera es el índice único en Infrastructure.
        if (await _graph.ExistsProjectForRepositoryAsync(ownerUserId, summary.Owner, summary.Name, ct))
        {
            throw new ConflictException($"Ya existe un proyecto para {summary.Owner}/{summary.Name}.");
        }

        var project = Project.CreateFromRepository(ownerUserId, summary.Owner, summary.Name, summary.DefaultBranch);
        await _graph.AddProjectAsync(project, ct);

        // Misma clave de ingesta que un proyecto manual: recibir telemetría no depende
        // de cómo se creó el proyecto.
        var key = _ingestKeys.Generate();
        await _graph.SetIngestKeyHashAsync(project.Id, key.Hash, ct);

        var reference = new RepositoryReference(summary.Owner, summary.Name, summary.DefaultBranch);
        await _analyze.ExecuteAsync(project.Id, reference, ct);

        return new CreateProjectResult(project, key.PlainKey);
    }
}
