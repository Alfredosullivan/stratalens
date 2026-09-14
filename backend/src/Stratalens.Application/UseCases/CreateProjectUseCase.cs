using Stratalens.Application.Abstractions;
using Stratalens.Domain.Entities;

namespace Stratalens.Application.UseCases;

// Crea un proyecto y le genera su clave de ingesta de telemetría.
// Devuelve la clave EN CLARO junto al proyecto: es la única vez que existe en claro
// (en reposo solo se guarda su hash), así que el caller la muestra una vez y no se
// puede recuperar después — hay que rotarla si se pierde.
public class CreateProjectUseCase
{
    private readonly IGraphRepository _graph;
    private readonly IIngestKeyService _ingestKeys;

    public CreateProjectUseCase(IGraphRepository graph, IIngestKeyService ingestKeys)
    {
        _graph = graph;
        _ingestKeys = ingestKeys;
    }

    public async Task<CreateProjectResult> ExecuteAsync(Guid ownerUserId, string name, CancellationToken ct = default)
    {
        var project = Project.CreateManual(ownerUserId, name);
        await _graph.AddProjectAsync(project, ct);

        // Generamos la clave y persistimos SOLO su hash.
        var key = _ingestKeys.Generate();
        await _graph.SetIngestKeyHashAsync(project.Id, key.Hash, ct);

        return new CreateProjectResult(project, key.PlainKey);
    }
}

// El proyecto creado + la clave de ingesta en claro (para mostrarla una única vez).
public record CreateProjectResult(Project Project, string IngestKey);
