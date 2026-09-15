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
    // Mismo criterio que Docker: la dependencia de JWT está declarada en un manifiesto real
    // (package.json/.csproj), evidencia directa de manifiesto, no inferencia sobre código.
    private const int SecurityToBackendConfidence = 90;
    // Igual que Security/Docker: evidencia de manifiesto (dependencia de un broker).
    private const int BackendToMessageBusConfidence = 90;
    // Igual: evidencia de manifiesto (dependencia de un framework de background jobs).
    private const int BackendToWorkersConfidence = 90;
    // Igual: evidencia de manifiesto (SDK de un cloud provider).
    private const int BackendToCloudConfidence = 90;

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
        (Node? Backend, Node? Database, IReadOnlyList<Node> Children) backendResult =
            resultsByLanguage.TryGetValue("csharp", out var cs) && cs.Nodes.Count > 0
                ? BuildBackend(projectId, files, "AspNetCore", ".csproj", "backend", cs, "DbContext", "EF Core DbContext")
            : resultsByLanguage.TryGetValue("express", out var ex) && ex.Nodes.Count > 0
                ? BuildBackend(projectId, files, "Express", "package.json", "backend", ex, "PgPool", "package.json (dependencia pg)")
            : (null, null, Array.Empty<Node>());

        backend = backendResult.Backend;
        database = backendResult.Database;
        if (backend is not null) nodes.Add(backend);
        if (database is not null) nodes.Add(database);
        // Los hijos ya vienen con ParentNodeId = backend.Id (o lista vacía si no hay backend).
        nodes.AddRange(backendResult.Children);

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

        // --- Seguridad (Auth/JWT, T32): mismo patrón que Docker — el analyzer entrega el
        // Node YA a nivel Sistema (Category=Security), "¿usa JWT?" es binario. Se agrega tal
        // cual, más el edge Security→Backend ("validates") SOLO si hay Backend detectado
        // (nunca inventar el otro extremo de un edge, RULES.md).
        //
        // Dirección Security→Backend (no Backend→Security): sigue la narrativa de arquitectura
        // (como Archify "Validate Token") — la seguridad se ubica del lado de ENTRADA y valida
        // las requests hacia el backend, en vez de leerse como una dependencia de librería. Así
        // el nodo Security queda a la izquierda del Backend (capa de entrada, junto al Frontend),
        // sin meterse en la línea Frontend→Backend.
        if (resultsByLanguage.TryGetValue("auth", out var auth) && auth.Nodes.Count > 0)
        {
            var securityNode = auth.Nodes[0];
            nodes.Add(securityNode);

            if (backend is not null)
            {
                edges.Add(Edge.FromStaticAnalysis(
                    projectId, securityNode.Id, backend.Id, "validates",
                    securityNode.Metadata["source"], SecurityToBackendConfidence));
            }
        }

        // --- Message Bus (T36): mismo patrón que Docker/Security — el analyzer entrega el Node
        // YA a nivel Sistema (Category=Infrastructure). Se agrega tal cual, más el edge
        // Backend→MessageBus ("messaging", neutral: la dependencia prueba que el backend USA el
        // bus, no si publica o consume) SOLO si hay Backend (nunca inventar el otro extremo).
        if (resultsByLanguage.TryGetValue("messagebus", out var bus) && bus.Nodes.Count > 0)
        {
            var busNode = bus.Nodes[0];
            nodes.Add(busNode);

            if (backend is not null)
            {
                edges.Add(Edge.FromStaticAnalysis(
                    projectId, backend.Id, busNode.Id, "messaging",
                    busNode.Metadata["source"], BackendToMessageBusConfidence));
            }
        }

        // --- Workers (T37): mismo patrón. Nodo a nivel Sistema (Category=Worker) + edge
        // Backend→Workers ("background jobs", neutral) SOLO si hay Backend (nunca inventar el
        // otro extremo).
        if (resultsByLanguage.TryGetValue("workers", out var workers) && workers.Nodes.Count > 0)
        {
            var workersNode = workers.Nodes[0];
            nodes.Add(workersNode);

            if (backend is not null)
            {
                edges.Add(Edge.FromStaticAnalysis(
                    projectId, backend.Id, workersNode.Id, "background jobs",
                    workersNode.Metadata["source"], BackendToWorkersConfidence));
            }
        }

        // --- Cloud (T38): mismo patrón. Nodo a nivel Sistema (Category=Deployment) + edge
        // Backend→Cloud ("cloud services", neutral: el SDK prueba que el backend usa el cloud)
        // SOLO si hay Backend (nunca inventar el otro extremo).
        if (resultsByLanguage.TryGetValue("cloud", out var cloud) && cloud.Nodes.Count > 0)
        {
            var cloudNode = cloud.Nodes[0];
            nodes.Add(cloudNode);

            if (backend is not null)
            {
                edges.Add(Edge.FromStaticAnalysis(
                    projectId, backend.Id, cloudNode.Id, "cloud services",
                    cloudNode.Metadata["source"], BackendToCloudConfidence));
            }
        }

        return new AnalysisResult(nodes, edges);
    }

    // Construye el nodo Backend (Type variable según el analyzer que lo detectó), su nodo
    // Database si hay marcador (DbContext/PgPool), y sus nodos HIJOS (Controllers/Services).
    // Compartido entre C# y Express: la ÚNICA diferencia entre stacks es qué Type usar y
    // dónde buscar el marcador — la forma de agregar es la misma.
    private static (Node Backend, Node? Database, IReadOnlyList<Node> Children) BuildBackend(
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

        // Hijos del Backend (T34, jerarquía): los nodos finos que el analyzer detectó
        // (Controllers/Services/Repositories), re-emitidos con ParentNodeId = backend.Id. Se
        // EXCLUYE el marcador de DB: ese es evidencia del nodo PostgreSQL, no un componente
        // interno del backend. Se RECREAN (Id nuevo) en vez de mutar los del analyzer porque
        // Node es inmutable y este ticket NO surfacea edges entre hijos (lo único que
        // necesitaría Ids estables); el análisis es idempotente, así que recrear no tiene coste
        // de datos. Preservamos Name/Type/Category/Metadata (el Source detectado se conserva).
        var children = result.Nodes
            .Where(n => n.Type != dbMarkerType)
            .Select(n => new Node(projectId, n.Name, n.Type, n.Category, n.Metadata, backend.Id))
            .ToList();

        return (backend, database, children);
    }

    private static string? FindFile(IReadOnlyList<RepositoryFile> files, string suffix) =>
        files.FirstOrDefault(f => f.Path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))?.Path;

    private static Dictionary<string, string> Meta(string source) =>
        new() { ["source"] = source };
}
