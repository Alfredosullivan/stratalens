using Stratalens.Application.Models;

namespace Stratalens.Application.Abstractions;

// Abstracción de un proveedor de código fuente (GitHub hoy; GitLab/Bitbucket futuros).
// Es el patrón "adapter/connector" que pide el Contexto maestro (secciones 18-19):
// el core no se llena de lógica específica de cada proveedor.
//
// Nota de seguridad: la autenticación (el access_token del usuario) es un detalle
// de Infrastructure. El adapter concreto obtiene las credenciales a través de
// IAccessTokenProvider (nunca hardcodeadas ni desde HttpContext directo), así que
// NINGÚN método de esta interfaz recibe tokens — no se filtran a Application.
public interface IProviderConnector
{
    Task<IReadOnlyList<RepositorySummary>> ListRepositoriesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<RepositoryFile>> GetFileTreeAsync(RepositoryReference repo, CancellationToken ct = default);

    Task<string> GetFileContentAsync(RepositoryReference repo, string path, CancellationToken ct = default);
}
