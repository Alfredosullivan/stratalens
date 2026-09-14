using Microsoft.EntityFrameworkCore;
using Stratalens.Application.Abstractions;
using Stratalens.Domain.Entities;

namespace Stratalens.Infrastructure.Persistence;

// Implementa IUserRepository. Aquí es donde el access_token se cifra antes de guardarse
// y se descifra al leerse, usando ITokenProtector. Quien llama trabaja siempre con
// texto plano y no se entera de la criptografía.
public class UserRepository : IUserRepository
{
    private const string TokenProperty = "EncryptedAccessToken";

    private readonly StratalensDbContext _db;
    private readonly ITokenProtector _protector;

    public UserRepository(StratalensDbContext db, ITokenProtector protector)
    {
        _db = db;
        _protector = protector;
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByGitHubUserIdAsync(string gitHubUserId, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.GitHubUserId == gitHubUserId, ct);

    public async Task<User> UpsertFromGitHubAsync(
        string gitHubUserId,
        string gitHubLogin,
        string accessToken,
        CancellationToken ct = default)
    {
        // El token entra en claro y se cifra AQUÍ, nunca antes de este punto.
        var cipher = _protector.Protect(accessToken);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.GitHubUserId == gitHubUserId, ct);
        if (user is null)
        {
            user = new User(gitHubUserId, gitHubLogin);
            _db.Users.Add(user);
        }

        // Escribimos la shadow property vía el change tracker (no es propiedad del CLR).
        _db.Entry(user).Property(TokenProperty).CurrentValue = cipher;
        await _db.SaveChangesAsync(ct);

        return user;
    }

    public async Task<string?> GetAccessTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return null;

        var cipher = (string?)_db.Entry(user).Property(TokenProperty).CurrentValue;
        return cipher is null ? null : _protector.Unprotect(cipher);
    }
}
