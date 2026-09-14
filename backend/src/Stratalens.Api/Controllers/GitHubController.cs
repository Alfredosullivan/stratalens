using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stratalens.Api.Contracts;
using Stratalens.Application.Models;
using Stratalens.Application.UseCases;

namespace Stratalens.Api.Controllers;

// Endpoints sobre el proveedor de código (GitHub) del usuario autenticado — no sobre un
// Project concreto, por eso vive separado de ProjectsController.
[ApiController]
[Authorize]
[Route("api/v1/github")]
public class GitHubController : ControllerBase
{
    private readonly ListRepositoriesUseCase _listRepositories;

    public GitHubController(ListRepositoriesUseCase listRepositories)
    {
        _listRepositories = listRepositories;
    }

    // Repos que el usuario POSEE (T25: affiliation=owner, filtrado en GitHubAdapter).
    // No hay ownership check: el connector ya resuelve "de quién es este token" a través
    // de IAccessTokenProvider; [Authorize] es la única puerta que hace falta.
    [HttpGet("repositories")]
    [ProducesResponseType(typeof(IReadOnlyList<RepositorySummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListRepositories(CancellationToken ct)
    {
        var repos = await _listRepositories.ExecuteAsync(ct);
        return Ok(repos.Select(ToDto).ToList());
    }

    private static RepositorySummaryDto ToDto(RepositorySummary repo) =>
        new(repo.Owner, repo.Name, repo.DefaultBranch, repo.IsPrivate);
}
