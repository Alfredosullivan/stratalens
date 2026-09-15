using System.Text.Json;
using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Entities;
using Stratalens.Domain.Enums;

namespace Stratalens.Infrastructure.Analyzers;

// Analyzer de autenticación local (T32, Fase 6). Detecta que el backend usa JWT PROPIO
// (no un proveedor de identidad de terceros — eso sería External y queda para un ticket
// futuro) buscando la dependencia real en el manifiesto del proyecto:
//   - Node:  "jsonwebtoken" o "passport-jwt" en un package.json
//   - .NET:  "Microsoft.AspNetCore.Authentication.JwtBearer" en un .csproj
//
// Mismo criterio de evidencia que NodeExpressAnalyzer/DockerAnalyzer: nunca se infiere
// auth por convención (una carpeta "auth/") — solo por una dependencia declarada en un
// manifiesto real (regla de RULES.md: nunca un Node sin Source verificable).
//
// Como Docker, entrega el Node YA a nivel Sistema (Category=Security): "¿usa JWT?" es un
// hecho binario, sin detalle fino que agregar en el MVP. SystemGraphBuilder lo pasa tal
// cual y, si hay Backend, dibuja el edge Backend→Security.
public class AuthAnalyzer : ILanguageAnalyzer
{
    // Dependencias de npm que evidencian JWT del lado del backend. jsonwebtoken firma y
    // verifica tokens; passport-jwt es la estrategia de Passport para validarlos. Ninguna
    // tiene sentido en un frontend (usan el módulo crypto de Node), así que su presencia
    // en cualquier package.json del repo es señal fuerte de auth de backend.
    private static readonly string[] NpmAuthPackages = { "jsonwebtoken", "passport-jwt" };

    // Paquete NuGet del middleware oficial de JWT Bearer de ASP.NET Core.
    private const string DotNetJwtPackage = "Microsoft.AspNetCore.Authentication.JwtBearer";

    public string Language => "auth";

    // Gate barato (solo rutas): sin ningún manifiesto (package.json/.csproj) no hay dónde
    // declarar la dependencia, así que no hay nada que confirmar. La confirmación real
    // (leer el contenido) ocurre en AnalyzeAsync, igual que NodeExpressAnalyzer.
    public bool CanAnalyze(IReadOnlyList<RepositoryFile> files) =>
        files.Any(IsManifest);

    public async Task<AnalysisResult> AnalyzeAsync(
        Guid projectId,
        RepositoryReference repo,
        IProviderConnector connector,
        IReadOnlyList<RepositoryFile> files,
        CancellationToken ct = default)
    {
        // Recorremos los manifiestos y nos quedamos con el PRIMERO que declare auth: como el
        // Node resultante es binario a nivel Sistema ("¿usa JWT?"), no hace falta atribuirlo
        // a un backend concreto entre varios — con una evidencia basta. (Nota de diseño: el
        // ticket sugería reusar la resolución "package.json más cercano" de NodeExpressAnalyzer;
        // al implementarlo se vio que esa lógica solo importa para ligar el hallazgo a UN
        // backend específico, que no es el caso aquí — habría acoplado dos analyzers sin ganar
        // corrección.)
        foreach (var file in files.Where(IsManifest))
        {
            var content = await connector.GetFileContentAsync(repo, file.Path, ct);

            if (DeclaresAuth(file.Path, content))
            {
                var node = new Node(
                    projectId,
                    "JWT",
                    "JWT",
                    NodeCategory.Security,
                    new Dictionary<string, string> { ["source"] = file.Path });

                return new AnalysisResult(new List<Node> { node }, new List<Edge>());
            }
        }

        // Ninguna evidencia: no inventamos un nodo de seguridad (RULES.md).
        return new AnalysisResult(new List<Node>(), new List<Edge>());
    }

    // Candidatos donde puede vivir la dependencia de auth: package.json (npm) o .csproj
    // (.NET), excluyendo node_modules (deps de terceros, no del proyecto).
    private static bool IsManifest(RepositoryFile file) =>
        file.Type == "blob"
        && !ContainsSegment(file.Path, "node_modules")
        && (file.Path.EndsWith("package.json", StringComparison.OrdinalIgnoreCase)
            || file.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));

    // Delega según el tipo de manifiesto: JSON para npm, substring para .csproj (parsear el
    // XML sería sobre-ingeniería — el nombre del paquete es único y no colisiona con otra cosa).
    private static bool DeclaresAuth(string path, string content) =>
        path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            ? content.Contains(DotNetJwtPackage, StringComparison.OrdinalIgnoreCase)
            : HasAnyNpmDependency(content, NpmAuthPackages);

    // Busca cualquiera de los paquetes en dependencies O devDependencies (mismo criterio
    // que NodeExpressAnalyzer: un paquete declarado solo en dev igual confirma el stack).
    private static bool HasAnyNpmDependency(string packageJsonContent, string[] packages)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(packageJsonContent);
        }
        catch (JsonException)
        {
            // Un package.json ilegible no es evidencia de nada — y como acá recorremos VARIOS
            // manifiestos, uno roto no debe tumbar el análisis entero (a diferencia de Express,
            // que lee un único package.json ya resuelto).
            return false;
        }

        using (doc)
        {
            return packages.Any(pkg =>
                HasKey(doc.RootElement, "dependencies", pkg)
                || HasKey(doc.RootElement, "devDependencies", pkg));
        }
    }

    private static bool HasKey(JsonElement root, string section, string key) =>
        root.TryGetProperty(section, out var deps)
        && deps.ValueKind == JsonValueKind.Object
        && deps.TryGetProperty(key, out _);

    // "segment" como carpeta completa del path, no una subcadena cualquiera (igual que
    // NodeExpressAnalyzer): "node_modules" no debe matchear un archivo suelto que lo contenga.
    private static bool ContainsSegment(string path, string segment) =>
        path.Contains($"/{segment}/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith($"{segment}/", StringComparison.OrdinalIgnoreCase);
}
