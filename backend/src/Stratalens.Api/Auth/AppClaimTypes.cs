using System.Security.Claims;

namespace Stratalens.Api.Auth;

// Claim propio para guardar el Id de NUESTRO User en la cookie (distinto del
// NameIdentifier, que el proveedor de GitHub usa para el id de GitHub).
public static class AppClaimTypes
{
    public const string UserId = "app:user_id";
    // Id del proyecto autenticado por su clave de ingesta (esquema IngestKey).
    // En ese esquema el "principal" no es un usuario sino un proyecto: lo llama una máquina.
    public const string ProjectId = "app:project_id";
}

public static class ClaimsPrincipalExtensions
{
    // Lee el Id de nuestro usuario desde la cookie autenticada.
    public static Guid? GetAppUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst(AppClaimTypes.UserId)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    // Lee el Id del proyecto desde la identidad autenticada por clave de ingesta.
    public static Guid? GetProjectId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst(AppClaimTypes.ProjectId)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
