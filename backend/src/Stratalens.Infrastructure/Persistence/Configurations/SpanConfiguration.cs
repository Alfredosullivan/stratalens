using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stratalens.Domain.Entities;

namespace Stratalens.Infrastructure.Persistence.Configurations;

// Mapeo de Span. La FK hacia su Trace no se declara aquí: la define TraceConfiguration
// como shadow property ("TraceId" Guid → Traces.Id), porque en el dominio un Span no
// conoce a su trace. Aquí solo describimos las columnas propias del span.
public class SpanConfiguration : IEntityTypeConfiguration<Span>
{
    public void Configure(EntityTypeBuilder<Span> builder)
    {
        builder.ToTable("Spans");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.SpanId).IsRequired().HasMaxLength(200);
        builder.Property(s => s.ParentSpanId).HasMaxLength(200);   // nullable: null en el span raíz
        builder.Property(s => s.SourceNode).IsRequired().HasMaxLength(500);
        builder.Property(s => s.TargetNode).HasMaxLength(500);     // nullable
        builder.Property(s => s.Operation).IsRequired().HasMaxLength(1000);
        builder.Property(s => s.Status).IsRequired().HasMaxLength(50);

        // StartedAt: Npgsql mapea DateTime(Kind=Utc) a "timestamp with time zone" y exige
        // UTC — encaja con la invariante del dominio y garantiza round-trip en UTC.
        builder.Property(s => s.StartedAt).IsRequired();
        builder.Property(s => s.DurationMs).IsRequired();
    }
}
