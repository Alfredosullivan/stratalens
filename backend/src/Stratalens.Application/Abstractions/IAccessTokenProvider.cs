namespace Stratalens.Application.Abstractions;

// Provee el access_token del usuario autenticado actual para llamar al proveedor
// de código (GitHub). Es la pieza que aísla a los adapters de DÓNDE sale el token.
//
// El adapter (GitHubAdapter, T7) depende de ESTA abstracción — nunca de un token
// hardcodeado ni de HttpContext directamente. Así:
//   - No hay credenciales en el código (Regla Absoluta #1 de CLAUDE.md).
//   - Infrastructure no se acopla a ASP.NET (HttpContext es de la capa web).
//   - El adapter es testeable inyectando un token falso.
//
// Su implementación concreta (T6) identificará al usuario de la petición actual
// y descifrará su token almacenado (Data Protection API). Ese "detalle" vive fuera
// de aquí, detrás de esta interfaz.
public interface IAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);
}
