using Stratalens.Api.Auth;
using Stratalens.Application.Abstractions;

namespace Stratalens.Api.Security;

// Implementación de IAccessTokenProvider que vive en la capa web (Api), porque es la
// única que conoce HttpContext. Así Infrastructure (GitHubAdapter, en T7) sigue sin
// depender de ASP.NET: solo pide IAccessTokenProvider.
//
// Obtiene el usuario actual de la cookie y le pide su token descifrado al repositorio.
public class HttpContextAccessTokenProvider : IAccessTokenProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserRepository _users;

    public HttpContextAccessTokenProvider(IHttpContextAccessor httpContextAccessor, IUserRepository users)
    {
        _httpContextAccessor = httpContextAccessor;
        _users = users;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var userId = _httpContextAccessor.HttpContext?.User.GetAppUserId()
            ?? throw new InvalidOperationException("No hay un usuario autenticado en la petición.");

        return await _users.GetAccessTokenAsync(userId, ct)
            ?? throw new InvalidOperationException("El usuario no tiene un access_token almacenado.");
    }
}
