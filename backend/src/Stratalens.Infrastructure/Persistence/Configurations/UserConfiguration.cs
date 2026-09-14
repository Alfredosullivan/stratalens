using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stratalens.Domain.Entities;

namespace Stratalens.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.GitHubUserId).IsRequired().HasMaxLength(50);
        builder.Property(u => u.GitHubLogin).IsRequired().HasMaxLength(200);
        builder.Property(u => u.CreatedAt).IsRequired();

        // Shadow property: la columna del token cifrado existe en la tabla, pero NO
        // como propiedad de la entidad User. Así el dominio no conoce el secreto;
        // solo Infrastructure lo lee/escribe vía el change tracker.
        builder.Property<string>("EncryptedAccessToken").IsRequired();

        // Un usuario de GitHub = un User (evita duplicados en login repetido).
        builder.HasIndex(u => u.GitHubUserId).IsUnique();
    }
}
