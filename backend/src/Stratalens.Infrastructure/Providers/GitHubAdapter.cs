using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;

namespace Stratalens.Infrastructure.Providers;

// Adapter concreto de IProviderConnector para GitHub (T7).
// Es el ÚNICO lugar del sistema que conoce la REST API de GitHub; Application y Domain
// solo ven la interfaz (patrón adapter, ARCHITECTURE.md secciones 18-19).
//
// Cómo obtiene credenciales:
//   - El access_token del usuario actual sale de IAccessTokenProvider, NO se recibe por
//     parámetro ni se hardcodea. El token se adjunta por-petición (nunca en headers por
//     defecto del HttpClient compartido, que se reutiliza entre usuarios).
//   - El token NUNCA se loguea: solo se registra método + path + status.
public class GitHubAdapter : IProviderConnector
{
    private readonly HttpClient _http;
    private readonly IAccessTokenProvider _tokenProvider;
    private readonly ILogger<GitHubAdapter> _logger;

    public GitHubAdapter(
        HttpClient http,
        IAccessTokenProvider tokenProvider,
        ILogger<GitHubAdapter> logger)
    {
        _http = http;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    // Lista los repositorios del usuario autenticado, siguiendo la paginación de GitHub
    // (header Link con rel="next") hasta agotar las páginas.
    public async Task<IReadOnlyList<RepositorySummary>> ListRepositoriesAsync(CancellationToken ct = default)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(ct);

        var summaries = new List<RepositorySummary>();

        // Primera página: 100 por página, ordenadas por actividad reciente.
        // affiliation=owner (decisión T25): solo repos que el usuario POSEE, nunca los de
        // una organización donde es colaborador/miembro — filtro nativo de GitHub, no hay
        // que replicarlo en Application comparando owners a mano.
        string? nextUri = "user/repos?per_page=100&sort=updated&affiliation=owner";

        while (nextUri is not null)
        {
            using var response = await SendAsync(HttpMethod.Get, nextUri, token, accept: null, ct);

            var page = await response.Content.ReadFromJsonAsync<List<GitHubRepositoryDto>>(cancellationToken: ct)
                ?? new List<GitHubRepositoryDto>();

            foreach (var repo in page)
            {
                summaries.Add(new RepositorySummary(
                    Owner: repo.Owner?.Login ?? string.Empty,
                    Name: repo.Name,
                    DefaultBranch: repo.DefaultBranch,
                    IsPrivate: repo.IsPrivate));
            }

            // La siguiente página viene como URL absoluta en el header Link.
            nextUri = ParseNextLink(response.Headers);
        }

        return summaries;
    }

    // Devuelve el árbol de archivos del repo en una referencia (rama/commit) concreta,
    // de forma recursiva (recursive=1) en una sola llamada.
    public async Task<IReadOnlyList<RepositoryFile>> GetFileTreeAsync(RepositoryReference repo, CancellationToken ct = default)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(ct);

        var uri = $"repos/{repo.Owner}/{repo.Name}/git/trees/{repo.Reference}?recursive=1";
        using var response = await SendAsync(HttpMethod.Get, uri, token, accept: null, ct);

        var tree = await response.Content.ReadFromJsonAsync<GitHubTreeDto>(cancellationToken: ct)
            ?? new GitHubTreeDto();

        // Si GitHub truncó el árbol (repos gigantes >100k entradas) avisamos por log.
        // El MVP no pagina el árbol; se deja anotado como límite conocido.
        if (tree.Truncated)
        {
            _logger.LogWarning(
                "El árbol de {Owner}/{Name}@{Ref} vino truncado por GitHub; puede faltar contenido.",
                repo.Owner, repo.Name, repo.Reference);
        }

        return tree.Tree
            .Select(entry => new RepositoryFile(entry.Path, entry.Type))
            .ToList();
    }

    // Lee el contenido crudo de un archivo puntual. Usa el media type "raw" para que
    // GitHub devuelva el archivo tal cual (sin envoltorio JSON ni Base64 que decodificar).
    public async Task<string> GetFileContentAsync(RepositoryReference repo, string path, CancellationToken ct = default)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(ct);

        var uri = $"repos/{repo.Owner}/{repo.Name}/contents/{path}?ref={repo.Reference}";
        var raw = new MediaTypeWithQualityHeaderValue("application/vnd.github.raw+json");

        using var response = await SendAsync(HttpMethod.Get, uri, token, accept: raw, ct);

        return await response.Content.ReadAsStringAsync(ct);
    }

    // --- Helpers privados ---

    // Construye la petición con el token del usuario, la envía y valida el status.
    // Centralizar aquí el envío garantiza que el Authorization se adjunte SIEMPRE por
    // petición (nunca como header por defecto compartido) y que el logging sea uniforme
    // y sin token.
    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string uri,
        string token,
        MediaTypeWithQualityHeaderValue? accept,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (accept is not null)
        {
            request.Headers.Accept.Add(accept);
        }

        var response = await _http.SendAsync(request, ct);

        // Log seguro: método + path + status. Nunca el token.
        _logger.LogInformation("GitHub {Method} {Path} → {Status}",
            method.Method, uri, (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw new ProviderConnectorException(
                $"GitHub respondió {(int)response.StatusCode} para {method.Method} {uri}.");
        }

        return response;
    }

    // Extrae la URL de la siguiente página del header Link de GitHub.
    // Formato: <https://api.github.com/user/repos?page=2>; rel="next", <...>; rel="last"
    private static string? ParseNextLink(HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues("Link", out var values))
        {
            return null;
        }

        foreach (var part in values.SelectMany(v => v.Split(',')))
        {
            var segments = part.Split(';');
            if (segments.Length < 2)
            {
                continue;
            }

            var isNext = segments.Skip(1).Any(s => s.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase));
            if (!isNext)
            {
                continue;
            }

            var url = segments[0].Trim().TrimStart('<').TrimEnd('>');
            return url;
        }

        return null;
    }

    // --- DTOs internos que mapean el JSON de GitHub ---
    // Son privados a Infrastructure: la forma cruda de la API no se filtra a Application,
    // que solo ve los records de Models/RepositoryModels.cs.

    private sealed class GitHubRepositoryDto
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("private")]
        public bool IsPrivate { get; init; }

        [JsonPropertyName("default_branch")]
        public string DefaultBranch { get; init; } = string.Empty;

        [JsonPropertyName("owner")]
        public GitHubOwnerDto? Owner { get; init; }
    }

    private sealed class GitHubOwnerDto
    {
        [JsonPropertyName("login")]
        public string Login { get; init; } = string.Empty;
    }

    private sealed class GitHubTreeDto
    {
        [JsonPropertyName("tree")]
        public List<GitHubTreeEntryDto> Tree { get; init; } = new();

        [JsonPropertyName("truncated")]
        public bool Truncated { get; init; }
    }

    private sealed class GitHubTreeEntryDto
    {
        [JsonPropertyName("path")]
        public string Path { get; init; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;
    }
}
