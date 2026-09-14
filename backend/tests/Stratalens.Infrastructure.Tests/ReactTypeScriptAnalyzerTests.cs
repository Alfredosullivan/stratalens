using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Enums;
using Stratalens.Infrastructure.Analyzers;

namespace Stratalens.Infrastructure.Tests;

// Tests del analyzer de React/TS (T9). Se prueban DOS responsabilidades por separado:
//   1. El MAPEO JSON → Node/Edge, con un runner FALSO → no necesita Node instalado.
//   2. La INTEGRACIÓN real vía Process.Start → sí necesita Node (se salta con soft-skip
//      si Node o el script no están disponibles).
public class ReactTypeScriptAnalyzerTests
{
    private static readonly RepositoryReference Repo = new("carlos", "demo", "main");

    private static readonly Dictionary<string, string> Fixture = new()
    {
        ["package.json"] = """{ "name": "demo", "dependencies": { "react": "19.0.0" } }""",

        ["src/pages/ProductsPage.tsx"] = """
            import { getProducts } from "../services/productService";
            export function ProductsPage() {
              getProducts();
              return null;
            }
            """,

        ["src/services/productService.ts"] = """
            import axios from "axios";
            export async function getProducts() {
              return axios.get("/api/products");
            }
            """
    };

    [Fact]
    public void CanAnalyze_EsTrue_SoloConPackageJsonYArchivosTs()
    {
        var analyzer = new ReactTypeScriptAnalyzer(new FakeRunner("{}"));

        var reactRepo = new List<RepositoryFile>
        {
            new("package.json", "blob"),
            new("src/App.tsx", "blob")
        };
        var soloBackend = new List<RepositoryFile> { new("Program.cs", "blob") };

        Assert.True(analyzer.CanAnalyze(reactRepo));
        Assert.False(analyzer.CanAnalyze(soloBackend));
    }

    // Bug reportado por Carlos: repos React reales con JSX puro (sin TypeScript) daban
    // grafo vacío porque este chequeo exigía .ts/.tsx. El subproceso Node ya sabía parsear
    // JSX (decide el ScriptKind por extensión); el único bloqueo era este filtro .NET.
    [Fact]
    public void CanAnalyze_EsTrue_ConPackageJsonYArchivosJsxPuro_SinTypeScript()
    {
        var analyzer = new ReactTypeScriptAnalyzer(new FakeRunner("{}"));

        var reactJsxRepo = new List<RepositoryFile>
        {
            new("client/package.json", "blob"),
            new("client/src/App.jsx", "blob"),
            new("client/src/components/Dashboard.jsx", "blob")
        };

        Assert.True(analyzer.CanAnalyze(reactJsxRepo));
    }

    [Fact]
    public async Task Analyze_MapeaComponentesImportsYApiCalls_DesdeElJsonDelRunner()
    {
        // --- Arrange: runner falso con la salida cruda que "produciría" Node ---
        const string json = """
            {
              "components": [
                { "name": "ProductsPage", "kind": "page", "file": "src/pages/ProductsPage.tsx" },
                { "name": "productService", "kind": "service", "file": "src/services/productService.ts" }
              ],
              "imports": [
                { "fromFile": "src/pages/ProductsPage.tsx", "toFile": "src/services/productService.ts" }
              ],
              "apiCalls": [
                { "fromFile": "src/services/productService.ts", "method": "GET", "url": "/api/products" }
              ]
            }
            """;

        var analyzer = new ReactTypeScriptAnalyzer(new FakeRunner(json));
        var connector = new StubConnector(Fixture);
        var projectId = Guid.NewGuid();
        var files = Fixture.Keys.Select(p => new RepositoryFile(p, "blob")).ToList();

        // --- Act ---
        var result = await analyzer.AnalyzeAsync(projectId, Repo, connector, files);

        // --- Assert: nodos ---
        Assert.Equal(2, result.Nodes.Count);
        var byType = result.Nodes.ToDictionary(n => n.Type, n => n);

        Assert.Equal("ProductsPage", byType["Page"].Name);
        Assert.Equal("productService", byType["Service"].Name);
        Assert.All(result.Nodes, n => Assert.Equal(NodeCategory.Code, n.Category));

        // La llamada a API quedó como evidencia en la metadata del service (para T10).
        Assert.Equal("GET /api/products", byType["Service"].Metadata["apiCall.0"]);

        // --- Assert: edge del import page → service ---
        var edge = Assert.Single(result.Edges);
        Assert.Equal(byType["Page"].Id, edge.SourceNodeId);
        Assert.Equal(byType["Service"].Id, edge.TargetNodeId);
        Assert.Equal("import", edge.Type);
        Assert.Equal(EdgeSourceType.Static, edge.SourceType);
        Assert.Equal(85, edge.Confidence);
        Assert.False(string.IsNullOrWhiteSpace(edge.Source));
    }

