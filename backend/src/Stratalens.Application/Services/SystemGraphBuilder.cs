using Stratalens.Application.Models;
using Stratalens.Domain.Entities;
using Stratalens.Domain.Enums;

namespace Stratalens.Application.Services;

// Agrega la detección FINA de los analyzers (controllers, componentes, DbContext...) en
// el grafo GRUESO que muestra el MVP: nivel Application/System (Frontend, Backend,
// PostgreSQL) con sus relaciones de alto nivel. El detalle de código no se persiste como
// nodos (el nivel Code está fuera del MVP, PRODUCT.md); se usa como EVIDENCIA (Source)
// de los edges gruesos.
//
// Vive en Application porque decidir "qué es un nodo de arquitectura y cómo se conectan"
// es lógica de negocio — no acceso a datos ni detalle de un lenguaje concreto.
public class SystemGraphBuilder
{
    // Confianzas de las relaciones de alto nivel: inferencia estática, nunca 100.
    private const int FrontendToBackendConfidence = 80;
    private const int BackendToDatabaseConfidence = 85;
    // Más alta que las otras dos: la evidencia (el Dockerfile existe) es directa, no una
    // inferencia sobre texto (URL en un import, nombre de un DbContext) — pero sigue sin
    // ser 100 (eso es exclusivo de runtime confirmado, invariante de Edge en Domain).
    private const int BackendToDockerConfidence = 90;

    // Recibe los resultados de cada analyzer indexados por su lenguaje (ej. "csharp",
    // "typescript") y produce el grafo grueso a persistir.
    public AnalysisResult Build(
        Guid projectId,
        IReadOnlyList<RepositoryFile> files,
        IReadOnlyDictionary<string, AnalysisResult> resultsByLanguage)
    {
        var nodes = new List<Node>();
        var edges = new List<Edge>();

        Node? frontend = null;
        Node? backend = null;
        Node? database = null;

        // --- Frontend: si el analyzer de TypeScript detectó componentes ---
        AnalysisResult? ts = null;
        if (resultsByLanguage.TryGetValue("typescript", out ts) && ts.Nodes.Count > 0)
        {
            frontend = new Node(projectId, "Frontend", "React", NodeCategory.Application,
                Meta(FindFile(files, "package.json") ?? "package.json"));
            nodes.Add(frontend);
        }

        // --- Backend + base de datos: desde el analyzer de C# o el de Express ---
        // Dos analyzers pueden aportar "hay un backend" (csharp/DbContext, express/PgPool);
        // cuál gane decide el Type del nodo, no si el nodo existe. Nunca deberían matchear
        // los dos a la vez en un repo real, pero si pasara, se prioriza C# arbitrariamente
        // (orden de los if) — no hay señal para "el correcto" en ese caso ambiguo.
        (Node? Backend, Node? Database) backendResult =
            resultsByLanguage.TryGetValue("csharp", out var cs) && cs.Nodes.Count > 0
                ? BuildBackend(projectId, files, "AspNetCore", ".csproj", "backend", cs, "DbContext", "EF Core DbContext")
            : resultsByLanguage.TryGetValue("express", out var ex) && ex.Nodes.Count > 0
                ? BuildBackend(projectId, files, "Express", "package.json", "backend", ex, "PgPool", "package.json (dependencia pg)")
            : (null, null);

        backend = backendResult.Backend;
        database = backendResult.Database;
        if (backend is not null) nodes.Add(backend);
        if (database is not null) nodes.Add(database);

        // --- Edge Frontend → Backend: evidencia = una llamada HTTP del frontend ---
        if (frontend is not null && backend is not null)
        {
            var apiCallSource = ts!.Nodes
                .Where(n => n.Metadata.Keys.Any(k => k.StartsWith("apiCall", StringComparison.Ordinal)))
                .Select(n => n.Metadata.GetValueOrDefault("source"))
                .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

            // Solo dibujamos el edge si hay evidencia real. Sin una llamada detectada no
            // inventamos la relación (regla: nunca un edge sin Source).
            if (apiCallSource is not null)
            {
                edges.Add(Edge.FromStaticAnalysis(
                    projectId, frontend.Id, backend.Id, "HTTP/REST", apiCallSource,
                    FrontendToBackendConfidence));
            }
        }

        // --- Edge Backend → PostgreSQL: evidencia = el DbContext ---
        if (backend is not null && database is not null)
        {
            edges.Add(Edge.FromStaticAnalysis(
                projectId, backend.Id, database.Id, "SQL", database.Metadata["source"],
                BackendToDatabaseConfidence));
        }

        // --- Infraestructura (Docker, T30): a diferencia del resto, este analyzer YA
        // entrega el Node a nivel Sistema (Category=Infrastructure) — no hay detalle fino
        // que agregar, "hay Docker o no hay" es un hecho binario. Se pasa tal cual.
        if (resultsByLanguage.TryGetValue("docker", out var docker) && docker.Nodes.Count > 0)
        {
            var dockerNode = docker.Nodes[0];
            nodes.Add(dockerNode);

            // Edge Backend → Docker ("containerized"): solo si hay Backend detectado — sin
            // uno, no hay de quién decir "esto se containeriza" (nunca inventar el otro
            // extremo del edge).
            if (backend is not null)
            {
                edges.Add(Edge.FromStaticAnalysis(
                    projectId, backend.Id, dockerNode.Id, "containerized",
                    dockerNode.Metadata["source"], BackendToDockerConfidence));
            }
        }

        return new AnalysisResult(nodes, edges);
    }

    // Construye el nodo Backend (Type variable según el analyzer que lo detectó) y, si hay
    // marcador de base de datos entre sus nodos finos (DbContext/PgPool), el nodo Database.
    // Compartido entre C# y Express: la ÚNICA diferencia entre stacks es qué Type usar y
    // dónde buscar el marcador — la forma de agregar es la misma.
    private static (Node Backend, Node? Database) BuildBackend(
        Guid projectId,
        IReadOnlyList<RepositoryFile> files,
        string backendType,
        string backendSourceSuffix,
        string backendFallbackSource,
        AnalysisResult result,
        string dbMarkerType,
        string dbFallbackSource)
    {
        var backend = new Node(projectId, "Backend", backendType, NodeCategory.Application,
            Meta(FindFile(files, backendSourceSuffix) ?? backendFallbackSource));

        Node? database = null;
        var dbMarker = result.Nodes.FirstOrDefault(n => n.Type == dbMarkerType);
        if (dbMarker is not null)
        {
            // El MVP solo soporta PostgreSQL como target, por eso se etiqueta siempre así
            // (ambos marcadores hoy — DbContext y PgPool — son evidencia de Postgres).
            var source = dbMarker.Metadata.GetValueOrDefault("source", dbFallbackSource);
            database = new Node(projectId, "PostgreSQL", "PostgreSQL", NodeCategory.Database, Meta(source));
        }

        return (backend, database);
    }

    private static string? FindFile(IReadOnlyList<RepositoryFile> files, string suffix) =>
        files.FirstOrDefault(f => f.Path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))?.Path;

    private static Dictionary<string, string> Meta(string source) =>
        new() { ["source"] = source };
}
