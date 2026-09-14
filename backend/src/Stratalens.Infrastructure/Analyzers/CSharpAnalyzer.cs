using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Entities;
using Stratalens.Domain.Enums;

namespace Stratalens.Infrastructure.Analyzers;

// Analyzer estático de repos .NET (T8). Implementa ILanguageAnalyzer con Roslyn en modo
// SINTÁCTICO: parsea cada archivo a su árbol de sintaxis y razona sobre el texto del
// código (nombres de clases, tipos base, atributos, parámetros de constructor), SIN
// resolución semántica (no restauramos NuGet ni compilamos el repo).
//
// Consecuencia consciente: es una inferencia heurística, no una verdad absoluta — por
// eso todo Edge sale con Confidence < 100 (regla de dominio, RULES.md). Vive detrás de
// ILanguageAnalyzer, así que migrar a análisis semántico en el futuro no toca al pipeline.
public class CSharpAnalyzer : ILanguageAnalyzer
{
    // Inferencia estática: confianza alta (la dependencia está declarada en el código)
    // pero nunca 100 — inyectar un tipo en el constructor no prueba que se use en runtime.
    private const int StaticConfidence = 90;

    public string Language => "csharp";

    // Aplica si el repo es un proyecto .NET (tiene al menos un .csproj).
    public bool CanAnalyze(IReadOnlyList<RepositoryFile> files) =>
        files.Any(f => f.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));

    public async Task<AnalysisResult> AnalyzeAsync(
        Guid projectId,
        RepositoryReference repo,
        IProviderConnector connector,
        IReadOnlyList<RepositoryFile> files,
        CancellationToken ct = default)
    {
        // 1. Solo archivos de código C#, excluyendo salidas de build (obj/, bin/).
        var sourceFiles = files
            .Where(f => f.Type == "blob"
                && f.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                && !IsBuildArtifact(f.Path))
            .ToList();

        var detected = new List<DetectedClass>();

        // 2. Leer y parsear cada archivo; clasificar cada clase en un tipo de nodo.
        foreach (var file in sourceFiles)
        {
            var content = await connector.GetFileContentAsync(repo, file.Path, ct);
            var root = CSharpSyntaxTree.ParseText(content, cancellationToken: ct).GetRoot(ct);

            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var nodeType = Classify(classDecl);
                if (nodeType is null)
                {
                    continue;
                }

                var node = new Node(
                    projectId,
                    classDecl.Identifier.Text,
                    nodeType,
                    NodeCategory.Code,
                    new Dictionary<string, string>
                    {
                        ["source"] = file.Path,
                        ["language"] = "csharp"
                    });

                detected.Add(new DetectedClass(classDecl, file.Path, node));
            }
        }

        // 3. Índice tipo → nodo. Se registra cada nodo por su nombre de clase Y por sus
        //    tipos base/interfaces, para resolver la inyección por interfaz (ej. un ctor
        //    que pide IProductService encuentra el nodo de ProductService).
        var typeIndex = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (var dc in detected)
        {
            Register(typeIndex, dc.Syntax.Identifier.Text, dc.Node);

            if (dc.Syntax.BaseList is not null)
            {
                foreach (var baseType in dc.Syntax.BaseList.Types)
                {
                    Register(typeIndex, GetSimpleTypeName(baseType.Type), dc.Node);
                }
            }
        }

        var nodes = detected.Select(d => d.Node).ToList();
        var edges = new List<Edge>();
        var seen = new HashSet<(Guid, Guid)>();

        // 4. Edges por inyección en el constructor (clásico o primario de C# 12).
        foreach (var dc in detected)
        {
            foreach (var paramType in GetConstructorParameterTypes(dc.Syntax))
            {
                if (!typeIndex.TryGetValue(paramType, out var target))
                {
                    continue;
                }

                // Ignorar auto-referencias y dependencias duplicadas entre el mismo par.
                if (target.Id == dc.Node.Id || !seen.Add((dc.Node.Id, target.Id)))
                {
                    continue;
                }

                edges.Add(Edge.FromStaticAnalysis(
                    projectId,
                    sourceNodeId: dc.Node.Id,
                    targetNodeId: target.Id,
                    type: "DI",                 // dependencia por inyección de constructor
                    source: dc.FilePath,        // archivo donde se declara la dependencia
                    confidence: StaticConfidence,
                    metadata: new Dictionary<string, string> { ["language"] = "csharp" }));
            }
        }

        return new AnalysisResult(nodes, edges);
    }

    // --- Clasificación y helpers de sintaxis ---

    // Devuelve el tipo de nodo para una clase, o null si no es un componente que nos
    // interese. Orden de precedencia: señales fuertes (framework) antes que el nombre.
    private static string? Classify(ClassDeclarationSyntax c)
    {
        var name = c.Identifier.Text;
        var baseNames = c.BaseList?.Types
            .Select(t => GetSimpleTypeName(t.Type))
            .ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);

        if (baseNames.Contains("DbContext") || name.EndsWith("DbContext", StringComparison.Ordinal))
        {
            return "DbContext";
        }

        if (HasAttribute(c, "ApiController")
            || baseNames.Contains("ControllerBase") || baseNames.Contains("Controller")
            || name.EndsWith("Controller", StringComparison.Ordinal))
        {
            return "Controller";
        }

        if (name.EndsWith("Repository", StringComparison.Ordinal))
        {
            return "Repository";
        }

        if (name.EndsWith("Service", StringComparison.Ordinal))
        {
            return "Service";
        }

        return null;
    }

    private static bool HasAttribute(ClassDeclarationSyntax c, string attributeName) =>
        c.AttributeLists
            .SelectMany(al => al.Attributes)
            .Select(a => GetSimpleTypeName(a.Name))
            .Any(n => n == attributeName || n == attributeName + "Attribute");

    // Reúne los tipos de los parámetros del constructor primario (C# 12) y de los
    // constructores clásicos: ambos representan dependencias inyectadas.
    private static IEnumerable<string> GetConstructorParameterTypes(ClassDeclarationSyntax c)
    {
        var parameters = new List<ParameterSyntax>();

        if (c.ParameterList is not null)
        {
            parameters.AddRange(c.ParameterList.Parameters);
        }

        parameters.AddRange(c.Members
            .OfType<ConstructorDeclarationSyntax>()
            .SelectMany(ctor => ctor.ParameterList.Parameters));

        return parameters
            .Where(p => p.Type is not null)
            .Select(p => GetSimpleTypeName(p.Type!));
    }

    // Reduce un TypeSyntax a su nombre simple (sin namespace ni genéricos):
    // "Demo.App.IProductService" → "IProductService", "List<Foo>" → "List".
    private static string GetSimpleTypeName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        GenericNameSyntax g => g.Identifier.Text,
        QualifiedNameSyntax q => GetSimpleTypeName(q.Right),
        AliasQualifiedNameSyntax a => GetSimpleTypeName(a.Name),
        NullableTypeSyntax n => GetSimpleTypeName(n.ElementType),
        _ => type.ToString()
    };

    private static void Register(Dictionary<string, Node> index, string key, Node node)
    {
        if (!string.IsNullOrEmpty(key))
        {
            // TryAdd: ante una colisión de nombre, gana el primero (heurística sintáctica).
            index.TryAdd(key, node);
        }
    }

    private static bool IsBuildArtifact(string path) =>
        path.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("bin/", StringComparison.OrdinalIgnoreCase);

    // Tupla interna: liga la sintaxis de la clase, su archivo y el nodo ya creado.
    private sealed record DetectedClass(ClassDeclarationSyntax Syntax, string FilePath, Node Node);
}
