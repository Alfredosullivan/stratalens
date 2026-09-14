using Stratalens.Domain.Entities;

namespace Stratalens.Application.Abstractions;

// Acceso a la persistencia de usuarios y su credencial de GitHub.
// El access_token se pasa/recibe en TEXTO PLANO por esta interfaz; el cifrado en
// reposo es un detalle de la implementación (Infrastructure), transparente para
// quien la use. Así Application nunca toca criptografía.
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<User?> GetByGitHubUserIdAsync(string gitHubUserId, CancellationToken ct = default);

    // Crea o actualiza el usuario tras el login OAuth, guardando su access_token
    // (la implementación lo cifra antes de persistir).
    Task<User> UpsertFromGitHubAsync(
        string gitHubUserId,
        string gitHubLogin,
        string accessToken,
        CancellationToken ct = default);

    // Devuelve el access_token YA DESCIFRADO del usuario (o null si no existe).
    Task<string?> GetAccessTokenAsync(Guid userId, CancellationToken ct = default);
}
