using Microsoft.EntityFrameworkCore;
using Stratalens.Domain.Entities;

namespace Stratalens.Infrastructure.Persistence;

// DbContext de la aplicación: la sesión con la base de datos.
// No contiene lógica de negocio; solo declara los conjuntos y aplica el mapeo.
public class StratalensDbContext : DbContext
{
    public StratalensDbContext(DbContextOptions<StratalensDbContext> options)
        : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Node> Nodes => Set<Node>();
    public DbSet<Edge> Edges => Set<Edge>();
    public DbSet<User> Users => Set<User>();
    // Solo se expone el aggregate root Trace: los Span se acceden a través de él
    // (Trace.Spans). Aun sin DbSet propio, EF mapea Spans por la relación + su config.
    public DbSet<Trace> Traces => Set<Trace>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Carga automáticamente todas las IEntityTypeConfiguration de este assembly,
        // en vez de configurarlas a mano aquí una por una.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StratalensDbContext).Assembly);
    }
}
