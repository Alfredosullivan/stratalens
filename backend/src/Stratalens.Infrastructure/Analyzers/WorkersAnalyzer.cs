using System.Text.Json;
using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Entities;
using Stratalens.Domain.Enums;

namespace Stratalens.Infrastructure.Analyzers;

// Analyzer de workers / procesamiento en background (T37, Fase 6). Hermano de
// MessageBusAnalyzer/AuthAnalyzer: detecta por dependencia REAL (nunca por convención — el
// riesgo de falso positivo es alto acá, "background job" se implementa de mil formas), pero
// SOLO librerías DEDICADAS de jobs/colas. A diferencia del bus, el nodo se llama siempre
// "Workers" (el insight es "hay procesamiento en background", no cuál lib) y la librería
// concreta se guarda en metadata para el panel de detalle.
public class WorkersAnalyzer : ILanguageAnalyzer
{
    // Librerías npm dedicadas de colas de jobs / workers. Se excluyen a propósito los cron
    // triviales (node-cron, etc.): se usan para tareas menores en cualquier web app → falso
    // positivo alto. Solo frameworks de procesamiento en background de verdad.
    private static readonly (string Package, string Framework)[] NpmWorkers =
    {
        ("bullmq", "BullMQ"),
        ("bull", "Bull"),
        ("bee-queue", "Bee-Queue"),
        ("agenda", "Agenda"),
    };

    // Frameworks .NET dedicados de background jobs / scheduling.
    private static readonly (string Package, string Framework)[] DotNetWorkers =
    {
        ("Hangfire", "Hangfire"),
        ("Quartz", "Quartz"),
        ("Coravel", "Coravel"),
    };

    public string Language => "workers";

    public bool CanAnalyze(IReadOnlyList<RepositoryFile> files) =>
        files.Any(IsManifest);

    public async Task<AnalysisResult> AnalyzeAsync(
        Guid projectId,
        RepositoryReference repo,
        IProviderConnector connector,
        IReadOnlyList<RepositoryFile> files,
        CancellationToken ct = default)
    {
        // Primer manifiesto con un framework de workers gana (nodo binario a nivel Sistema).
        foreach (var file in files.Where(IsManifest))
        {
            var content = await connector.GetFileContentAsync(repo, file.Path, ct);
            var framework = ResolveFramework(file.Path, content);

            if (framework is not null)
            {
                var node = new Node(
                    projectId,
                    "Workers",       // Name genérico (concepto arquitectónico, como en Archify)
                    "Workers",       // Type = tipo técnico (para el color del frontend)
                    NodeCategory.Worker,
                    new Dictionary<string, string>
                    {
                        ["source"] = file.Path,
                        ["framework"] = framework, // la lib concreta, para el panel de detalle
                    });

                return new AnalysisResult(new List<Node> { node }, new List<Edge>());
            }
        }

        // Ninguna evidencia: no inventamos workers que no están (RULES.md).
        return new AnalysisResult(new List<Node>(), new List<Edge>());
    }

    // Devuelve el framework de workers declarado en el manifiesto, o null si no hay ninguno.
    private static string? ResolveFramework(string path, string content)
    {
        if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var (package, framework) in DotNetWorkers)
            {
                if (content.Contains(package, StringComparison.OrdinalIgnoreCase))
                {
                    return framework;
                }
            }
            return null;
        }

        return ResolveNpmFramework(content);
    }

    private static string? ResolveNpmFramework(string packageJsonContent)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(packageJsonContent);
        }
        catch (JsonException)
        {
            // Un package.json ilegible no es evidencia — y recorremos varios, uno roto no debe
            // tumbar el análisis (igual que AuthAnalyzer/MessageBusAnalyzer).
            return null;
        }

        using (doc)
        {
            foreach (var (package, framework) in NpmWorkers)
            {
                if (HasKey(doc.RootElement, "dependencies", package)
                    || HasKey(doc.RootElement, "devDependencies", package))
                {
                    return framework;
                }
            }
            return null;
        }
    }

    private static bool IsManifest(RepositoryFile file) =>
        file.Type == "blob"
        && !ContainsSegment(file.Path, "node_modules")
        && (file.Path.EndsWith("package.json", StringComparison.OrdinalIgnoreCase)
            || file.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));

    private static bool HasKey(JsonElement root, string section, string key) =>
        root.TryGetProperty(section, out var deps)
        && deps.ValueKind == JsonValueKind.Object
        && deps.TryGetProperty(key, out _);

    private static bool ContainsSegment(string path, string segment) =>
        path.Contains($"/{segment}/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith($"{segment}/", StringComparison.OrdinalIgnoreCase);
}
