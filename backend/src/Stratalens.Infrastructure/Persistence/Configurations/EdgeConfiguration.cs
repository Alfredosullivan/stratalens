using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stratalens.Domain.Entities;

namespace Stratalens.Infrastructure.Persistence.Configurations;

public class EdgeConfiguration : IEntityTypeConfiguration<Edge>
{
    public void Configure(EntityTypeBuilder<Edge> builder)
    {
        builder.ToTable("Edges");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Type).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Source).IsRequired().HasMaxLength(1000);
        builder.Property(e => e.Confidence).IsRequired();
        builder.Property(e => e.SourceType).HasConversion<string>().IsRequired().HasMaxLength(20);

        builder.Property(e => e.Metadata)
            .HasColumnType("jsonb")
            .HasConversion(MetadataConversion.Converter, MetadataConversion.Comparer);

        // Índices para las queries de dependencias ("quién sale de / llega a este nodo").
        builder.HasIndex(e => e.ProjectId);
        builder.HasIndex(e => e.SourceNodeId);
        builder.HasIndex(e => e.TargetNodeId);

        // NOTA: por simplicidad del MVP no fuerzo FKs Edge→Node. Los índices bastan
        // para las queries; la consistencia la garantiza el pipeline de análisis.
        // Se pueden añadir FKs explícitas más adelante sin cambiar el dominio.
    }
}
