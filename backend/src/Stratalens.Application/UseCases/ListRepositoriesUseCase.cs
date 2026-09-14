using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;

namespace Stratalens.Application.UseCases;

// Lista los repositorios de GitHub del usuario autenticado actual (T25). No hay ownership
// check aquí (a diferencia de GetProjectUseCase): no hay un Project de por medio, solo el
// usuario de la sesión — el propio IAccessTokenProvider (detrás de IProviderConnector) ya
// resuelve "de quién es este token". Delega en el connector: mantiene la capa Controller →
// UseCase → Connector uniforme con el resto, aunque hoy sea un passthrough.
public class ListRepositoriesUseCase
{
    private readonly IProviderConnector _connector;

    public ListRepositoriesUseCase(IProviderConnector connector)
    {
        _connector = connector;
    }

    public Task<IReadOnlyList<RepositorySummary>> ExecuteAsync(CancellationToken ct = default)
        => _connector.ListRepositoriesAsync(ct);
}
