using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Entities;
using Stratalens.Domain.Enums;

namespace Stratalens.Infrastructure.Analyzers;

// Primer analyzer de infraestructura (T30). A diferencia de los demás (C#/Express/React),
// no detecta detalle fino que haya que agregar después: "hay un Dockerfile" es un hecho
// binario, así que este analyzer entrega directamente el Node a nivel Sistema
// (Category=Infrastructure) que SystemGraphBuilder pasa tal cual al grafo final.
public class DockerAnalyzer : ILanguageAnalyzer
{
    public string Language => "docker";

    public bool CanAnalyze(IReadOnlyList<RepositoryFile> files) =>
        files.Any(IsDockerFile);

    // No necesita leer contenido ni el connector: la sola presencia del archivo ya es
    // evidencia suficiente (a diferencia de NodeExpressAnalyzer, acá no hay convención de
    // carpetas ambigua que confirmar).
    public Task<AnalysisResult> AnalyzeAsync(
        Guid projectId,
        RepositoryReference repo,
        IProviderConnector connector,
        IReadOnlyList<RepositoryFile> files,
        CancellationToken ct = default)
    {
        // Un solo Node aunque haya varios archivos (Dockerfile + docker-compose.yml): la
        // pregunta que responde el grafo es "¿se containeriza?", no "cuántos archivos".
        // Preferimos el Dockerfile como evidencia si están los dos: es la señal más directa.
        var evidence = files.FirstOrDefault(f => IsDockerfilePath(f.Path))
            ?? files.First(IsDockerFile);

        var node = new Node(
            projectId,
            "Docker",
            "Docker",
            NodeCategory.Infrastructure,
            new Dictionary<string, string> { ["source"] = evidence.Path });

        return Task.FromResult(new AnalysisResult(new List<Node> { node }, new List<Edge>()));
    }

    private static bool IsDockerFile(RepositoryFile file) =>
        file.Type == "blob" && (IsDockerfilePath(file.Path) || IsComposePath(file.Path));

    private static bool IsDockerfilePath(string path)
    {
        var name = Path.GetFileName(path);
        return name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Dockerfile.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsComposePath(string path)
    {
        var name = Path.GetFileName(path);
        return name.Equals("docker-compose.yml", StringComparison.OrdinalIgnoreCase)
            || name.Equals("docker-compose.yaml", StringComparison.OrdinalIgnoreCase)
            || name.Equals("compose.yml", StringComparison.OrdinalIgnoreCase)
            || name.Equals("compose.yaml", StringComparison.OrdinalIgnoreCase);
    }
}
