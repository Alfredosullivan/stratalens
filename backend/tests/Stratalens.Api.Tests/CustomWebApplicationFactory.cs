using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stratalens.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Stratalens.Api.Tests;

// Arranca la Api REAL en memoria (WebApplicationFactory) contra un Postgres efímero
// (Testcontainers), y sustituye el esquema de auth por el de prueba. Así los tests
// ejercen el pipeline HTTP completo: routing, [Authorize], controllers, EF Core.
public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Al acceder a Services se construye el host (ya con la cadena del contenedor).
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StratalensDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                // Valores dummy para que el handler de GitHub se configure sin fallar.
                ["Authentication:GitHub:ClientId"] = "test-client-id",
                ["Authentication:GitHub:ClientSecret"] = "test-client-secret"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Sustituir el DbContext por uno apuntando al contenedor de test.
            // Se hace aquí (ConfigureTestServices corre DESPUÉS del registro de la app)
            // porque la cadena de conexión vía configuración se lee demasiado pronto.
            services.RemoveAll(typeof(DbContextOptions<StratalensDbContext>));
            services.AddDbContext<StratalensDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));

            // Añadimos el esquema de prueba y lo ponemos como predeterminado, de modo
            // que [Authorize] lo use en lugar de la cookie/GitHub reales.
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            });
        });
    }
}
