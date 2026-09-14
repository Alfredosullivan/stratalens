using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Stratalens.Infrastructure.Persistence;

// Permite a las herramientas de EF (dotnet ef migrations / database update) crear el
// DbContext en tiempo de DISEÑO, sin arrancar la Api. Sin esto, "dotnet ef" no sabría
// cómo construir el contexto porque su constructor exige DbContextOptions.
//
// Lee la cadena de conexión de una variable de entorno. El valor por defecto (sin
// contraseña) sirve para GENERAR migraciones (no conecta a la DB). Para aplicarlas
// (database update) se exporta ConnectionStrings__DefaultConnection desde .env.
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<StratalensDbContext>
{
    public StratalensDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5433;Database=observability_map;Username=observability";

        var options = new DbContextOptionsBuilder<StratalensDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new StratalensDbContext(options);
    }
}
