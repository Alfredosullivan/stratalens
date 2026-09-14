using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stratalens.Domain.Entities;

namespace Stratalens.Infrastructure.Persistence.Configurations;

public class NodeConfiguration : IEntityTypeConfiguration<Node>
{
    public void Configure(EntityTypeBuilder<Node> builder)
    {
        builder.ToTable("Nodes");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.Name).IsRequired().HasMaxLength(500);
        builder.Property(n => n.Type).IsRequired().HasMaxLength(200);

        // Category (enum) se guarda como texto legible ("Application"), no como int.
        // Más robusto: reordenar el enum no corrompe datos existentes.
        builder.Property(n => n.Category).HasConversion<string>().IsRequired().HasMaxLength(50);

        builder.Property(n => n.Metadata)
            .HasColumnType("jsonb")
            .HasConversion(MetadataConversion.Converter, MetadataConversion.Comparer);

        // Índice para la query más común: "dame los nodos de este proyecto".
        builder.HasIndex(n => n.ProjectId);
    }
}
