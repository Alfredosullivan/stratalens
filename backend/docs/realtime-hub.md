# Realtime — `ArchitectureMapHub` (modo LIVE)

Canal en tiempo real del modo LIVE (Fase 4). Los endpoints REST se documentan en Swagger;
un Hub de SignalR no entra en OpenAPI, así que su contrato vive aquí.

## Ruta

```
/hubs/architecture-map
```

Definida como constante `ArchitectureMapHub.Route` y mapeada en `Program.cs`.

## Autenticación

El hub lleva `[Authorize]`: la conexión se autentica con la **misma cookie de sesión**
que el resto de la API (no hay token aparte). El SPA se conecta con credenciales
(`withCredentials`); la política CORS `spa` ya permite credenciales para el origen del frontend.

## Autorización por recurso

Unirse al grupo de un proyecto exige ser su **dueño**. El check NO se decide en el hub:
se reutiliza `GetProjectUseCase` (Application), que lanza `NotFoundException` si el proyecto
no existe **o** es de otro usuario (misma respuesta para ambos: no se revela la existencia
de proyectos ajenos). El hub traduce esa excepción a `HubException` — el único tipo cuyo
mensaje SignalR reenvía al cliente sin enmascarar.

## Métodos (cliente → servidor)

| Método | Parámetro | Qué hace | Cuándo lo llama el cliente |
|--------|-----------|----------|----------------------------|
| `JoinProject` | `Guid projectId` | Une la conexión al grupo `project:{id}` tras verificar ownership. Lanza `HubException` si no es del usuario. | Al abrir el mapa de un proyecto. |
| `LeaveProject` | `Guid projectId` | Saca la conexión del grupo. Sin ownership check (salir es inocuo). | Al cerrar el mapa o cambiar de proyecto. |

## Grupos

Un grupo por proyecto, nombrado `project:{projectId}` (`ArchitectureMapHub.GroupName`).
Los eventos LIVE (T19+) se emitirán con `Clients.Group(GroupName(id)).Send(...)`, de modo
que solo llegan a los clientes que tienen ese mapa abierto.

## Eventos (servidor → cliente)

### `TraceIngested`

- **Cuándo se emite:** cada vez que se ingesta y persiste un trace vía
  `POST /api/v1/telemetry/traces` (uno por trace del batch). Lo dispara
  `IngestTelemetryUseCase` a través del puerto `ILiveTraceNotifier`; el adaptador
  `SignalRLiveTraceNotifier` (Api) lo envía **solo al grupo del proyecto** — nunca llega
  a clientes de otros proyectos.
- **Cómo se escucha (cliente):** `connection.on("TraceIngested", ev => ...)`.
- **Payload** (`LiveTraceEvent`):

```jsonc
{
  "traceId": "000102...0f",      // id del trace (hex)
  "totalDurationMs": 40,          // duración del span raíz: abarca toda la request
  "status": "OK",                 // status del span raíz ("OK" | "ERROR" | ...)
  "hops": [                       // spans ordenados por tiempo de inicio (recorrido real)
    {
      "sourceNode": "demo-backend",
      "targetNode": null,          // null si el span no es una llamada saliente
      "operation": "GET /pedidos",
      "durationMs": 40,
      "status": "OK"
    },
    {
      "sourceNode": "demo-backend",
      "targetNode": "postgresql",
      "operation": "SELECT pedidos",
      "durationMs": 12,
      "status": "OK"
    }
  ]
}
```

Es la proyección del "evento normalizado" (Contexto maestro, sección 21) lista para
animar el mapa (T22) y el timeline (T23).
