using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Application.Services;

namespace Stratalens.Application.UseCases;

// Orquesta el análisis estático de un repositorio conectado:
//   1. Verifica que el proyecto existe.
//   2. Lee el árbol de archivos vía el proveedor (GitHub).
//   3. Corre los analyzers que apliquen al stack detectado.
//   4. Agrega la detección fina en el grafo de arquitectura (Frontend/Backend/DB) y lo persiste.
//
// Fíjate: este caso de uso NO conoce ninguna implementación concreta. No sabe que
// detrás hay PostgreSQL, ni GitHub, ni Roslyn. Solo depende de las abstracciones
// que recibe por constructor (D de SOLID: depende de abstracciones, no de detalles).
public class AnalyzeRepositoryUseCase
{
    private readonly IGraphRepository _graph;
    private readonly IProviderConnector _connector;
    private readonly IEnumerable<ILanguageAnalyzer> _analyzers;
    private readonly SystemGraphBuilder _builder;

    public AnalyzeRepositoryUseCase(
        IGraphRepository graph,
        IProviderConnector connector,
        IEnumerable<ILanguageAnalyzer> analyzers,
        SystemGraphBuilder builder)
    {
        _graph = graph;
        _connector = connector;
        _analyzers = analyzers;
        _builder = builder;
    }

    public async Task<ProjectGraph> ExecuteAsync(
        Guid projectId,
        RepositoryReference repo,
        CancellationToken ct = default)
    {
        // NOTA: por ahora un proyecto inexistente lanza InvalidOperationException.
        // Cuando montemos el middleware de errores de la Api (T11) se traducirá a un 404
        // usando una excepción de aplicación tipada; se deja así para no adelantar código.
        _ = await _graph.GetProjectAsync(projectId, ct)
            ?? throw new InvalidOperationException($"El proyecto {projectId} no existe.");

        var files = await _connector.GetFileTreeAsync(repo, ct);

        // Cada analyzer aporta la detección fina de su stack, indexada por lenguaje.
        // Añadir un stack nuevo no cambia este bucle: simplemente aparece otro analyzer.
        var resultsByLanguage = new Dictionary<string, AnalysisResult>();
        foreach (var analyzer in _analyzers.Where(a => a.CanAnalyze(files)))
        {
            resultsByLanguage[analyzer.Language] =
                await analyzer.AnalyzeAsync(projectId, repo, _connector, files, ct);
        }

        // El builder agrega esa detección en el grafo grueso (nivel Application/System).
        var graph = _builder.Build(projectId, files, resultsByLanguage);

        await _graph.SaveGraphAsync(projectId, graph, ct);

        return await _graph.GetGraphAsync(projectId, ct);
    }
}