    // El filtro de CanAnalyze y el de "qué archivos se mandan al runner" son DOS chequeos
    // separados (mismo helper, IsScriptFile) — este test cubre el segundo: que un .jsx
    // realmente llegue al payload, no solo que CanAnalyze diga que sí.
    [Fact]
    public async Task Analyze_IncluyeArchivosJsx_EnElPayloadDelRunner()
    {
        var capturingRunner = new CapturingRunner("{}");
        var analyzer = new ReactTypeScriptAnalyzer(capturingRunner);
        var jsxFixture = new Dictionary<string, string>
        {
            ["package.json"] = """{ "name": "demo", "dependencies": { "react": "19.0.0" } }""",
            ["src/components/Dashboard.jsx"] = "export function Dashboard() { return null; }"
        };
        var connector = new StubConnector(jsxFixture);
        var files = jsxFixture.Keys.Select(p => new RepositoryFile(p, "blob")).ToList();

        await analyzer.AnalyzeAsync(Guid.NewGuid(), Repo, connector, files);

        Assert.Contains(capturingRunner.ReceivedFiles!, f => f.Path == "src/components/Dashboard.jsx");
    }

    [Fact]
    public async Task Integracion_InvocaElSubprocesoNodeReal_YProduceElGrafo()
    {
        // Soft-skip: si no hay Node o no encontramos el script, no fallamos el CI.
        var scriptPath = LocateAnalyzerScript();
        if (scriptPath is null || !IsNodeAvailable())
        {
            return;
        }

        // --- Arrange: runner REAL (Process.Start) contra el analyze.mjs de verdad ---
        var analyzer = new ReactTypeScriptAnalyzer(new NodeProcessRunner(scriptPath));
        var connector = new StubConnector(Fixture);
        var projectId = Guid.NewGuid();
        var files = Fixture.Keys.Select(p => new RepositoryFile(p, "blob")).ToList();

        // --- Act ---
        var result = await analyzer.AnalyzeAsync(projectId, Repo, connector, files);

        // --- Assert: el subproceso detectó y .NET construyó el grafo esperado ---
        Assert.Equal(2, result.Nodes.Count);
        Assert.Contains(result.Nodes, n => n.Name == "ProductsPage" && n.Type == "Page");
        Assert.Contains(result.Nodes, n => n.Name == "productService" && n.Type == "Service");

        var edge = Assert.Single(result.Edges);
        Assert.Equal("import", edge.Type);

        var service = result.Nodes.Single(n => n.Type == "Service");
        Assert.Equal("GET /api/products", service.Metadata["apiCall.0"]);
    }

    // Sube por el árbol de carpetas buscando analyzers-node/src/analyze.mjs.
    private static string? LocateAnalyzerScript()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "analyzers-node", "src", "analyze.mjs");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        return null;
    }

    private static bool IsNodeAvailable()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "node",
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            process!.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    // Runner falso: devuelve un JSON fijo sin lanzar ningún proceso.
    private sealed class FakeRunner : INodeAnalyzerRunner
    {
        private readonly string _json;
        public FakeRunner(string json) => _json = json;
        public Task<string> RunAsync(IReadOnlyList<NodeAnalyzerFile> files, CancellationToken ct = default) =>
            Task.FromResult(_json);
    }

    // Como FakeRunner, pero además guarda qué archivos recibió, para poder afirmar sobre
    // el payload real que arma AnalyzeAsync (no solo sobre el JSON de vuelta).
    private sealed class CapturingRunner : INodeAnalyzerRunner
    {
        private readonly string _json;
        public IReadOnlyList<NodeAnalyzerFile>? ReceivedFiles { get; private set; }
        public CapturingRunner(string json) => _json = json;

        public Task<string> RunAsync(IReadOnlyList<NodeAnalyzerFile> files, CancellationToken ct = default)
        {
            ReceivedFiles = files;
            return Task.FromResult(_json);
        }
    }

    // Connector de stub: sirve el contenido de archivo desde el fixture en memoria.
    private sealed class StubConnector : IProviderConnector
    {
        private readonly IReadOnlyDictionary<string, string> _files;
        public StubConnector(IReadOnlyDictionary<string, string> files) => _files = files;

        public Task<string> GetFileContentAsync(RepositoryReference repo, string path, CancellationToken ct = default) =>
            Task.FromResult(_files[path]);

        public Task<IReadOnlyList<RepositoryFile>> GetFileTreeAsync(RepositoryReference repo, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RepositorySummary>> ListRepositoriesAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
