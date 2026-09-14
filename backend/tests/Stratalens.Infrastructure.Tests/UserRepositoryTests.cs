using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Stratalens.Infrastructure.Persistence;
using Stratalens.Infrastructure.Security;
using Testcontainers.PostgreSql;

namespace Stratalens.Infrastructure.Tests;

// Verifica el requisito de seguridad: el access_token NUNCA se guarda en texto plano.
public class UserRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
    private StratalensDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<StratalensDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        _db = new StratalensDbContext(options);
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task UpsertFromGitHub_GuardaTokenCifrado_NoEnTextoPlano()
    {
        // Arrange: EphemeralDataProtectionProvider = llaves en memoria, ideal para tests.
        var protector = new DataProtectionTokenProtector(new EphemeralDataProtectionProvider());
        var repo = new UserRepository(_db, protector);
        const string plaintext = "gho_secretoDeCarlos1234567890";

        // Act
        var user = await repo.UpsertFromGitHubAsync("999001", "carlos", plaintext);

        // Assert 1: leyendo la columna CRUDA por SQL, el token NO aparece en claro.
        var raw = await _db.Database
            .SqlQuery<string>(
                $"SELECT \"EncryptedAccessToken\" AS \"Value\" FROM \"Users\" WHERE \"Id\" = {user.Id}")
            .ToListAsync();
        Assert.Single(raw);
        Assert.DoesNotContain(plaintext, raw[0]);

        // Assert 2: el repo puede descifrarlo de vuelta correctamente.
        var decrypted = await repo.GetAccessTokenAsync(user.Id);
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public async Task UpsertFromGitHub_MismoUsuarioDosVeces_NoDuplicaYActualizaToken()
    {
        // Arrange
        var protector = new DataProtectionTokenProtector(new EphemeralDataProtectionProvider());
        var repo = new UserRepository(_db, protector);

        // Act: mismo GitHubUserId dos veces (login repetido).
        var u1 = await repo.UpsertFromGitHubAsync("999002", "carlos", "token-viejo");
        var u2 = await repo.UpsertFromGitHubAsync("999002", "carlos", "token-nuevo");

        // Assert: es el mismo usuario, sin duplicar, y con el token actualizado.
        Assert.Equal(u1.Id, u2.Id);
        Assert.Equal(1, await _db.Users.CountAsync());
        Assert.Equal("token-nuevo", await repo.GetAccessTokenAsync(u2.Id));
    }
}
