using System.Text.Json;
using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Entities;
using Stratalens.Domain.Enums;

namespace Stratalens.Infrastructure.Analyzers;

// Analyzer de cloud provider (T38, Fase 6, último del patrón). Detecta que la app USA un cloud
// por el SDK declarado en el manifiesto, y nombra el nodo por el proveedor (AWS/Azure/GCP),
// como el bus se nombra por el broker. A diferencia de los otros analyzers npm, los SDK de
// cloud son paquetes con PREFIJO (@aws-sdk/client-s3, @google-cloud/storage, @azure/...), así
// que la detección npm es por prefijo de la clave, no coincidencia exacta.
//
// Categoría = Deployment: el enum ya lista "AWS, Azure, GCP" ahí (no necesita categoría nueva,
// como Message Bus encajó en Infrastructure).
public class CloudAnalyzer : ILanguageAnalyzer
{
    public string Language => "cloud";

    public bool CanAnalyze(IReadOnlyList<RepositoryFile> files) =>
        files.Any(IsManifest);

    public async Task<AnalysisResult> AnalyzeAsync(
        Guid projectId,
        RepositoryReference repo,
        IProviderConnector connector,
        IReadOnlyList<RepositoryFile> files,
        CancellationToken ct = default)
    {
        foreach (var file in files.Where(IsManifest))
        {
            var content = await connector.GetFileContentAsync(repo, file.Path, ct);
            var provider = ResolveProvider(file.Path, content);

            if (provider is not null)
            {
                var node = new Node(
                    projectId,
                    provider,        // Name = el proveedor (AWS/Azure/GCP)
                    "Cloud",         // Type = tipo técnico (para el color del frontend)
                    NodeCategory.Deployment,
                    new Dictionary<string, string> { ["source"] = file.Path });

                return new AnalysisResult(new List<Node> { node }, new List<Edge>());
            }
        }

        // Ninguna evidencia: no inventamos un cloud que no está (RULES.md).
        return new AnalysisResult(new List<Node>(), new List<Edge>());
    }

    private static string? ResolveProvider(string path, string content) =>
        path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            ? ResolveDotNetProvider(content)
            : ResolveNpmProvider(content);

    // .NET: prefijos de paquete NuGet (substring, sin parsear XML). AWS y GCP tienen prefijos
    // inequívocos; "Azure." se evalúa al final por ser el más amplio.
    private static string? ResolveDotNetProvider(string content)
    {
        if (content.Contains("AWSSDK.", StringComparison.OrdinalIgnoreCase)) return "AWS";
        if (content.Contains("Google.Cloud.", StringComparison.OrdinalIgnoreCase)) return "GCP";
        if (content.Contains("Azure.", StringComparison.OrdinalIgnoreCase)) return "Azure";
        return null;
    }

    // npm: los SDK son paquetes con prefijo. Se recorren TODAS las claves de dependencies/
    // devDependencies y se resuelve el proveedor con prioridad fija (AWS, GCP, Azure) para que
    // el resultado sea determinista aunque hubiera SDKs de varios clouds.
    private static string? ResolveNpmProvider(string packageJsonContent)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(packageJsonContent);
        }
        catch (JsonException)
        {
            // Manifiesto ilegible: no es evidencia, y no debe tumbar el análisis (recorremos
            // varios) — igual que los demás analyzers de manifiesto.
            return null;
        }

        using (doc)
        {
            var keys = DependencyKeys(doc.RootElement).ToList();

            if (keys.Any(k => k.Equals("aws-sdk", StringComparison.OrdinalIgnoreCase)
                || k.StartsWith("@aws-sdk/", StringComparison.OrdinalIgnoreCase)))
            {
                return "AWS";
            }
            if (keys.Any(k => k.StartsWith("@google-cloud/", StringComparison.OrdinalIgnoreCase)))
            {
                return "GCP";
            }
            if (keys.Any(k => k.StartsWith("@azure/", StringComparison.OrdinalIgnoreCase)))
            {
                return "Azure";
            }
            return null;
        }
    }

    // Todas las claves de dependencies + devDependencies (los nombres de los paquetes).
    private static IEnumerable<string> DependencyKeys(JsonElement root)
    {
        foreach (var section in new[] { "dependencies", "devDependencies" })
        {
            if (root.TryGetProperty(section, out var deps) && deps.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in deps.EnumerateObject())
                {
                    yield return prop.Name;
                }
            }
        }
    }

    private static bool IsManifest(RepositoryFile file) =>
        file.Type == "blob"
        && !ContainsSegment(file.Path, "node_modules")
        && (file.Path.EndsWith("package.json", StringComparison.OrdinalIgnoreCase)
            || file.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));

    private static bool ContainsSegment(string path, string segment) =>
        path.Contains($"/{segment}/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith($"{segment}/", StringComparison.OrdinalIgnoreCase);
}
