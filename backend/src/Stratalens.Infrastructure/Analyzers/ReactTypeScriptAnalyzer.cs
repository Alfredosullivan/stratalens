using System.Text.Json;
using System.Text.Json.Serialization;
using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Entities;
using Stratalens.Domain.Enums;

namespace Stratalens.Infrastructure.Analyzers;

// Analyzer estático de repos React/TypeScript (T9). Implementa ILanguageAnalyzer, igual
// que el de C#, pero delega la detección a un subproceso Node (TS Compiler API) a través
// de INodeAnalyzerRunner. Este .NET-side es quien CONSTRUYE los Node/Edge del dominio a
// partir del JSON crudo que devuelve Node — Node nunca conoce el modelo del dominio.
//
// Ámbito de T9: detecta la estructura del frontend y las llamadas a API salientes. NO
// conecta el frontend con el backend .NET (eso es T10, que emparejará las URLs detectadas
// con los Controllers). Por eso las llamadas a API se guardan como metadata del nodo que
// las hace, como evidencia para ese paso posterior.
public class ReactTypeScriptAnalyzer : ILanguageAnalyzer
{
    // Inferencia estática (import declarado en el código): confianza alta, nunca 100.
    private const int ImportConfidence = 85;

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly INodeAnalyzerRunner _runner;

    public ReactTypeScriptAnalyzer(INodeAnalyzerRunner runner)
    {
        _runner = runner;
    }

    // El nombre quedó de cuando el analyzer solo aceptaba .ts/.tsx (T9). Se dejó así al
    // ampliar a JS/JSX (ver IsScriptFile) para no tener que tocar SystemGraphBuilder/DI,
    // que indexan por esta clave — el TS Compiler API que corre detrás siempre soportó
    // toda la familia JS/TS/JSX/TSX, así que la ampliación no cambia CÓMO se analiza.
    public string Language => "typescript";

    // Aplica si el repo es un proyecto Node con React (hay package.json y algún archivo
    // de código fuente JS/TS). No leemos contenidos aquí (CanAnalyze solo recibe rutas).
    public bool CanAnalyze(IReadOnlyList<RepositoryFile> files) =>
        files.Any(f => f.Path.EndsWith("package.json", StringComparison.OrdinalIgnoreCase))
        && files.Any(f => IsScriptFile(f.Path));

    public async Task<AnalysisResult> AnalyzeAsync(
        Guid projectId,
        RepositoryReference repo,
        IProviderConnector connector,
        IReadOnlyList<RepositoryFile> files,
        CancellationToken ct = default)
    {
        // 1. Reunir el contenido de los archivos de código fuente (excluyendo dependencias).
        var sourceFiles = files
            .Where(f => f.Type == "blob" && IsScriptFile(f.Path) && !IsDependency(f.Path))
            .ToList();

        var payload = new List<NodeAnalyzerFile>();
        foreach (var file in sourceFiles)
        {
            var content = await connector.GetFileContentAsync(repo, file.Path, ct);
            payload.Add(new NodeAnalyzerFile(file.Path, content));
        }

        // 2. Delegar la detección al subproceso Node y deserializar su salida.
        var json = await _runner.RunAsync(payload, ct);
        var raw = JsonSerializer.Deserialize<RawAnalysis>(json, JsonOptions) ?? new RawAnalysis();

        // 3. Agrupar las llamadas a API por archivo, para adjuntarlas al nodo que las hace.
        var callsByFile = raw.ApiCalls
            .GroupBy(c => c.FromFile)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 4. Construir un Node por componente detectado.
        var nodesByFile = new Dictionary<string, Node>(StringComparer.Ordinal);
        var nodes = new List<Node>();
        foreach (var component in raw.Components)
        {
            var metadata = new Dictionary<string, string>
            {
                ["source"] = component.File,
                ["language"] = "typescript"
            };

            // Evidencia para T10: las llamadas HTTP salientes de este componente.
            if (callsByFile.TryGetValue(component.File, out var calls))
            {
                for (var i = 0; i < calls.Count; i++)
                {
                    metadata[$"apiCall.{i}"] = $"{calls[i].Method} {calls[i].Url}";
                }
            }

            var node = new Node(projectId, component.Name, MapKind(component.Kind), NodeCategory.Code, metadata);
            nodes.Add(node);
            nodesByFile[component.File] = node;
        }

        // 5. Un Edge por import local entre dos componentes detectados.
        var edges = new List<Edge>();
        var seen = new HashSet<(Guid, Guid)>();
        foreach (var import in raw.Imports)
        {
            if (!nodesByFile.TryGetValue(import.FromFile, out var from) ||
                !nodesByFile.TryGetValue(import.ToFile, out var to))
            {
                continue;
            }

            if (from.Id == to.Id || !seen.Add((from.Id, to.Id)))
            {
                continue;
            }

            edges.Add(Edge.FromStaticAnalysis(
                projectId,
                sourceNodeId: from.Id,
                targetNodeId: to.Id,
                type: "import",
                source: import.FromFile,
                confidence: ImportConfidence,
                metadata: new Dictionary<string, string> { ["language"] = "typescript" }));
        }

        return new AnalysisResult(nodes, edges);
    }

    // Traduce la carpeta detectada por Node al Type del nodo del dominio.
    private static string MapKind(string kind) => kind switch
    {
        "page" => "Page",
        "component" => "Component",
        "hook" => "Hook",
        "service" => "Service",
        _ => "Module"
    };

    // Incluye JS/JSX puro (sin tipos): el subproceso Node decide el ScriptKind correcto
    // por extensión (ver analyzers-node/src/analyze.mjs), así que ya sabía parsearlos —
    // el único bloqueo era este filtro, que dejaba afuera repos React 100% JSX.
    private static bool IsScriptFile(string path) =>
        path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase);

    private static bool IsDependency(string path) =>
        path.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("node_modules/", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase);

    // --- DTOs que mapean el JSON crudo del subproceso Node ---

    private sealed class RawAnalysis
    {
        [JsonPropertyName("components")]
        public List<RawComponent> Components { get; init; } = new();

        [JsonPropertyName("imports")]
        public List<RawImport> Imports { get; init; } = new();

        [JsonPropertyName("apiCalls")]
        public List<RawApiCall> ApiCalls { get; init; } = new();
    }

    private sealed class RawComponent
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("kind")]
        public string Kind { get; init; } = string.Empty;

        [JsonPropertyName("file")]
        public string File { get; init; } = string.Empty;
    }

    private sealed class RawImport
    {
        [JsonPropertyName("fromFile")]
        public string FromFile { get; init; } = string.Empty;

        [JsonPropertyName("toFile")]
        public string ToFile { get; init; } = string.Empty;
    }

    private sealed class RawApiCall
    {
        [JsonPropertyName("fromFile")]
        public string FromFile { get; init; } = string.Empty;

        [JsonPropertyName("method")]
        public string Method { get; init; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; init; } = string.Empty;
    }
}
