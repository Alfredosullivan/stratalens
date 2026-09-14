using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stratalens.Domain.Entities;

namespace Stratalens.Infrastructure.Persistence.Configurations;

// Mapeo del aggregate root Trace. Aquí se declara que Trace POSEE sus Spans:
// la relación se configura desde la raíz, que es la dueña de la consistencia.
public class TraceConfiguration : IEntityTypeConfiguration<Trace>
{
    public void Configure(EntityTypeBuilder<Trace> builder)
    {
        builder.ToTable("Traces");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        // TraceId aquí es el identificador de OpenTelemetry (texto, 32 hex en OTLP).
        // OJO: no confundir con la columna FK "TraceId" de la tabla Spans, que es el
        // Guid que apunta a Traces.Id (ver comentario en la relación de abajo).
        builder.Property(t => t.TraceId).IsRequired().HasMaxLength(200);

        // Índice para "traces de este proyecto" y para localizar un trace concreto
        // por su id de OTel dentro del proyecto.
        builder.HasIndex(t => t.ProjectId);
        builder.HasIndex(t => new { t.ProjectId, t.TraceId });

        // Relación uno-a-muchos: un Trace tiene muchos Spans. WithOne() sin navegación
        // inversa porque Span no conoce a su Trace en el dominio (la FK es shadow).
        // Cascade: borrar un trace borra sus spans (no tienen sentido sin él).
        builder.HasMany(t => t.Spans)
            .WithOne()
            .HasForeignKey("TraceId")
            .IsRequired()   // la FK NO es nullable: refuerza en la DB "un Span pertenece a un Trace"
            .OnDelete(DeleteBehavior.Cascade);

        // EF debe leer/escribir la colección por el CAMPO privado _spans, no por la
        // propiedad Spans (que es IReadOnlyList y no tiene setter). Así respeta el
        // encapsulamiento que protege la invariante del span raíz.
        builder.Metadata
            .FindNavigation(nameof(Trace.Spans))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
