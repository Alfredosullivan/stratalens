using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stratalens.Api.Auth;

namespace Stratalens.Api.Controllers;

// Endpoints de autenticación. Sin lógica de negocio: solo disparan el flujo OAuth
// (que maneja el middleware de GitHub) y exponen el estado de la sesión.
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    // Inicia el flujo "Sign in with GitHub": devuelve un Challenge que redirige a GitHub.
    // El callback (/api/v1/auth/github/callback) lo intercepta el middleware OAuth.
    [HttpGet("github/login")]
    public IActionResult Login([FromQuery] string? returnUrl)
    {
        var properties = new AuthenticationProperties { RedirectUri = returnUrl ?? "/" };
        return Challenge(properties, "GitHub");
    }

    // Devuelve el usuario autenticado actual (o 401 si no hay sesión).
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            userId = User.GetAppUserId(),
            login = User.Identity?.Name
        });
    }

    // Cierra la sesión borrando la cookie.
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
