using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stratalens.Domain.Entities;

namespace Stratalens.Infrastructure.Persistence.Configurations;

// Mapeo de Project a la tabla Projects. Separar la configuración en clases
// IEntityTypeConfiguration mantiene el DbContext limpio (una responsabilidad).
public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.HasKey(p => p.Id);
        // El Id lo genera el dominio (Guid.NewGuid en el constructor), no la DB.
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.OwnerUserId).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();

        // Nullable: un Project manual (CreateManual) no tiene repositorio conectado.
        // MaxLength alineado a los límites reales de GitHub (login de owner: 39;
        // nombre de repo: 100); la referencia (rama/tag/sha) puede ser más larga.
        builder.Property(p => p.RepositoryOwner).HasMaxLength(39);
        builder.Property(p => p.RepositoryName).HasMaxLength(100);
        builder.Property(p => p.RepositoryReference).HasMaxLength(255);

        builder.HasIndex(p => p.OwnerUserId);

        // T26: un usuario no puede tener dos proyectos para el mismo repo. Parcial (solo
        // cuando hay repo conectado) porque los proyectos manuales tienen los 3 campos en
        // NULL y no participan de esta invariante — nada les impide compartir nombre.
        builder.HasIndex(p => new { p.OwnerUserId, p.RepositoryOwner, p.RepositoryName })
            .IsUnique()
            .HasFilter("\"RepositoryOwner\" IS NOT NULL");

        // Shadow property: hash SHA-256 (64 chars hex) de la clave de ingesta del proyecto.
        // El dominio (Project) NO la conoce — es un detalle de seguridad/persistencia, igual
        // que el token cifrado del User. Nullable: un proyecto podría no tener clave todavía.
        // Índice único para poder localizar el proyecto por el hash en la autenticación de
        // ingesta (varios NULL conviven: Postgres no los considera duplicados en índice único).
        builder.Property<string?>("IngestKeyHash").HasMaxLength(64);
        builder.HasIndex("IngestKeyHash").IsUnique();
    }
}
