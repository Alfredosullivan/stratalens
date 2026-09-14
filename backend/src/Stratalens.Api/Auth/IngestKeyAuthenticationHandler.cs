using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Stratalens.Application.Abstractions;

namespace Stratalens.Api.Auth;

// Esquema de autenticación para MÁQUINAS (el demo instrumentado), no humanos.
// A diferencia de la cookie de sesión, aquí no hay usuario ni navegador: el demo envía
// su clave de ingesta en un header. La resolvemos a un proyecto y autenticamos con un
// claim del ProjectId. Cumple RULES.md ("todo endpoint requiere auth") sin cookie.
public class IngestKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "IngestKey";
    public const string HeaderName = "X-Ingest-Key";

    private readonly IIngestKeyService _ingestKeys;
    private readonly IGraphRepository _graph;

    public IngestKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IIngestKeyService ingestKeys,
        IGraphRepository graph)
        : base(options, logger, encoder)
    {
        _ingestKeys = ingestKeys;
        _graph = graph;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Sin header = esta petición no usa este esquema → NoResult (no es un fallo,
        // deja que el pipeline responda 401 por falta de auth).
        if (!Request.Headers.TryGetValue(HeaderName, out var values))
            return AuthenticateResult.NoResult();

        var plainKey = values.ToString();
        if (string.IsNullOrWhiteSpace(plainKey))
            return AuthenticateResult.Fail("Clave de ingesta vacía.");

        // Hasheamos la clave entrante y buscamos el proyecto por el hash (nunca comparamos
        // en claro). La clave real nunca se guarda ni se loguea.
        var hash = _ingestKeys.Hash(plainKey);
        var projectId = await _graph.GetProjectIdByIngestKeyHashAsync(hash);
        if (projectId is null)
            return AuthenticateResult.Fail("Clave de ingesta inválida.");

        // Autenticado: el "principal" es el proyecto. Guardamos su id como claim para que
        // el endpoint sepa a qué proyecto asociar los traces, sin confiar en el body.
        var claims = new[] { new Claim(AppClaimTypes.ProjectId, projectId.Value.ToString()) };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
