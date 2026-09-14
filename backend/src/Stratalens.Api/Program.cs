using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Stratalens.Api.Auth;
using Stratalens.Api.Hubs;
using Stratalens.Api.Realtime;
using Stratalens.Api.Security;
using Stratalens.Api.Validation;
using Stratalens.Application.Abstractions;
using Stratalens.Application.Services;
using Stratalens.Application.UseCases;
using Stratalens.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// --- Infraestructura (DbContext + repositorios + protector de tokens) ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
builder.Services.AddInfrastructure(connectionString);

// Data Protection: gestiona las llaves con las que se cifra el access_token.
builder.Services.AddDataProtection();

// Acceso a HttpContext + provider del token del usuario actual (vive en la Api
// porque es lo único que conoce HttpContext; Infrastructure no se acopla a la web).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAccessTokenProvider, HttpContextAccessTokenProvider>();

// Validación de entrada (FluentValidation) — regla de RULES.md.
builder.Services.AddValidatorsFromAssemblyContaining<CreateProjectRequestValidator>();

// Pipeline de análisis (Application): el builder de grafo y el caso de uso que orquesta
// connector + analyzers + persistencia. Los analyzers y repositorios los provee AddInfrastructure.
builder.Services.AddSingleton<SystemGraphBuilder>();
builder.Services.AddScoped<RuntimeEdgePromoter>();
builder.Services.AddScoped<AnalyzeRepositoryUseCase>();
builder.Services.AddScoped<CreateProjectUseCase>();
builder.Services.AddScoped<CreateProjectFromRepositoryUseCase>();
builder.Services.AddScoped<ReanalyzeProjectUseCase>();
builder.Services.AddScoped<ListProjectsUseCase>();
builder.Services.AddScoped<GetProjectUseCase>();
builder.Services.AddScoped<GetProjectGraphUseCase>();
builder.Services.AddScoped<GetProjectTracesUseCase>();
builder.Services.AddScoped<IngestTelemetryUseCase>();
builder.Services.AddScoped<ListRepositoriesUseCase>();

// --- Autenticación: cookie (sesión) + GitHub (login externo) ---
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        // SIN DefaultChallengeScheme explícito: cae al DefaultScheme (Cookie), cuyo
        // OnRedirectToLogin devuelve 401 (abajo). Si esto apuntara a "GitHub" (como antes),
        // CUALQUIER endpoint [Authorize] sin sesión redirigiría a GitHub en vez de dar 401
        // — invisible en los tests porque CustomWebApplicationFactory sustituye este valor,
        // pero real en producción. Login() sigue pidiendo "GitHub" explícito (no depende
        // de este default), así que el flujo de login no cambia.
    })
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;                       // el JS no puede leerla → anti-XSS
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // http en local, https en prod
        // Para una API: en vez de redirigir a una página de login, devolvemos códigos.
        options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    })
    .AddGitHub(options =>
    {
        options.ClientId = builder.Configuration["Authentication:GitHub:ClientId"] ?? "";
        options.ClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"] ?? "";
        options.CallbackPath = "/api/v1/auth/github/callback";

        // Scopes mínimos (RULES.md): leer el perfil y repos públicos. Nada de admin.
        options.Scope.Add("read:user");
        options.Scope.Add("public_repo");

        // No guardamos el token en la cookie: lo persistimos cifrado nosotros.
        options.SaveTokens = false;

        // Al completar el login, creamos/actualizamos el usuario y guardamos su token
        // cifrado, y añadimos NUESTRO user id como claim de la cookie.
        options.Events.OnCreatingTicket = async context =>
        {
            var accessToken = context.AccessToken;
            var gitHubUserId = context.Identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var gitHubLogin = context.Identity?.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(accessToken) ||
                string.IsNullOrEmpty(gitHubUserId) ||
                string.IsNullOrEmpty(gitHubLogin))
            {
                return;
            }

            var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            var user = await users.UpsertFromGitHubAsync(gitHubUserId, gitHubLogin, accessToken);

            context.Identity!.AddClaim(new Claim(AppClaimTypes.UserId, user.Id.ToString()));
        };
    })
    // Esquema para máquinas: autentica el endpoint de ingesta OTLP por clave de proyecto,
    // en vez de cookie de usuario. Se activa solo donde se pide explícitamente con
    // [Authorize(AuthenticationSchemes = "IngestKey")].
    .AddScheme<AuthenticationSchemeOptions, IngestKeyAuthenticationHandler>(
        IngestKeyAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization();

// --- CORS para el SPA (frontend en Vite) ---
const string SpaCorsPolicy = "spa";
builder.Services.AddCors(options =>
    options.AddPolicy(SpaCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials())); // necesario para enviar la cookie

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// SignalR para el modo LIVE (ADR "SignalR para el modo LIVE"): canal realtime con
// reconexión y negociación de transporte (WebSocket → long polling) resueltas por el
// framework. El Hub vive en Api/Hubs. No requiere paquete: viene en el framework Web.
builder.Services.AddSignalR();

// Adaptador del puerto ILiveTraceNotifier (Application) sobre SignalR. Vive en Api
// porque es la única capa que ve el Hub. Singleton: no tiene estado por-petición e
// IHubContext ya es singleton. Emite el evento LIVE al ingestar un trace (T19).
builder.Services.AddSingleton<ILiveTraceNotifier, SignalRLiveTraceNotifier>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(SpaCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Ruta del Hub del modo LIVE. El cliente SignalR se conecta aquí con la misma cookie de
// sesión (la política CORS "spa" ya permite credenciales, necesario para el canal realtime).
app.MapHub<ArchitectureMapHub>(ArchitectureMapHub.Route).RequireCors(SpaCorsPolicy);

app.Run();

// Marcador para que WebApplicationFactory<Program> pueda referenciar el host en tests.
public partial class Program { }
