using System.Text.Json;
using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Entities;
using Stratalens.Domain.Enums;

namespace Stratalens.Infrastructure.Analyzers;

// Analyzer estático de backends Node.js + Express. A diferencia de CSharpAnalyzer (Roslyn)
// y ReactTypeScriptAnalyzer (subproceso Node con TS Compiler API), este NO parsea código:
// para el nivel Sistema que muestra el MVP (Backend + Database, sin detalle de Code, T10)
// alcanza con leer el package.json real y la convención de carpetas — parsear JS de verdad
// sería sobre-ingeniería para lo que hoy hace falta mostrar.
public class NodeExpressAnalyzer : ILanguageAnalyzer
{
    public string Language => "express";

    // Señal barata (solo rutas, sin contenido): package.json + al menos un archivo de
    // código en una carpeta controllers/ o routes/ — la estructura que CLAUDE.md prescribe
    // para los proyectos Node/Express de Carlos, y la convención MVC más común en general.
    public bool CanAnalyze(IReadOnlyList<RepositoryFile> files) =>
        files.Any(f => f.Path.EndsWith("package.json", StringComparison.OrdinalIgnoreCase))
        && files.Any(IsControllerOrRoute);

    public async Task<AnalysisResult> AnalyzeAsync(
        Guid projectId,
        RepositoryReference repo,
        IProviderConnector connector,
        IReadOnlyList<RepositoryFile> files,
        CancellationToken ct = default)
    {
        // Monorepos reales (ej. backend en la raíz + frontend en client/) tienen MÁS de un
        // package.json — hay que leer el del BACKEND, no "el primero de la lista" (frágil,
        // dependía del orden de enumeración). Se resuelve buscando el ancestro más cercano
        // al primer controller/route detectado, subiendo directorio por directorio.
        var anchorFile = files.First(IsControllerOrRoute).Path;
        var packageJsonPath = FindNearestPackageJson(files, anchorFile);
        var packageJson = await connector.GetFileContentAsync(repo, packageJsonPath, ct);

        // Confirmación real (no solo convención de carpetas): si el package.json no lista
        // "express", no es un backend Express aunque tenga carpetas con esos nombres —
        // nunca inventar infraestructura que no está (regla de RULES.md).
        if (!HasDependency(packageJson, "express"))
        {
            return new AnalysisResult(new List<Node>(), new List<Edge>());
        }

        var nodes = new List<Node>();

        foreach (var file in files.Where(IsControllerOrRoute))
        {
            nodes.Add(new Node(
                projectId,
                Path.GetFileNameWithoutExtension(file.Path),
                "Controller",
                NodeCategory.Code,
                new Dictionary<string, string> { ["source"] = file.Path, ["language"] = "express" }));
        }

        // Marcador de base de datos: mismo rol que "DbContext" para CSharpAnalyzer. Solo
        // Postgres por ahora (driver "pg", el que usan los proyectos reales de Carlos);
        // ampliar a Mongo/MySQL cuando haga falta un caso real, no antes.
        if (HasDependency(packageJson, "pg"))
        {
            nodes.Add(new Node(
                projectId,
                "pg",
                "PgPool",
                NodeCategory.Code,
                new Dictionary<string, string> { ["source"] = packageJsonPath, ["language"] = "express" }));
        }

        return new AnalysisResult(nodes, new List<Edge>());
    }

    // Sube desde el directorio del archivo ancla buscando el package.json más cercano
    // (patrón estándar de resolución en monorepos: el package.json que realmente gobierna
    // ese archivo es el del ancestro más próximo, no cualquiera que exista en el repo).
    private static string FindNearestPackageJson(IReadOnlyList<RepositoryFile> files, string anchorPath)
    {
        var packageJsonPaths = files
            .Where(f => f.Path.EndsWith("package.json", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var dir = Path.GetDirectoryName(anchorPath)?.Replace('\\', '/') ?? string.Empty;
        while (true)
        {
            var candidate = dir.Length == 0 ? "package.json" : $"{dir}/package.json";
            if (packageJsonPaths.Contains(candidate))
            {
                return candidate;
            }

            if (dir.Length == 0) break;
            var lastSlash = dir.LastIndexOf('/');
            dir = lastSlash < 0 ? string.Empty : dir[..lastSlash];
        }

        // Fallback defensivo: no debería pasar (CanAnalyze ya exige al menos un package.json).
        return packageJsonPaths.First();
    }

    private static bool IsControllerOrRoute(RepositoryFile file) =>
        file.Type == "blob"
        && !ContainsSegment(file.Path, "node_modules")
        && (ContainsSegment(file.Path, "controllers") || ContainsSegment(file.Path, "routes"))
        && (file.Path.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
            || file.Path.EndsWith(".mjs", StringComparison.OrdinalIgnoreCase)
            || file.Path.EndsWith(".cjs", StringComparison.OrdinalIgnoreCase)
            || file.Path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase));

    // "segment" como carpeta completa del path, no una subcadena cualquiera (para que
    // "controllers" no matchee, por ejemplo, un archivo llamado "mycontrollers.js" suelto).
    private static bool ContainsSegment(string path, string segment) =>
        path.Contains($"/{segment}/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith($"{segment}/", StringComparison.OrdinalIgnoreCase);

    // Busca el paquete en "dependencies" O "devDependencies" (algunos proyectos declaran
    // drivers de DB solo en dev para tests, ej. pg-mem — igual confirma que el stack existe).
    private static bool HasDependency(string packageJsonContent, string packageName)
    {
        using var doc = JsonDocument.Parse(packageJsonContent);
        return HasKey(doc.RootElement, "dependencies", packageName)
            || HasKey(doc.RootElement, "devDependencies", packageName);
    }

    private static bool HasKey(JsonElement root, string section, string key) =>
        root.TryGetProperty(section, out var deps)
        && deps.ValueKind == JsonValueKind.Object
        && deps.TryGetProperty(key, out _);
}
