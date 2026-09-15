using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Enums;
using Stratalens.Infrastructure.Analyzers;

namespace Stratalens.Infrastructure.Tests;

// Tests del analyzer de autenticación local (T32): detecta JWT propio por dependencia real
// en package.json (npm) o .csproj (.NET), nunca por convención de carpetas.
public class AuthAnalyzerTests
{
    private static readonly RepositoryReference Repo = new("carlos", "demo", "main");

    // --- CanAnalyze: gate barato de rutas (¿hay algún manifiesto donde mirar?) ---

    [Fact]
    public void CanAnalyze_EsTrue_ConPackageJson()
    {
        var analyzer = new AuthAnalyzer();
        var files = new List<RepositoryFile> { new("package.json", "blob"), new("src/index.js", "blob") };

        Assert.True(analyzer.CanAnalyze(files));
    }

    [Fact]
    public void CanAnalyze_EsTrue_ConCsproj()
    {
        var analyzer = new AuthAnalyzer();
        var files = new List<RepositoryFile> { new("src/Api/Api.csproj", "blob") };

        Assert.True(analyzer.CanAnalyze(files));
    }

    // Sin manifiesto no hay dónde declarar la dependencia → nada que analizar.
    [Fact]
    public void CanAnalyze_EsFalse_SinManifiesto()
    {
        var analyzer = new AuthAnalyzer();
        var files = new List<RepositoryFile> { new("src/index.js", "blob"), new("README.md", "blob") };

        Assert.False(analyzer.CanAnalyze(files));
    }

    // --- AnalyzeAsync: confirmación real leyendo el contenido ---

    [Fact]
    public async Task Analyze_ConJsonwebtoken_ProduceNodoDeSeguridad()
    {
        var fixture = new Dictionary<string, string>
        {
            ["package.json"] = """{ "name": "demo", "dependencies": { "express": "5.2.1", "jsonwebtoken": "9.0.2" } }"""
        };
        var result = await Analyze(fixture);

        var node = Assert.Single(result.Nodes);
        Assert.Equal("JWT", node.Name);
        Assert.Equal("JWT", node.Type);
        Assert.Equal(NodeCategory.Security, node.Category);
        Assert.Equal("package.json", node.Metadata["source"]);
        Assert.Empty(result.Edges); // el edge Backend→Security lo crea SystemGraphBuilder, no el analyzer
    }

    // passport-jwt es la otra evidencia npm aceptada (estrategia Passport de validación JWT).
    [Fact]
    public async Task Analyze_ConPassportJwt_ProduceNodoDeSeguridad()
    {
        var fixture = new Dictionary<string, string>
        {
            ["package.json"] = """{ "name": "demo", "dependencies": { "passport-jwt": "4.0.1" } }"""
        };
        var result = await Analyze(fixture);

        Assert.Single(result.Nodes, n => n.Category == NodeCategory.Security);
    }

    // .NET: el paquete oficial JwtBearer en un .csproj. Primer analyzer que lee contenido de
    // .csproj (substring, sin parsear XML — el nombre del paquete no colisiona).
    [Fact]
    public async Task Analyze_ConJwtBearerEnCsproj_ProduceNodoDeSeguridad()
    {
        var fixture = new Dictionary<string, string>
        {
            ["src/Api/Api.csproj"] = """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <ItemGroup>
                    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />
                  </ItemGroup>
                </Project>
                """
        };
        var result = await Analyze(fixture);

        var node = Assert.Single(result.Nodes);
        Assert.Equal(NodeCategory.Security, node.Category);
        Assert.Equal("src/Api/Api.csproj", node.Metadata["source"]);
    }

    // Nunca inventar (RULES.md): un manifiesto sin ninguna dependencia de auth no produce nodo.
    [Fact]
    public async Task Analyze_SinDependenciaDeAuth_NoProduceNada()
    {
        var fixture = new Dictionary<string, string>
        {
            ["package.json"] = """{ "name": "demo", "dependencies": { "express": "5.2.1", "pg": "8.20.0" } }"""
        };
        var result = await Analyze(fixture);

        Assert.Empty(result.Nodes);
        Assert.Empty(result.Edges);
    }

    // Monorepo: el frontend (client/) NO tiene auth, el backend (raíz) SÍ. Debe detectarlo
    // igual y atribuir el Source al manifiesto correcto (el que declara la dependencia).
    [Fact]
    public async Task Analyze_EnMonorepo_AtribuyeElSourceAlManifiestoConLaDependencia()
    {
        var fixture = new Dictionary<string, string>
        {
            ["package.json"] = """{ "name": "demo", "dependencies": { "express": "5.2.1", "jsonwebtoken": "9.0.2" } }""",
            ["client/package.json"] = """{ "name": "client", "dependencies": { "react": "19.0.0" } }"""
        };
        var result = await Analyze(fixture);

        var node = Assert.Single(result.Nodes);
        Assert.Equal("package.json", node.Metadata["source"]);
    }

    private static async Task<AnalysisResult> Analyze(Dictionary<string, string> fixture)
    {
        var analyzer = new AuthAnalyzer();
        var connector = new StubConnector(fixture);
        var files = fixture.Keys.Select(p => new RepositoryFile(p, "blob")).ToList();
        return await analyzer.AnalyzeAsync(Guid.NewGuid(), Repo, connector, files);
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
