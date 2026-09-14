using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Enums;
using Stratalens.Infrastructure.Analyzers;

namespace Stratalens.Infrastructure.Tests;

// Tests del analyzer de Node/Express (hueco reportado por Carlos: no existía NINGÚN
// analyzer para backends Express, por eso un repo así daba grafo vacío).
public class NodeExpressAnalyzerTests
{
    private static readonly RepositoryReference Repo = new("carlos", "demo", "main");

    private const string PackageJsonConPgYExpress = """
        { "name": "demo", "dependencies": { "express": "5.2.1", "pg": "8.20.0" } }
        """;

    [Fact]
    public void CanAnalyze_EsTrue_ConPackageJsonYCarpetaControllers()
    {
        var analyzer = new NodeExpressAnalyzer();

        var expressRepo = new List<RepositoryFile>
        {
            new("package.json", "blob"),
            new("src/controllers/report.controller.js", "blob")
        };
        var soloFrontend = new List<RepositoryFile>
        {
            new("client/package.json", "blob"),
            new("client/src/App.jsx", "blob")
        };

        Assert.True(analyzer.CanAnalyze(expressRepo));
        Assert.False(analyzer.CanAnalyze(soloFrontend));
    }

    [Fact]
    public async Task Analyze_ConExpressYPg_ProduceControllerYMarcadorDeDb()
    {
        var analyzer = new NodeExpressAnalyzer();
        var fixture = new Dictionary<string, string>
        {
            ["package.json"] = PackageJsonConPgYExpress,
            ["src/controllers/report.controller.js"] = "// controller",
            ["src/routes/report.routes.js"] = "// routes"
        };
        var connector = new StubConnector(fixture);
        var files = fixture.Keys.Select(p => new RepositoryFile(p, "blob")).ToList();

        var result = await analyzer.AnalyzeAsync(Guid.NewGuid(), Repo, connector, files);

        Assert.Equal(3, result.Nodes.Count); // 2 controllers/routes + 1 marcador de DB
        Assert.Contains(result.Nodes, n => n.Type == "Controller" && n.Name == "report.controller");
        Assert.Contains(result.Nodes, n => n.Type == "Controller" && n.Name == "report.routes");

        var dbMarker = Assert.Single(result.Nodes, n => n.Type == "PgPool");
        Assert.Equal(NodeCategory.Code, dbMarker.Category);
        Assert.Equal("package.json", dbMarker.Metadata["source"]);
    }

    [Fact]
    public async Task Analyze_SinDependenciaPg_NoProduceMarcadorDeDb()
    {
        var analyzer = new NodeExpressAnalyzer();
        var fixture = new Dictionary<string, string>
        {
            ["package.json"] = """{ "name": "demo", "dependencies": { "express": "5.2.1" } }""",
            ["src/controllers/report.controller.js"] = "// controller"
        };
        var connector = new StubConnector(fixture);
        var files = fixture.Keys.Select(p => new RepositoryFile(p, "blob")).ToList();

        var result = await analyzer.AnalyzeAsync(Guid.NewGuid(), Repo, connector, files);

        Assert.DoesNotContain(result.Nodes, n => n.Type == "PgPool");
    }

    // Monorepo real (forma exacta de los repos de Carlos): backend en la raíz + frontend
    // en client/, CADA UNO con su propio package.json. Debe leer el del backend (tiene
    // express+pg), no el del frontend (solo react) — "el primero de la lista" habría sido
    // frágil y potencialmente incorrecto según el orden de enumeración.
    [Fact]
    public async Task Analyze_ConDosPackageJson_UsaElDelBackendNoElDelFrontend()
    {
        var analyzer = new NodeExpressAnalyzer();
        var fixture = new Dictionary<string, string>
        {
            ["package.json"] = PackageJsonConPgYExpress,
            ["src/controllers/report.controller.js"] = "// controller",
            ["client/package.json"] = """{ "name": "client", "dependencies": { "react": "19.0.0" } }""",
            ["client/src/App.jsx"] = "export function App() { return null; }"
        };
        var connector = new StubConnector(fixture);
        var files = fixture.Keys.Select(p => new RepositoryFile(p, "blob")).ToList();

        var result = await analyzer.AnalyzeAsync(Guid.NewGuid(), Repo, connector, files);

        // Si hubiera leído el package.json del frontend (sin "pg"), no habría marcador de DB.
        Assert.Contains(result.Nodes, n => n.Type == "PgPool");
    }

    // Nunca inventar infraestructura que no está (RULES.md): una carpeta "controllers/"
    // sin "express" real en package.json no debe producir un Backend falso.
    [Fact]
    public async Task Analyze_SinExpressEnPackageJson_NoProduceNodos()
    {
        var analyzer = new NodeExpressAnalyzer();
        var fixture = new Dictionary<string, string>
        {
            ["package.json"] = """{ "name": "demo", "dependencies": { "fastify": "4.0.0" } }""",
            ["src/controllers/report.controller.js"] = "// controller"
        };
        var connector = new StubConnector(fixture);
        var files = fixture.Keys.Select(p => new RepositoryFile(p, "blob")).ToList();

        var result = await analyzer.AnalyzeAsync(Guid.NewGuid(), Repo, connector, files);

        Assert.Empty(result.Nodes);
    }

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
