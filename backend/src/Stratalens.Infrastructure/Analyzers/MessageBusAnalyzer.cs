using System.Text.Json;
using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Domain.Entities;
using Stratalens.Domain.Enums;

namespace Stratalens.Infrastructure.Analyzers;

// Analyzer de message bus / mensajería (T36, Fase 6). Hermano de AuthAnalyzer: detecta un
// broker por dependencia REAL en el manifiesto (nunca por convención), pero además MAPEA la
// librería a su broker concreto (amqplib→RabbitMQ, kafkajs→Kafka...) para nombrar el nodo con
// el broker específico, igual que el nodo de DB se llama "PostgreSQL" y no "Database".
//
// No necesita categoría nueva: NodeCategory.Infrastructure ya lista "Queue" (a diferencia de
// Security, que sí necesitó un enum nuevo). Como Docker, entrega el Node YA a nivel Sistema;
// SystemGraphBuilder lo pasa tal cual y, si hay Backend, dibuja el edge Backend→MessageBus.
public class MessageBusAnalyzer : ILanguageAnalyzer
{
    // Paquetes npm → broker. Listas ORDENADAS (no diccionarios) para controlar precedencia:
    // los brokers concretos se resuelven por su lib específica.
    private static readonly (string Package, string Broker)[] NpmBrokers =
    {
        ("amqplib", "RabbitMQ"),
        ("amqp-connection-manager", "RabbitMQ"),
        ("kafkajs", "Kafka"),
        ("nats", "NATS"),
    };

    // Paquetes NuGet → broker. MassTransit va AL FINAL: abstrae el broker (Rabbit/Azure/etc.),
    // así que solo se usa su nombre genérico si no matcheó antes una lib de broker concreto.
    private static readonly (string Package, string Broker)[] DotNetBrokers =
    {
        ("RabbitMQ.Client", "RabbitMQ"),
        ("Confluent.Kafka", "Kafka"),
        ("MassTransit", "Message Bus"),
    };

    public string Language => "messagebus";

    // Gate barato (solo rutas): sin manifiesto no hay dónde declarar la dependencia. La
    // confirmación real (leer el contenido) ocurre en AnalyzeAsync, igual que AuthAnalyzer.
    public bool CanAnalyze(IReadOnlyList<RepositoryFile> files) =>
        files.Any(IsManifest);

    public async Task<AnalysisResult> AnalyzeAsync(
        Guid projectId,
        RepositoryReference repo,
        IProviderConnector connector,
        IReadOnlyList<RepositoryFile> files,
        CancellationToken ct = default)
    {
        // Primer manifiesto que declare un broker gana (nodo binario a nivel Sistema, como
        // Docker/Security — no hace falta atribuirlo a un backend concreto entre varios).
        foreach (var file in files.Where(IsManifest))
        {
            var content = await connector.GetFileContentAsync(repo, file.Path, ct);
            var broker = ResolveBroker(file.Path, content);

            if (broker is not null)
            {
                var node = new Node(
                    projectId,
                    broker,          // Name = el broker concreto (RabbitMQ/Kafka/NATS/Message Bus)
                    "MessageBus",    // Type = categoría técnica (para el color del frontend)
                    NodeCategory.Infrastructure,
                    new Dictionary<string, string> { ["source"] = file.Path });

                return new AnalysisResult(new List<Node> { node }, new List<Edge>());
            }
        }

        // Ninguna evidencia: no inventamos un bus que no está (RULES.md).
        return new AnalysisResult(new List<Node>(), new List<Edge>());
    }

    // Devuelve el broker declarado en el manifiesto, o null si no hay ninguno. Delega según
    // el tipo: substring para .csproj (parsear XML sería sobre-ingeniería), JSON para npm.
    private static string? ResolveBroker(string path, string content)
    {
        if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var (package, broker) in DotNetBrokers)
            {
                if (content.Contains(package, StringComparison.OrdinalIgnoreCase))
                {
                    return broker;
                }
            }
            return null;
        }

        return ResolveNpmBroker(content);
    }

    private static string? ResolveNpmBroker(string packageJsonContent)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(packageJsonContent);
        }
        catch (JsonException)
        {
            // Un package.json ilegible no es evidencia de nada — y como recorremos VARIOS
            // manifiestos, uno roto no debe tumbar el análisis (igual que AuthAnalyzer).
            return null;
        }

        using (doc)
        {
            foreach (var (package, broker) in NpmBrokers)
            {
                if (HasKey(doc.RootElement, "dependencies", package)
                    || HasKey(doc.RootElement, "devDependencies", package))
                {
                    return broker;
                }
            }
            return null;
        }
    }

    private static bool IsManifest(RepositoryFile file) =>
        file.Type == "blob"
        && !ContainsSegment(file.Path, "node_modules")
        && (file.Path.EndsWith("package.json", StringComparison.OrdinalIgnoreCase)
            || file.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));

    private static bool HasKey(JsonElement root, string section, string key) =>
        root.TryGetProperty(section, out var deps)
        && deps.ValueKind == JsonValueKind.Object
        && deps.TryGetProperty(key, out _);

    private static bool ContainsSegment(string path, string segment) =>
        path.Contains($"/{segment}/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith($"{segment}/", StringComparison.OrdinalIgnoreCase);
}
