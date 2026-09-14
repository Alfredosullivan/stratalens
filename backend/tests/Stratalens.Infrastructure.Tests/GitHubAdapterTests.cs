using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Stratalens.Application.Abstractions;
using Stratalens.Application.Models;
using Stratalens.Infrastructure.Providers;

namespace Stratalens.Infrastructure.Tests;

// Test de integración del GitHubAdapter SIN red: interceptamos las llamadas HTTP con un
// HttpMessageHandler falso que devuelve JSON fijo (fixtures locales). Así el test es
// determinista, corre en CI sin token ni internet, y verificamos dos cosas:
//   1. El adapter mapea correctamente la respuesta de GitHub a los DTOs de Application.
//   2. El access_token viaja en el header Authorization pero NUNCA aparece en los logs
//      (regla de seguridad de RULES.md).
public class GitHubAdapterTests
{
    // Token falso reconocible: si aparece en cualquier log, el test debe fallar.
    private const string FakeToken = "gho_secretTokenNoDebeAparecerEnLogs";

    [Fact]
    public async Task ListRepositories_MapeaYPagina_SiguiendoElHeaderLink()
    {
        // --- Arrange ---
        // El handler responde distinto según la página pedida (simula paginación real).
        var handler = new StubHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.ToString();

            if (url.Contains("page=2"))
            {
                // Segunda (y última) página: sin header Link → el bucle termina.
                return JsonResponse("""
                    [ { "name": "repo-b", "private": true, "default_branch": "develop",
                        "owner": { "login": "carlos" } } ]
                    """);
            }

            // Primera página: incluye el header Link apuntando a la página 2.
            var response = JsonResponse("""
                [ { "name": "repo-a", "private": false, "default_branch": "main",
                    "owner": { "login": "carlos" } } ]
                """);
            response.Headers.Add("Link",
                "<https://api.github.com/user/repos?per_page=100&sort=updated&page=2>; rel=\"next\"");
            return response;
        });

        var logger = new CapturingLogger<GitHubAdapter>();
        var adapter = BuildAdapter(handler, logger);

        // --- Act ---
        var repos = await adapter.ListRepositoriesAsync();

        // --- Assert ---
        // Se combinaron las dos páginas.
        Assert.Equal(2, repos.Count);
        Assert.Equal("repo-a", repos[0].Name);
        Assert.False(repos[0].IsPrivate);
        Assert.Equal("main", repos[0].DefaultBranch);
        Assert.Equal("carlos", repos[0].Owner);
        Assert.Equal("repo-b", repos[1].Name);
        Assert.True(repos[1].IsPrivate);

        // El token viajó en el Authorization de cada petición...
        Assert.All(handler.Requests, req =>
        {
            Assert.NotNull(req.Headers.Authorization);
            Assert.Equal("Bearer", req.Headers.Authorization!.Scheme);
            Assert.Equal(FakeToken, req.Headers.Authorization.Parameter);
        });

        // ...pero NO aparece en ningún log (regla de seguridad).
        Assert.DoesNotContain(logger.Messages, m => m.Contains(FakeToken));

        // La primera petición pide SOLO repos propios (T25: no orgs, no colaboraciones).
        Assert.Contains("affiliation=owner", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetFileTree_MapeaEntradasDelArbol()
    {
        // --- Arrange ---
        var handler = new StubHttpMessageHandler(_ => JsonResponse("""
            { "truncated": false, "tree": [
                { "path": "src/App.tsx", "type": "blob" },
                { "path": "src", "type": "tree" }
            ] }
            """));

        var logger = new CapturingLogger<GitHubAdapter>();
        var adapter = BuildAdapter(handler, logger);
        var repo = new RepositoryReference("carlos", "repo-a", "main");

        // --- Act ---
        var files = await adapter.GetFileTreeAsync(repo);

        // --- Assert ---
        Assert.Equal(2, files.Count);
        Assert.Equal("src/App.tsx", files[0].Path);
        Assert.Equal("blob", files[0].Type);
        Assert.Equal("tree", files[1].Type);
        Assert.DoesNotContain(logger.Messages, m => m.Contains(FakeToken));
    }

    [Fact]
    public async Task GetFileContent_DevuelveElContenidoCrudo()
    {
        // --- Arrange ---
        const string rawContent = "export const answer = 42;";
        var handler = new StubHttpMessageHandler(request =>
        {
            // Verificamos que pedimos el media type "raw" para no lidiar con Base64.
            Assert.Contains(request.Headers.Accept,
                a => a.MediaType == "application/vnd.github.raw+json");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(rawContent)
            };
        });

        var logger = new CapturingLogger<GitHubAdapter>();
        var adapter = BuildAdapter(handler, logger);
        var repo = new RepositoryReference("carlos", "repo-a", "main");

        // --- Act ---
        var content = await adapter.GetFileContentAsync(repo, "src/config.ts");

        // --- Assert ---
        Assert.Equal(rawContent, content);
        Assert.DoesNotContain(logger.Messages, m => m.Contains(FakeToken));
    }

    [Fact]
    public async Task Send_LanzaProviderConnectorException_EnRespuestaDeError()
    {
        // --- Arrange ---
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound));

        var logger = new CapturingLogger<GitHubAdapter>();
        var adapter = BuildAdapter(handler, logger);
        var repo = new RepositoryReference("carlos", "no-existe", "main");

        // --- Act + Assert ---
        var ex = await Assert.ThrowsAsync<ProviderConnectorException>(
            () => adapter.GetFileTreeAsync(repo));

        Assert.Contains("404", ex.Message);
        // Ni siquiera en el mensaje de error se filtra el token.
        Assert.DoesNotContain(FakeToken, ex.Message);
    }

    // --- Helpers de test ---

    private static GitHubAdapter BuildAdapter(StubHttpMessageHandler handler, ILogger<GitHubAdapter> logger)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.com/")
        };
        var tokenProvider = new StubAccessTokenProvider(FakeToken);
        return new GitHubAdapter(http, tokenProvider, logger);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

    // Handler falso: ejecuta la función provista y registra cada petición para asserts.
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public List<HttpRequestMessage> Requests { get; } = new();

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responder(request));
        }
    }

    // Provider de token falso: aísla el adapter de HttpContext/DB en el test.
    private sealed class StubAccessTokenProvider : IAccessTokenProvider
    {
        private readonly string _token;
        public StubAccessTokenProvider(string token) => _token = token;
        public Task<string> GetAccessTokenAsync(CancellationToken ct = default) => Task.FromResult(_token);
    }

    // Logger que captura los mensajes formateados para poder afirmar que el token no sale.
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
