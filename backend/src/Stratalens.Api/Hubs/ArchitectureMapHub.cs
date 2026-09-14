using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Stratalens.Api.Auth;
using Stratalens.Application.Exceptions;
using Stratalens.Application.UseCases;

namespace Stratalens.Api.Hubs;

// Hub del modo LIVE (Fase 4). Un cliente autenticado, tras abrir el mapa de un proyecto,
// se une al GRUPO de ese proyecto para recibir en vivo los eventos runtime (T19+).
//
// [Authorize] exige la MISMA cookie de sesión que el resto de la API: la conexión ya
// llega autenticada. La autorización POR RECURSO (¿este usuario es dueño de este
// proyecto?) NO se decide aquí — se reutiliza GetProjectUseCase (Application), que ya
// tiene ese check. Regla de INSTRUCTIONS.md: nunca lógica de negocio en un Hub; el Hub
// es capa de transporte, no de negocio.
[Authorize]
public class ArchitectureMapHub : Hub
{
    // Ruta pública del hub. Centralizada aquí para que el mapeo en Program.cs y cualquier
    // referencia futura usen la misma constante (no strings mágicos repetidos).
    public const string Route = "/hubs/architecture-map";

    private readonly GetProjectUseCase _getProject;

    public ArchitectureMapHub(GetProjectUseCase getProject)
    {
        _getProject = getProject;
    }

    // El cliente pide unirse al grupo de un proyecto. Solo se permite si es suyo.
    public async Task JoinProject(Guid projectId)
    {
        var userId = Context.User?.GetAppUserId();
        if (userId is null)
        {
            // [Authorize] ya lo garantiza; esto es defensa en profundidad.
            throw new HubException("No autenticado.");
        }

        try
        {
            // Reutiliza el ownership check de Application: lanza NotFoundException si el
            // proyecto no existe o es de otro usuario (misma respuesta para ambos casos,
            // no se revela la existencia de proyectos ajenos).
            await _getProject.ExecuteAsync(projectId, userId.Value, Context.ConnectionAborted);
        }
        catch (NotFoundException)
        {
            // HubException es el ÚNICO tipo cuyo mensaje SignalR reenvía al cliente sin
            // enmascarar; cualquier otra excepción el cliente la ve como error genérico.
            throw new HubException("El proyecto no existe o no es accesible.");
        }

        // El grupo se nombra por el id del proyecto: al emitir en T19 haremos
        // Clients.Group(...).Send(...) y el evento solo llega a quien tiene ese mapa abierto.
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(projectId));
    }

    // Permite salir del grupo (ej. el usuario cierra el mapa o cambia de proyecto).
    // No necesita ownership check: salir de un grupo del que no eres miembro es inocuo.
    public Task LeaveProject(Guid projectId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(projectId));

    // Convención de nombre de grupo, centralizada para reutilizarla desde el emisor (T19).
    public static string GroupName(Guid projectId) => $"project:{projectId}";
}
