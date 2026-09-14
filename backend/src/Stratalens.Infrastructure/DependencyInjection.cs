using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stratalens.Application.Abstractions;
using Stratalens.Infrastructure.Analyzers;
using Stratalens.Infrastructure.Persistence;
using Stratalens.Infrastructure.Providers;
using Stratalens.Infrastructure.Security;

namespace Stratalens.Infrastructure;

// Punto único donde Infrastructure expone sus implementaciones al contenedor de DI.
// El Api llama a AddInfrastructure(...) sin conocer las clases concretas: solo
// registra "cuando alguien pida IGraphRepository, entrega GraphRepository", etc.
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<StratalensDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IGraphRepository, GraphRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITelemetryIngestor, TelemetryIngestor>();

        // El protector no tiene estado por petición → singleton.
        services.AddSingleton<ITokenProtector, DataProtectionTokenProtector>();

        // Generación/hash de claves de ingesta: sin estado → singleton.
        services.AddSingleton<IIngestKeyService, IngestKeyService>();

        // Adapter de GitHub como typed client: IHttpClientFactory gestiona el pool de
        // conexiones (evita agotar sockets) y aquí configuramos solo lo NO secreto.
        // El access_token no va aquí: lo adjunta el adapter por-petición desde
        // IAccessTokenProvider (registrado en la capa Api, que conoce HttpContext).
        services.AddHttpClient<IProviderConnector, GitHubAdapter>(client =>
        {
            client.BaseAddress = new Uri("https://api.github.com/");
            // GitHub exige User-Agent o responde 403.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Stratalens");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        });

        // Analyzers de lenguaje (plugin system). El pipeline los recibe como IEnumerable
        // y corre los que apliquen. Añadir un stack nuevo = registrar otro ILanguageAnalyzer.
        services.AddScoped<ILanguageAnalyzer, CSharpAnalyzer>();
        services.AddScoped<ILanguageAnalyzer, ReactTypeScriptAnalyzer>();
        services.AddScoped<ILanguageAnalyzer, NodeExpressAnalyzer>();
        services.AddScoped<ILanguageAnalyzer, DockerAnalyzer>();

        // Runner del subproceso Node (seam). NODE_ANALYZER_SCRIPT es el escape hatch para
        // despliegues donde analyzers-node no queda como hermana del checkout (Docker/CI);
        // en dev local se resuelve solo subiendo por el árbol de carpetas desde el binario
        // — el mismo truco que ya usaban los tests (ReactTypeScriptAnalyzerTests), portado
        // acá. El fallback viejo (BaseDirectory/analyzers-node/...) apuntaba a una carpeta
        // que NUNCA existe (nada la copia a bin/) y fallaba en silencio: el proceso Node
        // moría al instante por "no encuentro el script" y .NET reventaba escribiendo a un
        // stdin ya cerrado — un IOException que no decía nada sobre la causa real.
        services.AddSingleton<INodeAnalyzerRunner>(_ =>
        {
            var scriptPath = Environment.GetEnvironmentVariable("NODE_ANALYZER_SCRIPT")
                ?? LocateAnalyzerScript()
                ?? throw new InvalidOperationException(
                    "No se encontró analyzers-node/src/analyze.mjs subiendo desde " +
                    $"{AppContext.BaseDirectory}. Configurá NODE_ANALYZER_SCRIPT si el " +
                    "checkout no tiene esa carpeta como hermana del repo (ej. en despliegue).");
            return new NodeProcessRunner(scriptPath);
        });

        return services;
    }

    // Sube por el árbol de carpetas desde el binario buscando analyzers-node/src/analyze.mjs.
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
}
