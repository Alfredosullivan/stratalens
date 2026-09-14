using Stratalens.Application.Models;

namespace Stratalens.Application.Abstractions;

// Analyzer de un stack concreto (C#/Roslyn, React-TS/Node...). Es un sistema de
// plugins: cada implementación vive en Infrastructure y detecta los nodos/edges
// de su lenguaje. Añadir soporte para un stack nuevo = añadir un ILanguageAnalyzer,
// sin tocar el pipeline ni el resto del sistema (O de SOLID: abierto a extensión).
public interface ILanguageAnalyzer
{
    // Identificador del stack que analiza (ej. "csharp", "typescript").
    string Language { get; }

    // Decide si este analyzer aplica al repo mirando el árbol de archivos
    // (ej. hay .csproj → C#; hay package.json con React → TS).
    bool CanAnalyze(IReadOnlyList<RepositoryFile> files);

    // Analiza el repo y devuelve los nodos/edges detectados.
    // Recibe el connector para leer bajo demanda solo los archivos que necesita,
    // en vez de cargar todo el repositorio en memoria.
    Task<AnalysisResult> AnalyzeAsync(
        Guid projectId,
        RepositoryReference repo,
        IProviderConnector connector,
        IReadOnlyList<RepositoryFile> files,
        CancellationToken ct = default);
}
