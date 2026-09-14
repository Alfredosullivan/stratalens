using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stratalens.Api.Auth;

namespace Stratalens.Api.Tests;

// Esquema de autenticación de PRUEBA: reemplaza a GitHub en los tests.
// Lee el header "X-Test-User" (un GUID) y crea una identidad con ese user id.
// Así podemos probar la autorización (ownership) sin un login real contra GitHub.
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Sin header = petición no autenticada → [Authorize] responderá 401.
        if (!Request.Headers.TryGetValue(UserHeader, out var userId) || string.IsNullOrEmpty(userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new[]
        {
            new Claim(AppClaimTypes.UserId, userId.ToString()),
            new Claim(ClaimTypes.Name, "test-user")
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
