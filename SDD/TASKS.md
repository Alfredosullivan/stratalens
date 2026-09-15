# TASKS.md — Operativa Atómica

> **Qué es este archivo:** el trabajo baja aquí a unidades ejecutables con dependencias explícitas. La IA no debería trabajar con prompts aislados sin contexto de qué depende de qué — este archivo es ese grafo de dependencias. Se va llenando conforme avanza el proyecto, no de una sola vez.

## Formato de tarea

Cada tarea sigue esta estructura:

```
### [ID] — [Nombre corto]
- Depende de: [ID(s) o "ninguna"]
- Descripción: [qué hay que hacer, en una o dos líneas]
- Criterio de aceptación: [cómo se sabe que está terminada]
- Estado: pending | in-progress | done | blocked
```

## Roadmap por fases

- **Fase 0 — Fundaciones:** solución .NET con Clean Architecture, base de datos, auth con GitHub.
- **Fase 1 — Static Discovery:** analyzers (Roslyn + Node/TS), pipeline de análisis, modelo de grafo persistido.
- **Fase 2 — Visualización:** frontend con React Flow, panel de detalle de nodo, expand/collapse System↔Application.
- **Fase 3 — Runtime:** proyecto demo instrumentado con OpenTelemetry, endpoint de ingesta de traces.
- **Fase 4 — LIVE mode:** SignalR, animación de flujo en el mapa, timeline de una request.
- **Fase 5 — Plataforma multi-tenant:** elegir repo de una lista tras conectar GitHub, re-análisis, dashboard de proyectos propios, primer analyzer de infraestructura (Docker).

Este archivo se va llenando con el detalle de cada fase conforme se cierra la anterior. Fases 0, 1, 2, 3 y 4 completas (T1–T23). Fase 5 detallada a continuación, resultado de la charla de ideas post-Fase 4 (ver `SDD/PRODUCT.md` para los checkboxes de alcance que esta fase completa).

## Tus tareas

### T1 — Scaffold de la solución backend (Clean Architecture)
- Depende de: ninguna
- Descripción: Crear solución .NET con proyectos `Domain`, `Application`, `Infrastructure`, `Api`, y sus referencias según el diagrama de capas de `ARCHITECTURE.md`.
- Criterio de aceptación: `dotnet build` compila los 4 proyectos; `Api` referencia `Application`; `Infrastructure` referencia `Application`; `Domain` no referencia nada.
- Estado: done

### T2 — Scaffold del frontend
- Depende de: ninguna
- Descripción: Proyecto React + TypeScript + Vite con estructura `src/{components,pages,hooks,services,context,graph,utils}` y React Flow instalado.
- Criterio de aceptación: `npm run dev` levanta una pantalla en blanco sin errores de consola.
- Estado: done

### T3 — Entidades de dominio: Node, Edge, Project
- Depende de: T1
- Descripción: Crear `Node`, `Edge`, `Project` en Domain con sus invariantes (ej. `Edge.Source` no vacío, `Confidence < 100` salvo `SourceType = Runtime`, según `RULES.md`).
- Criterio de aceptación: Entidades creadas + unit tests de invariantes pasando (incluyendo los casos que deben fallar).
- Estado: done

### T4 — Abstracciones de Application
- Depende de: T3
- Descripción: Definir `IGraphRepository`, `IProviderConnector`, `ILanguageAnalyzer`, `ITelemetryIngestor` en Application, sin implementación concreta.
- Criterio de aceptación: Interfaces definidas y referenciadas por al menos un caso de uso (ej. `AnalyzeRepositoryUseCase` las usa sin conocer la implementación).
- Estado: done

### T5 — Persistencia: EF Core + PostgreSQL para el grafo
- Depende de: T4
- Descripción: Implementar `IGraphRepository` en Infrastructure con EF Core sobre PostgreSQL (tablas `Nodes`, `Edges`, `Projects`, metadata en JSONB).
- Criterio de aceptación: Migraciones EF Core aplican limpio contra Postgres en Docker local; integration test que persiste y recupera un `Node`/`Edge`.
- Estado: done

### T6 — Auth: GitHub OAuth
- Depende de: T1
- Descripción: Flujo "Sign in with GitHub" en Api (login/callback), cifrado del `access_token` con Data Protection API antes de guardarlo.
- Criterio de aceptación: Login real contra GitHub funciona en local; token verificado como cifrado en DB (no texto plano); test de integración de acceso cruzado entre usuarios devuelve 403/404.
- Estado: done

### T7 — GitHubAdapter (IProviderConnector)
- Depende de: T4, T6
- Descripción: Implementar `IProviderConnector` para GitHub: listar repos del usuario autenticado, leer árbol de archivos y contenido de archivos puntuales.
- Criterio de aceptación: Integration test que, contra un repo real (fixture), lista archivos y lee contenido sin exponer el token en logs.
- Estado: done

### T8 — Analyzer C# (Roslyn)
- Depende de: T4
- Descripción: Implementar `ILanguageAnalyzer` para detectar Controllers/Services/Repositories y configuración de EF Core en un repo .NET, usando Roslyn.
- Criterio de aceptación: Corriendo contra un fixture .NET real, detecta al menos Controllers → Services → DbContext con su `Source` (archivo) correcto.
- Estado: done

### T9 — Analyzer React/TS (subproceso Node + TS Compiler API)
- Depende de: T4
- Descripción: CLI en `analyzers-node/` que usa TypeScript Compiler API para detectar components/pages/hooks/services de un repo React, invocado desde Infrastructure vía `Process.Start` con contrato JSON por stdout.
- Criterio de aceptación: Corriendo contra un fixture React real, detecta estructura de carpetas y llamadas a servicios/API con su `Source` correcto; test de integración que invoca el subproceso desde .NET.
- Estado: done

### T10 — Pipeline de análisis end-to-end
- Depende de: T5, T7, T8, T9
- Descripción: Caso de uso `AnalyzeRepositoryUseCase` que orquesta: leer repo vía `IProviderConnector` → correr analyzers correspondientes → construir `Node`/`Edge` → persistir vía `IGraphRepository`.
- Criterio de aceptación: Contra el repo demo (React + ASP.NET Core + Postgres), el pipeline completo produce un grafo persistido equivalente al ejemplo de la sección 54 del contexto maestro (Frontend → Backend → PostgreSQL detectado).
- Estado: done

### T11 — Endpoint API: obtener grafo de un proyecto
- Depende de: T10
- Descripción: `GET /api/v1/projects/{id}/graph` devuelve nodos y edges del proyecto, documentado en Swagger.
- Criterio de aceptación: Test de integración que crea proyecto, dispara análisis, y verifica que el endpoint devuelve el grafo esperado.
- Estado: done

### T12 — Frontend: renderizar grafo estático con React Flow
- Depende de: T2, T11
- Descripción: Pantalla que consume `GET /projects/{id}/graph` y lo renderiza en React Flow con zoom/pan/selección.
- Criterio de aceptación: Con el grafo del repo demo, se ven los nodos Frontend/Backend/PostgreSQL conectados y son seleccionables.
- Estado: done

### T13 — Frontend: panel de detalle de nodo
- Depende de: T12
- Descripción: Al seleccionar un nodo, panel lateral con su metadata (tipo, tecnología, `Source`).
- Criterio de aceptación: Seleccionar el nodo Backend muestra tecnología ASP.NET Core y el archivo de origen detectado.
- Estado: done

## Backlog (mejoras futuras, no del MVP)

### TB1 — Analyzer React/TS con análisis semántico (Opción B)
- Depende de: T9
- Descripción: Segunda implementación de `INodeAnalyzerRunner` que vuelca los archivos a un directorio temporal y corre un `ts.createProgram` con type-checker completo, como alternativa al modo stdin sintáctico actual. Seleccionable por configuración.
- Criterio de aceptación: Con el mismo contrato JSON, produce el grafo sin tocar `ILanguageAnalyzer` ni el mapeo; limpia el temp dir siempre.
- Estado: pending

## Fase 3 — Runtime (OpenTelemetry)

Objetivo: recibir traces reales del proyecto demo y persistirlos. NO incluye el modo LIVE
(animación en el mapa, SignalR) — eso es Fase 4. Se apoya en el scaffolding de Fase 0 ya
existente: `ITelemetryIngestor`, `IngestTelemetryUseCase` y el modelo `TelemetrySpan`
(evento normalizado, sección 21 del contexto maestro).

### T14 — Entidades de dominio: Trace y Span
- Depende de: T3
- Descripción: Crear `Trace` y `Span` en Domain con sus invariantes (un `Span` pertenece a un `Trace`; `DurationMs >= 0`; `StartedAt` en UTC; un `Trace` tiene exactamente un span raíz con `ParentSpanId` nulo; los nombres del Glosario se usan tal cual). Construcción validada como en `Node`/`Edge`.
- Criterio de aceptación: Entidades creadas + unit tests de invariantes, incluyendo los casos que deben fallar (regla de `RULES.md`: toda invariante de dominio `Trace` tiene test).
- Estado: done

### T15 — Persistencia de Traces/Spans (ITelemetryIngestor con EF Core)
- Depende de: T14, T5
- Descripción: Implementar `ITelemetryIngestor` en Infrastructure con EF Core sobre PostgreSQL (tablas `Traces` y `Spans`), mapeando la lista de `TelemetrySpan` normalizados a las entidades de dominio y agrupándolos por `TraceId`. Migración EF Core nueva.
- Criterio de aceptación: La migración aplica limpio contra Postgres en Docker; integration test (Testcontainers) que ingesta una lista de spans y los recupera agrupados en su trace correcto.
- Estado: done

### T16 — Endpoint de ingesta OTLP: POST /api/v1/telemetry/traces
- Depende de: T15 (usa `IngestTelemetryUseCase` e `ITelemetryIngestor` ya existentes)
- Descripción: Endpoint que recibe spans en formato OTLP/HTTP, los normaliza a `TelemetrySpan` (sección 21) y los persiste vía `IngestTelemetryUseCase`. Documentar en Swagger. DECISIÓN PENDIENTE a resolver con Carlos al arrancar la tarea: cómo se autentica/identifica el proyecto en un endpoint que llama una máquina (el demo), no un usuario con cookie — probable clave de ingesta por proyecto (regla de `RULES.md`: todo endpoint excepto login requiere auth válida).
- Criterio de aceptación: Test de integración que hace POST de un payload OTLP de ejemplo y verifica que los spans quedan persistidos y asociados al proyecto correcto; endpoint documentado en Swagger.
- Estado: done
- DECISIÓN RESUELTA: auth máquina-a-máquina con clave de ingesta por proyecto (Opción A), hash SHA-256, esquema `IngestKey`. Formato OTLP protobuf real (Opción 1): `.proto` oficiales vendorizados + Grpc.Tools.

### T17 — Proyecto demo instrumentado con OpenTelemetry
- Depende de: T16
- Descripción: Tener una app demo mínima ejecutable (frontend → backend ASP.NET Core → Postgres) e instrumentar su backend con el OpenTelemetry .NET SDK, exportando spans vía OTLP/HTTP al endpoint propio (`/api/v1/telemetry/traces`).
- Criterio de aceptación: Al hacer una request real contra el demo, llegan traces al endpoint y quedan persistidos; se puede recuperar el trace (traceId, sus spans y la duración) asociado al proyecto.
- Estado: done
- Demo en `demo/DemoApp` (fuera de `backend.sln`). Endpoint de lectura `GET /api/v1/projects/{id}/traces` añadido. Verificado end-to-end: request real → OTLP protobuf → persistido (trace con span SERVER `GET /pedidos` + span SQL `postgresql`).

## Fase 4 — LIVE mode (SignalR)

Objetivo: cuando llega una request real con trace (Fase 3 ya la ingesta y persiste), el mapa
abierto del proyecto **reacciona en vivo** — anima el flujo (frontend → backend → base de datos),
muestra la duración total y distingue lo confirmado por runtime de lo inferido estáticamente.
Se apoya en lo ya construido: endpoint de ingesta OTLP (T16), `Trace`/`Span` persistidos (T14-T15),
el factory `Edge.FromRuntimeTrace` (Confidence=100, ya existe en Domain desde T3) y el mapa
React Flow (T12-T13). Decisión de arquitectura vinculante: SignalR con un Hub por proyecto
(`ARCHITECTURE.md`, ADR "SignalR para el modo LIVE").

### T18 — SignalR: `ArchitectureMapHub` con suscripción por proyecto
- Depende de: T6 (auth), T11 (endpoint de proyecto)
- Descripción: Crear `ArchitectureMapHub` en Api, registrar SignalR y mapear la ruta del hub. El cliente autenticado se une al **grupo** de un proyecto (`JoinProject(projectId)`) tras seleccionarlo; el hub solo permite unirse a proyectos de los que el usuario es dueño (mismo ownership check que el resto). Documentar el hub (ruta, métodos, cuándo se usa) junto a la doc de la API.
- Criterio de aceptación: Test de integración con un cliente SignalR real contra `WebApplicationFactory`: el dueño se conecta y se une al grupo de su proyecto; un usuario ajeno que intenta unirse a un proyecto que no es suyo es rechazado.
- Estado: done
- Hub en `Api/Hubs/ArchitectureMapHub.cs` (`[Authorize]`, `JoinProject`/`LeaveProject`, grupo `project:{id}`). Ownership reutiliza `GetProjectUseCase` → `NotFoundException` traducida a `HubException`. Ruta `/hubs/architecture-map`. Doc en `backend/docs/realtime-hub.md`. 3 tests (dueño OK, ajeno rechazado, sin auth rechazado) con cliente SignalR real (LongPolling sobre TestServer). Dep nueva solo en tests: `Microsoft.AspNetCore.SignalR.Client`.

### T19 — Emisión de evento LIVE al ingestar un trace
- Depende de: T18, T16
- Descripción: Al persistir un trace en `IngestTelemetryUseCase`, emitir un evento en vivo al grupo del proyecto con el evento normalizado (sección 21): `traceId`, spans ordenados (source → target por hop), duración total, status. Application no puede depender de SignalR → definir abstracción `ILiveTraceNotifier` en Application e implementarla en Api/Infrastructure con `IHubContext<ArchitectureMapHub>`. Documentar el evento (nombre, payload, cuándo se emite) — regla de `RULES.md`.
- Criterio de aceptación: Test de integración que, tras un POST de ingesta OTLP, un cliente SignalR suscrito al proyecto recibe el evento con el `traceId` y la duración correctos. El evento NO llega a clientes de otros proyectos.
- Estado: done
- Puerto `ILiveTraceNotifier` (Application/Abstractions) + payload `LiveTraceEvent`/`LiveTraceHop` (Application/Models). `IngestTelemetryUseCase` construye un evento por trace (agrupa por TraceId, ordena por StartedAt, duración+status del span raíz) y emite tras persistir. Adaptador `SignalRLiveTraceNotifier` en `Api/Realtime/` (usa `IHubContext<ArchitectureMapHub>`, emite `TraceIngested` a `Clients.Group(GroupName(projectId))`) — vive en Api porque Infrastructure no puede ver el Hub. Evento documentado en `backend/docs/realtime-hub.md`. 2 tests (dueño suscrito recibe con traceId+duración; otro proyecto NO recibe).

### T20 — Promoción de trace observado a `Edge` de runtime
- Depende de: T16, T10 (grafo)
- Descripción: A partir de los hops de un trace observado (ej. `demo-backend` → `postgresql`), crear/actualizar el `Edge` correspondiente entre los nodos del grafo usando `Edge.FromRuntimeTrace` (Confidence=100, SourceType=Runtime). Es la materialización de "Static Discovery vs Runtime Observability": un edge confirmado por un trace real deja de ser inferencia.
- DECISIÓN PENDIENTE (resolver con Carlos al arrancar): cómo mapear los nombres de nodo del trace (`SourceNode`/`TargetNode`, strings de OTel como `demo-backend`, `postgresql`) a los `Node` del grafo (que tienen `Name`/`Type`). Probable: resolución por convención + metadata, con fallback a no promover si no hay match claro (nunca inventar un edge).
- Criterio de aceptación: Test que, tras ingestar un trace cuyos hops corresponden a nodos existentes del grafo, verifica que existe un `Edge` con `Confidence=100` y `SourceType=Runtime` entre esos nodos; y que un hop sin match no crea un edge falso.
- Estado: done
- DECISIÓN RESUELTA (con Carlos): mapeo = **Opción A** (match EXACTO case-insensitive contra `Name`/`Type`/alias metadata `otelName`; sin match → no promover, nunca inventar). Promoción (Decisión 2) = **reemplazo**: el edge estático del mismo par se quita y queda solo el de runtime (idempotente si ya hay runtime). Implementación: `GraphNodeResolver` + `RuntimeEdgePromoter` (Application/Services); repo `AddEdgeAsync`/`RemoveEdgeAsync` (primitivos; política en Application). Enganchado en `IngestTelemetryUseCase` por grupo de trace (junto al notify LIVE). Type del edge por categoría del destino (Database→"SQL", resto "HTTP/REST"). 2 tests integración (promueve+supera estático; hop sin nodo→sin edge). NOTA para T22: el demo real emite `service.name=demo-backend` pero `SystemGraphBuilder` nombra el nodo backend `"Backend"`/Type `"AspNetCore"` → NO casa; para ver la promoción en vivo hay que alinear el `service.name` del demo (a `Backend`/`AspNetCore`) o sembrar `otelName` en el nodo.

### T21 — Frontend: cliente SignalR (`useLiveTraces`)
- Depende de: T18, T12
- Descripción: Instalar `@microsoft/signalr`; hook `useLiveTraces(projectId)` que se conecta al hub, se une al grupo del proyecto, maneja reconexión automática y expone los eventos LIVE recibidos. Conexión con las credenciales de la cookie (igual que el resto de llamadas).
- Criterio de aceptación: Con el mapa de un proyecto abierto y una ingesta real de trace, el hook recibe el evento LIVE (verificable en consola/estado). Reconexión tras caída sin recargar la página.
- Estado: done
- dep `@microsoft/signalr`; `frontend/src/live/types.ts` (LiveTraceEvent/LiveTraceHop, espejo del backend); hook `frontend/src/hooks/useLiveTraces.ts` (withCredentials + withAutomaticReconnect; `on('TraceIngested')`; re-`JoinProject` en `onreconnected`; estados connecting/connected/reconnecting/disconnected; console.log del evento). Integrado en `GraphPage` con badge de estado + último trace. `npm run build` y `npm run lint` OK.
- VERIFICADO runtime end-to-end (2026-09-12): con el mapa abierto (`?projectId=...`) y el demo real, el `[LIVE] TraceIngested` llegó a la consola/badge; se tumbó y relanzó el API y el cliente reconectó SIN recargar (log servidor mostró el `SELECT Projects` del re-`JoinProject`) y siguió recibiendo eventos nuevos. Nota: status real del trace = `UNSET` (convención OTel: span servidor exitoso queda UNSET, no OK).

### T22 — Frontend: animación del flujo en el mapa
- Depende de: T21, T19, T20
- Descripción: Al recibir un evento LIVE, resaltar/animar los edges del recorrido del trace en React Flow (frontend → backend → base de datos) y mostrar la duración total. Distinguir VISUALMENTE los edges confirmados por runtime (Confidence=100) de los estáticos inferidos — regla de `RULES.md`: nunca mezclar Static y Runtime sin etiquetar cuál es cuál.
- Criterio de aceptación: Con el grafo del demo abierto, una request real anima el camino frontend→backend→PostgreSQL; los edges runtime se ven distintos de los estáticos y la duración total aparece en el mapa.
- Estado: done
- DECISIÓN: mapeo hop→edge en frontend con misma convención Opción A (`resolveTracePath`, función pura). Lenguaje visual (Decisión B): estático = punteado gris `"… · N% · inferido"` (quieto); runtime = sólido teal `"… · confirmado"` (animado); resalte del último trace = ámbar grueso animado + badge `⚡ Última request: N ms`. Cabo suelto resuelto con **A1**: demo emite `service.name=Backend` (alineado al Name del nodo). Archivos: `resolveTracePath.ts` (nuevo), `mapToReactFlow.ts` (estilo por sourceType + highlightedEdgeIds), `GraphCanvas.tsx` (resalte + badge), `GraphPage.tsx` (pasa live.latest), `demoGraph.ts` (edge runtime de ejemplo). Build+lint OK. VERIFICADO end-to-end (2026-09-12): grafo sembrado por SQL en proyecto real, request del demo → camino Backend→PostgreSQL en ámbar + badge de duración + `[LIVE]` en consola + edge promovido a Runtime/100 en DB (visible teal tras recargar). GOTCHA: el primer request en frío parte los spans en 2 batches OTLP y el del hijo rompe la invariante "un solo raíz" (asunción MVP T15); con demo caliente los spans van juntos y promueve bien.

### T23 — Frontend: timeline de una request
- Depende de: T21, T19
- Descripción: Panel tipo waterfall que, para un trace (recibido en vivo o leído de `GET /projects/{id}/traces`), muestra sus spans con sus duraciones relativas y la duración total de la request.
- Criterio de aceptación: Seleccionar/recibir un trace muestra un timeline con el span raíz (backend) y el span hijo (query a la DB) proporcionales a su duración, y el total.
- Estado: done

## Housekeeping — Rename del producto a "Stratalens"

- Depende de: ninguna (transversal, no bloquea ni depende de tickets de fases)
- Descripción: Renombrar el producto de "Observability Map" a **Stratalens** (decisión tomada en la charla post-Fase 4, ver `SDD/PRODUCT.md` y memoria `direccion-post-fase4`). Alcance: namespaces `ObservabilityMap.*` → `Stratalens.*` en las 7 unidades del backend (4 `src/` + 3 `tests/`), carpetas y `.csproj` renombrados, `ObservabilityMap.slnx` → `Stratalens.slnx`, clase `ObservabilityMapDbContext` → `StratalensDbContext` (+ su `ModelSnapshot`), título/textos de UI (frontend `index.html`, `GraphPage`, demo), `docker-compose.yml` (`container_name` solamente — el nombre del **volumen** de datos se deja intacto a propósito para no perder los datos locales), `package.json`/`package-lock.json` de `analyzers-node`, y `CLAUDE.md` raíz.
- Deliberadamente NO tocado: la carpeta raíz del proyecto en disco (`C:\...\Observability Map` — renombrarla a mitad de sesión es una acción disruptiva que corresponde hacer manualmente a Carlos, no a mitad de una sesión de la IA); los dos HTML de portfolio históricos (`observability-map-fase0/1-portfolio.html`, dejados como registro de cuando el producto sí se llamaba así).
- Criterio de aceptación: cero referencias a `ObservabilityMap`/`Observability Map`/`observability-map` en código y docs fuente (verificado por barrido); `dotnet build` de `Stratalens.slnx` compila 0 errores; los 97 tests de backend pasan; `npm run build` y `npm run lint` del frontend limpios (mismos warnings preexistentes).
- Estado: done

## Housekeeping — Rediseño visual (nodos custom + panel-árbol + paleta GitHub Dark)

- Depende de: ninguna (transversal, solo frontend, no bloquea Fase 5)
- Descripción: Dirección visual decidida en la charla post-Fase 4 (ver memoria `direccion-post-fase4`), sin criterio de aceptación formal previo por ser puramente visual — se documenta aquí como registro, no como ticket con criterio previo. Tres piezas: (1) nodos custom de React Flow con estética "ventana de terminal" (`components/nodes/TerminalNode.tsx`, color por `Type` con fallback por `Category` en `graph/nodeVisuals.ts`); (2) panel-árbol lateral (`components/GraphTreePanel.tsx`) que agrupa los mismos nodos del grafo por `Category` (NO es la estructura real de directorios del repo — el grafo no la conoce hoy); (3) paleta recoloreada a los tokens reales de GitHub Dark/Primer, centralizados en `frontend/src/theme/githubDark.ts`.
- Bugs encontrados y arreglados durante la verificación: (a) los paneles laterales tapaban el header por un padding-top "mágico" en vez de un offset real — fix con constante compartida `frontend/src/layout/constants.ts` (`HEADER_HEIGHT`); (b) las etiquetas de los edges eran ilegibles por la opacidad del 75% que trae React Flow por defecto en el fondo de la etiqueta — fix con `fillOpacity:1` explícito.
- Verificado visualmente por Carlos en las tres piezas ("me gusta mucho más como quedó"). `npm run build`/`npm run lint` limpios en cada paso.
- Estado: done

## Fase 5 — Plataforma multi-tenant (GitHub repo picker + Docker discovery)

Objetivo: completar el flujo "login con GitHub → ver mis repos → elegir uno → analizar → ver
el mapa" que ya estaba en el alcance del MVP original (`PRODUCT.md`) pero sin construir, y dar
el primer paso de detección de infraestructura (Docker). Nace de la charla de ideas post-Fase 4
con Carlos. Gran parte del backend necesario YA EXISTE (verificado antes de escribir estos
tickets): `RepositorySummary`/`RepositoryReference` (Application/Models), `GitHubAdapter.
ListRepositoriesAsync` (T7), `AnalyzeRepositoryUseCase.ExecuteAsync(projectId, repo)` (T10),
y `NodeCategory.Infrastructure` en el dominio desde T3. Esta fase conecta esas piezas con
endpoints y UI nuevos, no rediseña nada existente.

Decisiones tomadas con Carlos al arrancar la fase (interrogatorio previo a estos tickets):
- Alcance de repos: **solo públicos** (mantiene el scope OAuth actual `public_repo`, sin ampliar a `repo`).
- Repos de: **solo cuenta personal** (no organizaciones) — filtrar por `affiliation=owner`.
- Modelo: **un Project = un repo fijo**; re-analizar reemplaza el mismo grafo (ya es idempotente, `SaveGraphAsync`).
- Creación manual (solo nombre, sin repo) **coexiste** con la creación desde repo — no rompe el demo OTel/LIVE actual ni sus tests.
- Ejecución del análisis: **síncrona** (la request HTTP espera el resultado) — sin infraestructura de colas/jobs nueva.
- Nombre del proyecto: **automático**, igual al nombre del repo.
- Re-análisis: **sí**, bajo demanda (botón/endpoint), sin límite.
- Primer analyzer de infraestructura: **Docker** (`Dockerfile`/`docker-compose.yml`). Kubernetes y cloud quedan para después (`PRODUCT.md`, "No en esta fase").

### T24 — Domain: `Project` conoce su repo conectado
- Depende de: T3
- Descripción: Añadir a `Project` los campos `RepositoryOwner`/`RepositoryName`/`RepositoryReference` (nullable, all-or-nothing: los tres o ninguno). Convertir el constructor público actual al patrón ya usado por `Edge` (ctor privado + factories): `Project.CreateManual(ownerUserId, name)` (equivalente al constructor de hoy) y `Project.CreateFromRepository(ownerUserId, repositoryOwner, repositoryName, repositoryReference)` (Name = repositoryName automáticamente). Migración EF Core nueva. Actualizar `CreateProjectUseCase` al nuevo factory.
- Criterio de aceptación: unit tests de invariantes (all-or-nothing de los 3 campos; ambos factories producen un `Project` válido); migración aplica limpio; los tests existentes que crean `Project` siguen pasando con el factory renombrado.
- Estado: done
- Patrón calcado de `Edge`: ctor privado sin validación (solo asigna) + factories públicos que validan cada uno lo suyo. `CreateManual` siempre pasa los 3 campos de repo en `null`; `CreateFromRepository` exige los 3 no vacíos → "all-or-nothing" se cumple por construcción (no hay un tercer camino público que deje un estado intermedio). `RepositoryName` también fija `Name` automáticamente. EF: 3 columnas nullable (`RepositoryOwner` varchar(39), `RepositoryName` varchar(100), `RepositoryReference` varchar(255), límites de GitHub). Migración `20260914043918_AddProjectRepository` generada y aplicada contra Postgres local. Actualizados: `CreateProjectUseCase` (usa `CreateManual`) + 4 archivos de test que instanciaban `Project` directo (sin builders de test, se tocó cada `new Project(...)` uno por uno). `ProjectTests.cs` reescrito: tests existentes renombrados a `CreateManual_*` + nuevos para `CreateFromRepository` (caso válido, owner vacío, y `Theory` con los 4 casos de campo faltante). Total backend: **103 tests** (69 domain + 17 api + 17 infra), todos verdes.

### T25 — Endpoint: listar repos de GitHub del usuario autenticado
- Depende de: T7, T6
- Descripción: `GET /api/v1/github/repositories` devuelve los repos del usuario autenticado vía `GitHubAdapter.ListRepositoriesAsync`, ajustando la query a `affiliation=owner` (solo cuenta personal, no orgs). Documentado en Swagger.
- Criterio de aceptación: test con `HttpMessageHandler` mockeado que devuelve repos de distintos owners; solo se listan los del usuario autenticado; sin auth → 401.
- Estado: done
- Filtro `affiliation=owner` agregado a la query de `GitHubAdapter.ListRepositoriesAsync` (param nativo de GitHub, sin cambiar `IProviderConnector` ni filtrar en Application). `ListRepositoriesUseCase` nuevo (Application) delega en el connector — passthrough hoy, mantiene la capa Controller→UseCase→Connector uniforme con el resto. `GitHubController` nuevo (`Api/Controllers`, separado de `ProjectsController` porque no hay un `Project` de por medio) con `GET /api/v1/github/repositories`, `[Authorize]` sin ownership check (el token ya resuelve de quién es). DTO `RepositorySummaryDto` nuevo en `Api/Contracts/GitHubContracts.cs` (no se expone `RepositorySummary` de Application directo). Tests: 1 nuevo en `GitHubAdapterTests` (URL incluye `affiliation=owner`) + 2 nuevos en `Api.Tests/GitHubRepositoriesTests.cs` (stub de `IProviderConnector` vía `WithWebHostBuilder`, primer uso de este patrón en el repo para no tocar `CustomWebApplicationFactory` compartida; y 401 sin auth). Total backend: **105 tests** (69 domain + 19 api + 17 infra), verdes.

### T26 — Endpoint: crear proyecto desde un repo elegido
- Depende de: T24, T25
- Descripción: Nuevo caso de uso que crea el `Project` vía `Project.CreateFromRepository` y ejecuta `AnalyzeRepositoryUseCase` de forma síncrona contra ese repo (rama por defecto del `RepositorySummary`). Endpoint `POST /api/v1/projects/from-repository { owner, name }`, distinto de `POST /api/v1/projects` (que sigue sirviendo la creación manual, T1). Invariante de negocio nueva: un usuario no puede tener dos proyectos para el mismo `(owner, name)` — índice único parcial `(OwnerUserId, RepositoryOwner, RepositoryName)` en Infrastructure.
- Criterio de aceptación: test de integración (fixture de repo vía connector mockeado) que crea el proyecto, verifica que el grafo resultante tiene nodos/edges, y que repetir la misma llamada para el mismo repo devuelve 409 (no duplica).
- Estado: done
- `CreateProjectFromRepositoryUseCase` (Application) resuelve el repo vía `ListRepositoriesAsync` PRIMERO (match case-insensitive) para obtener owner/name en el **casing canónico de GitHub** antes de chequear duplicados — evita falsos negativos por mayúsculas y evita tener que hacer la query EF case-insensitive. Orden: resolver repo → `ExistsProjectForRepositoryAsync` (chequeo "mejor esfuerzo") → `Project.CreateFromRepository` + ingest key (mismas 4 líneas que `CreateProjectUseCase`, duplicadas a propósito, regla de tres) → `AnalyzeRepositoryUseCase.ExecuteAsync` síncrono ya existente (reusado entero, no reimplementado). `ConflictException` nueva (Application/Exceptions, mismo estilo que `NotFoundException`) → 409. `IGraphRepository.ExistsProjectForRepositoryAsync` nuevo + índice único **parcial** en Postgres `(OwnerUserId, RepositoryOwner, RepositoryName) WHERE "RepositoryOwner" IS NOT NULL` (migración `20260914050823_AddProjectRepositoryUniqueIndex`) — el chequeo en el use case es UX (falla rápido), el índice es la garantía real ante condición de carrera. Endpoint `POST /api/v1/projects/from-repository` en `ProjectsController` (no un controller aparte: a diferencia de T25 esto SÍ crea un `Project`), mapea `NotFoundException`→404 (repo no existe o no es del usuario) y `ConflictException`→409, reusa `CreateProjectResponse` existente. Validator con límites de longitud iguales a las columnas (39/100) para dar 400 en vez de reventar en el INSERT. Tests nuevos en `Api.Tests/CreateProjectFromRepositoryTests.cs` (3: crea+analiza con fixture backend-only vía Roslyn real sin tocar Node, duplicado→409, repo ajeno→404), mismo patrón `WithWebHostBuilder` de T25. Total backend: **108 tests** (69 domain + 22 api + 17 infra), verdes.

### T27 — Endpoint: re-analizar un proyecto existente
- Depende de: T26
- Descripción: `POST /api/v1/projects/{id}/analyze` vuelve a ejecutar `AnalyzeRepositoryUseCase` con el repo ya asociado al proyecto (404 si el proyecto no tiene repo conectado — un proyecto manual no es re-analizable). Mismo ownership check que el resto (`GetProjectUseCase`).
- Criterio de aceptación: test que analiza, cambia el contenido del repo mockeado, re-analiza, y verifica que el grafo se reemplazó (no se duplicó) reflejando el contenido nuevo; proyecto ajeno → 404; proyecto manual (sin repo) → 404.
- Estado: done
- `ReanalyzeProjectUseCase` (Application) inyecta `GetProjectUseCase` (ownership check reusado, no reimplementado — mismo patrón que T26) + `AnalyzeRepositoryUseCase`. Si `project.RepositoryOwner` es `null` (proyecto manual, T24) lanza el mismo `NotFoundException` que "ajeno/no existe" — la Api no distingue el motivo en la respuesta HTTP. Reconstruye `RepositoryReference` desde los 3 campos ya persistidos del `Project` (garantizados all-or-nothing por el dominio). Endpoint `POST /api/v1/projects/{id}/analyze` en `ProjectsController`, devuelve `GraphResponse` 200 con el grafo YA actualizado (evita un round-trip extra del cliente). Test con connector de contenido MUTABLE (mismo objeto reutilizado entre llamadas HTTP vía closure) para simular "el repo cambió": fixture sin `DbContext` (solo nodo Backend) → re-analiza con fixture que sí tiene `DbContext` → grafo pasa a Backend+PostgreSQL+1 edge, sin duplicar — todo con Roslyn real, sin tocar Node/TS. + tests de 404 (ajeno, manual sin repo). Total backend: **111 tests** (69 domain + 25 api + 17 infra), verdes.

**FASE 5 (parcial) — T24-T27 done: dominio con repo conectado, listar repos de GitHub, crear+analizar desde repo, re-analizar bajo demanda. Quedan T28 (listar mis proyectos) y T29 (frontend) para cerrar la fase; T30 (Docker analyzer) es independiente.**

### T28 — Endpoint: listar mis proyectos
- Depende de: T3
- Descripción: `IGraphRepository.GetProjectsByOwnerAsync(userId)` + `GET /api/v1/projects` (listado, no confundir con `GET /projects/{id}`) filtrado por el usuario autenticado. Es el "dashboard" que permite volver y encontrar tus análisis previos (`PRODUCT.md`, sección Usuarios/Roles).
- Criterio de aceptación: test con dos usuarios, cada uno con proyectos propios; cada `GET /projects` devuelve solo los suyos.
- Estado: done
- `IGraphRepository.GetProjectsByOwnerAsync` (orden `CreatedAt` descendente) + `ListProjectsUseCase` (Application, sin ownership check explícito: acá la ownership ES el filtro `WHERE OwnerUserId`, no una verificación posterior). Endpoint `GET /api/v1/projects` (bare, distinto de `GET /projects/{id}`) en `ProjectsController`. Cambio de diseño (confirmado con Carlos): `ProjectDto` extendido con `RepositoryOwner`/`RepositoryName` nullable — sin esto el dashboard no podría distinguir un proyecto conectado a GitHub de uno manual (dato que T27 necesita para decidir si el botón "re-analizar" aplica). Tests: 2 usuarios con proyectos propios (cada uno ve solo los suyos), lista vacía sin proyectos, 401 sin auth. Total backend: **114 tests** (69 domain + 28 api + 17 infra), verdes.

**FASE 5 backend COMPLETA: T24-T28 done.** Queda T29 (frontend) para cerrar el flujo completo y T30 (Docker analyzer, independiente).

### T29 — Frontend: flujo Connect GitHub → dashboard → elegir repo → analizar
- Depende de: T25, T26, T28
- Descripción: Pantallas nuevas: lista de "mis proyectos" (T28) como landing tras login, y un selector de repos (T25) que dispara la creación+análisis (T26) y navega al mapa del proyecto resultante. DECISIÓN PENDIENTE a resolver al arrancar: hoy no hay router (el mapa se abre vía `?projectId=` en la URL) — un flujo multi-pantalla real probablemente necesita **React Router** (dependencia nueva a justificar explícitamente, regla de `INSTRUCTIONS.md`); evaluar si se justifica ya o si se resuelve con estado local + una sola página por ahora.
- Criterio de aceptación: login real → se ve la lista de proyectos propios (vacía la primera vez) → botón para conectar/elegir un repo → lista de repos propios → elegir uno → aparece el grafo analizado.
- Estado: done
- DECISIÓN RESUELTA (con Carlos): **React Router v6** — ya es el default del stack (`CLAUDE.md`) y evita reimplementar a mano "URL por pantalla + botón atrás + ruta protegida" para 4 pantallas reales. Rutas: `/login` (pública), `/demo` (pública, `GraphPage projectId={null}`, preserva el modo demo existente), y bajo `RequireAuth` (redirige a `/login` si `/auth/me` da 401): `/` (`DashboardPage`, T28), `/projects/new` (`NewProjectPage`, T25+T26), `/projects/:id` (`GraphPage`, adaptada de leer `?projectId=` de la URL a recibir `projectId` como prop — ya no conoce React Router). Nuevo: `apiPost` en `services/api.ts`; `authService`/`projectsService`/`githubService` + tipos espejo (`auth/types.ts`, `projects/types.ts`, `github/types.ts`); hooks `useCurrentUser`/`useProjects`/`useRepositories` (mismo patrón unión-discriminada que `useProjectGraph`).
- **Bug real encontrado en la verificación (no de T29): endpoints `[Authorize]` sin sesión redirigían 302 a GitHub OAuth en vez de dar 401.** Causa raíz: `Program.cs` tenía `DefaultChallengeScheme = "GitHub"` — invisible en los 114 tests previos porque `CustomWebApplicationFactory` sustituye ese valor. Fix: quitar el default explícito (cae a `DefaultScheme`=Cookie, cuyo `OnRedirectToLogin` ya daba 401); `AuthController.Login()` sigue pidiendo `"GitHub"` explícito, sin cambio de comportamiento en el login real. Verificado con curl real (401 antes imposible de obtener sin cookie, ahora sí).
- **Segundo hallazgo real (verificación con repos reales de Carlos, no simulados): el pipeline de análisis no detectaba NADA en repos JS/JSX puros ni backends Express** — ver housekeeping siguiente.
- Estado final: **verificado en vivo por Carlos** con su propia cuenta de GitHub (login real, dashboard, selector de repos, creación+análisis, re-análisis).

## Housekeeping — Analyzers: soporte JS/JSX puro + backend Node/Express

- Depende de: ninguna (transversal, extiende T8/T9, no bloquea T30)
- Descripción: durante la verificación de T29 con repos REALES de Carlos (no fixtures), se encontró que el pipeline de análisis (Fase 1) no detectaba nada en repos React con JSX puro (sin TypeScript) ni en backends Node.js/Express — el gap real que motivó T29 a ser mucho más que "solo frontend".
- Causa raíz 1: `ReactTypeScriptAnalyzer.CanAnalyze`/`IsTypeScript` exigían `.ts`/`.tsx`. El subproceso Node (`analyzers-node/src/analyze.mjs`) YA sabía parsear JSX (decide el `ScriptKind` por extensión) — el único bloqueo era el filtro .NET. Fix: `IsTypeScript` → `IsScriptFile`, ahora incluye `.js`/`.jsx`. `Language` se dejó como `"typescript"` a propósito (comentario explica por qué) para no tener que tocar `SystemGraphBuilder`/DI.
- Causa raíz 2: no existía NINGÚN analyzer para backends Express — solo había uno para C#. Nuevo `NodeExpressAnalyzer` (`Language="express"`, sin subproceso: lee `package.json` real vía el connector y confirma `express`/`pg` como dependencias — nunca infiere Backend solo por convención de carpetas, regla de `RULES.md`). Resuelve el `package.json` correcto en monorepos (backend en la raíz + frontend en `client/`, cada uno con el suyo) subiendo desde el primer controller/route detectado hasta encontrar su ancestro más cercano — "el primero de la lista" habría sido frágil.
- `SystemGraphBuilder.Build` generalizado: backend ahora se busca en `"csharp"` **o** `"express"` (Type del nodo = `AspNetCore`/`Express` según cuál matcheó); marcador de base de datos en `DbContext` **o** `PgPool`. Lógica compartida extraída a `BuildBackend` (antes duplicada 1:1 entre ambos analyzers).
- Causa raíz 3 (encontrada al verificar en vivo, no en tests): el runner real del subproceso Node (`NodeProcessRunner`, registrado en `DependencyInjection.AddInfrastructure`) nunca se había ejecutado de verdad para ningún proyecto de Carlos hasta este fix — sus dos repos reales son JSX puro, así que `CanAnalyze` daba `false` siempre antes de hoy. Al activarse por primera vez, falló: el fallback de resolución del script (`NODE_ANALYZER_SCRIPT` sin setear → `AppContext.BaseDirectory/analyzers-node/...`) apuntaba a una carpeta que nunca existe en `bin/`, Node moría al instante y .NET reventaba escribiendo a un stdin ya cerrado (`IOException: The pipe is being closed`, sin decir la causa real). Fix: mismo algoritmo de búsqueda hacia arriba que ya usaban los tests (`LocateAnalyzerScript`), portado a la DI real; `NODE_ANALYZER_SCRIPT` queda como escape hatch documentado para despliegues (Docker/CI) donde `analyzers-node` no es hermana del checkout.
- Limitación conocida, documentada, no resuelta (aceptada como imprecisión de heurística MVP): en un monorepo, `ReactTypeScriptAnalyzer` recibe TODOS los archivos `.js/.jsx` del repo (frontend y backend), no solo los del frontend — un archivo de servicio de BACKEND en una carpeta `services/` puede clasificarse erróneamente como un "Service" de frontend si también hace una llamada HTTP saliente. No inventa nodos falsos (el archivo existe de verdad) ni viola ninguna regla dura de `RULES.md`, pero puede producir evidencia de edge mal atribuida. Separar analyzers por subárbol del repo queda para una futura tarea si se vuelve un problema real.
- Tests nuevos: `ReactTypeScriptAnalyzerTests` (+2: `CanAnalyze` con JSX puro, payload al runner incluye `.jsx`), `NodeExpressAnalyzerTests` (nuevo archivo, 5 tests: detección, Controller+PgPool, sin `pg`→sin marcador, sin `express`→vacío, monorepo con 2 `package.json`), `NodeExpressPipelineTests` (nuevo, end-to-end con la forma EXACTA del repo real de Carlos, file tree traído con `gh api`, no inventado). `InMemoryGraphRepository` (fake de test) extraído a `TestDoubles/` compartido entre los dos tests de pipeline (antes duplicado). Total backend: **122 tests** (69 domain + 28 api + 25 infra), verdes.
- Frontend: botón "↻ Re-analizar" agregado a `GraphPage` (llama a `POST /projects/{id}/analyze` de T27, antes sin UI) — permitió verificar el fix en vivo sin recrear proyectos. `useProjectGraph` ganó un `refreshKey` opcional (mismo patrón que `useProjectTraces`).
- Estado: **done, verificado en vivo por Carlos** contra sus repos reales (airbnb-finance-assistant: Express+pg+React JSX → ahora muestra Frontend+Backend+PostgreSQL).

### T30 — Analyzer: detección de Docker
- Depende de: T4
- Descripción: `DockerAnalyzer` (`ILanguageAnalyzer`, `Language="docker"`) detecta `Dockerfile` y/o `docker-compose.yml` en el árbol del repo. Produce un `Node(Category=Infrastructure, Type="Docker")` con `Source` = la ruta del archivo detectado. DECISIÓN PENDIENTE a resolver al arrancar: si se emite también un `Edge` (ej. Backend→Docker, "containerized") o solo el nodo suelto en esta primera iteración — empezar por el nodo solo es más simple y ya aporta valor visual.
- Criterio de aceptación: fixture de repo con `Dockerfile` → el pipeline produce un `Node` de tipo Docker con su `Source` correcto; repo sin `Dockerfile` no produce ese nodo (nunca inventar infraestructura que no está).
- Estado: done
- `DockerAnalyzer` nuevo: sin subproceso ni lectura de contenido (a diferencia de `NodeExpressAnalyzer`, la sola presencia del archivo ya es evidencia suficiente, sin convención ambigua que confirmar). Prefiere `Dockerfile` sobre `docker-compose.yml` si están los dos (evidencia más directa); un solo `Node` aunque haya varios archivos ("¿se containeriza?" es binario, no "cuántos"). A diferencia de los demás analyzers, entrega el `Node` YA a nivel Sistema (`Category=Infrastructure`) — no hay detalle fino que agregar, así que `SystemGraphBuilder.Build` lo pasa tal cual (generalización nueva: `resultsByLanguage["docker"]` → `nodes.AddRange` directo).
- DECISIÓN RESUELTA (con Carlos, ampliando el alcance original del ticket): **sí** se emite el edge Backend→Docker (`"containerized"`, confidence 90 — más alta que HTTP/REST y SQL porque la evidencia es directa, un archivo que existe, no una inferencia sobre texto; nunca 100, eso es exclusivo de runtime). Solo se crea si hay Backend detectado (nunca inventar el otro extremo de un edge).
- **Bug de layout encontrado y arreglado en la verificación en vivo** (con screenshot real de Carlos): la etiqueta `"containerized · 90% · inferido"` es más larga que las etiquetas anteriores (`HTTP/REST`, `SQL`) y no entraba en el hueco entre `Backend` y `Docker` (ambos quedan en la misma fila, edge horizontal corto) — se cortaba contra el borde del nodo `Docker`. Mismo síntoma que ya se había arreglado una vez en el rediseño post-Fase 4 subiendo `LAYER_WIDTH`; se repitió la misma solución (`340→400` en `mapToReactFlow.ts`).
- Tests nuevos: `DockerAnalyzerTests` (5: `CanAnalyze` con Dockerfile/compose/ninguno, `Node` con `Source` correcto, prefiere Dockerfile sobre compose sin duplicar), `SystemGraphBuilderTests` (nuevo archivo, 1: Docker sin Backend detectado → nodo suelto sin edge, `SystemGraphBuilder` probado directo por ser función pura). `NodeExpressPipelineTests` extendido con `Dockerfile` real (mismo fixture que ya imita a `airbnb-finance-assistant`) para probar los 4 nodos + 3 edges end-to-end. Total backend: **128 tests** (69 domain + 28 api + 31 infra), verdes.
- Estado final: **verificado en vivo por Carlos** contra `airbnb-finance-assistant` (tiene un `Dockerfile` real) — nodo Docker + edge Backend→Docker visibles y legibles tras el fix de layout.

**ROADMAP ORIGINAL (T1-T30) TERMINADO Y VERIFICADO EN VIVO.** Ver Fase 6 más abajo para el trabajo nuevo.

## Fase 6 — Analyzers de infraestructura ampliada (Security, Message Bus, Workers, Cloud)

Objetivo: ampliar el "ancho" de lo que el pipeline de análisis detecta, más allá de
Frontend/Backend/Database/Docker. Nace de comparar Stratalens contra Archify (proyecto de
GitHub con la misma idea de fondo): su mapa muestra categorías que el nuestro no tiene
(Security & Identity, Message Bus, Workers, Cloud Infrastructure). El objetivo NO es copiar
su enfoque (ellos delegan la detección en un LLM narrando sobre una descripción de texto,
sin analizador real) — es cerrar el gap real: nuestros analyzers usan compiladores/manifiestos
reales (Roslyn, TS Compiler API, `package.json`), así que cada categoría nueva es un analyzer
nuevo que seguirá esa misma barra de evidencia (regla de `RULES.md`: nunca un `Node`/`Edge`
sin `Source`).

Hallazgo de diseño (verificado leyendo `SystemGraphBuilder.cs` antes de planear el ticket):
agregar un analyzer nuevo NO alcanza por sí solo. `SystemGraphBuilder.Build` es el único lugar
que decide qué detalle fino de un analyzer se "promueve" a nodo visible en el mapa — el resto
queda solo como evidencia (`Source`) de otro edge, nunca se dibuja (así es como Controllers/
DbContext/PgPool ya funcionan hoy, línea 10 del archivo). Docker (T30) es visible porque tiene
su propio bloque en `SystemGraphBuilder.Build` que lo promueve directo a nivel Sistema. Cada
categoría nueva de esta fase necesita el mismo patrón: analyzer + bloque en `SystemGraphBuilder`.

Orden decidido con Carlos: empezar por Security/Auth (la categoría que más le interesó del
ejemplo de Archify). Message Bus, Workers y Cloud Infrastructure quedan como próximos tickets
de esta misma fase, mismo patrón, uno a la vez — no se detallan todavía (regla de
`INSTRUCTIONS.md`: este archivo se llena conforme avanza el proyecto, no de una sola vez).

Decisiones tomadas con Carlos al arrancar la fase (interrogatorio previo a estos tickets):
- Categoría de dominio: **nuevo valor `NodeCategory.Security`** (no reusar `External` — JWT
  local no es un servicio de terceros, es una capacidad que vive dentro del propio backend;
  modelarlo como `External` sería semánticamente incorrecto).
- Alcance v1 del analyzer de auth: **solo JWT local** (`jsonwebtoken`/`passport-jwt` en Node,
  `Microsoft.AspNetCore.Authentication.JwtBearer` en .NET). Identity providers de terceros
  (Auth0, Okta, Firebase Auth) quedan fuera de v1 y son un ticket futuro aparte — mezclarlos
  en el mismo ticket rompería la convención de "una tarea a la vez" (evidencia de dependencia
  de código vs. evidencia de SDK de un proveedor externo son dos cosas distintas).

### T31 — Domain: nuevo `NodeCategory.Security`
- Depende de: T3
- Descripción: agregar `Security` a `Domain/Enums/NodeCategory.cs` (comentario con ejemplos:
  JWT, OAuth, Identity Provider — mismo estilo que los valores existentes). Mapear su color en
  `frontend/src/graph/nodeVisuals.ts` (`CATEGORY_FALLBACK_COLORS`): necesita un tono propio,
  no reusar el `github.attention` (amarillo) que ya usan Infrastructure/DevOps/Deployment —
  reusarlo confundiría visualmente un nodo Security con un nodo Docker.
- Criterio de aceptación: el enum compila con el valor nuevo; ningún analyzer lo usa todavía
  (lo consume T32); `nodeVisuals.ts` tiene un color para `Security` que no choca con los
  existentes (verificar contra la paleta de `theme/githubDark.ts`); `dotnet build` y
  `npm run build`/`npm run lint` del frontend limpios.
- Estado: done
- `NodeCategory.Security` agregado al enum (Domain) con comentario de ejemplos (JWT/OAuth/
  identidad DENTRO del backend, para distinguirlo de `External` que es terceros). Color nuevo:
  token `sponsors` (`#db61a2`, el rosa REAL de Primer/GitHub Sponsors — no un hex inventado,
  respeta la regla de `githubDark.ts`) agregado a la paleta y mapeado en
  `nodeVisuals.ts.CATEGORY_FALLBACK_COLORS` (no en `TYPE_COLORS`: el Type concreto `"JWT"` lo
  mapea T32 cuando exista el analyzer). Rosa elegido por ser el único distinto de los 5 en uso
  (azul/morado/verde/amarillo/rojo-reservado) y por coincidir con el reddish-pink que Archify
  usa para su caja "Security & Identity". Verificado: `dotnet build` 0/0; `npm run build` OK;
  lint solo con los 6 warnings preexistentes (`set-state-in-effect`), ninguno nuevo.

### T32 — Analyzer: detección de autenticación JWT (`AuthAnalyzer`)
- Depende de: T31, T4, T10 (pipeline), extiende el patrón de T30 en `SystemGraphBuilder`
- Descripción: `AuthAnalyzer` nuevo (`ILanguageAnalyzer`, `Language="auth"`) detecta JWT local
  por dependencia real, nunca por convención de carpetas (regla de `RULES.md`, mismo criterio
  que `NodeExpressAnalyzer.HasDependency` para `express`/`pg`):
  - Node: `jsonwebtoken` o `passport-jwt` como dependencia en el `package.json` del backend
    (reusar la resolución de "package.json más cercano" que ya tiene `NodeExpressAnalyzer` para
    monorepos, no reimplementarla).
  - .NET: `Microsoft.AspNetCore.Authentication.JwtBearer` como `PackageReference` dentro de un
    `.csproj` — primera vez que un analyzer de C# lee contenido de `.csproj` en vez de solo
    Roslyn; usar `connector.GetFileContentAsync` igual que Express, no agregar una dependencia
    nueva para parsear XML si un check de substring alcanza.
  - Produce un `Node(Category=Security, Type="JWT")` YA a nivel Sistema (igual que Docker en
    T30 — sin detalle fino que agregar en el MVP).
  - `SystemGraphBuilder.Build` gana un bloque nuevo (mismo shape que el bloque Docker, líneas
    94-108 de hoy) que agrega el nodo Security si el analyzer lo detectó, más
    `Edge Backend→Security` SOLO si hay Backend detectado (nunca inventar el otro extremo).
- DECISIÓN PENDIENTE a resolver con Carlos al arrancar la tarea: label y `Confidence` exactos
  del edge (ej. `"authenticates"` o `"protected by"`; Confidence — ¿90 como Docker, mismo
  criterio de "evidencia directa de manifiesto", o distinto? se decide al empezar, no acá).
- Criterio de aceptación: fixture Node con `jsonwebtoken` en `package.json` → aparece
  `Node(Security, JWT)` + `Edge Backend→Security` con `Source` = ruta del `package.json`;
  fixture .NET con `JwtBearer` en `.csproj` → mismo resultado con `Source` = ruta del `.csproj`;
  repo sin ninguna de las dos dependencias → el nodo Security NO aparece (nunca inventar);
  test en `SystemGraphBuilderTests` (mismo archivo de T30) para "Security sin Backend
  detectado → nodo suelto sin edge".
- Estado: done
- DECISIÓN RESUELTA (con Carlos): label del edge = **"secured by"** (`"Backend secured by JWT"`,
  se lee natural en la dirección de la flecha); Confidence = **90**, igual que Docker (misma
  clase de evidencia: dependencia declarada en un manifiesto real, no inferencia sobre texto).
- `AuthAnalyzer` nuevo (`Language="auth"`): mismo patrón de gate barato + confirmación por
  contenido que `NodeExpressAnalyzer`. Detecta `jsonwebtoken`/`passport-jwt` en cualquier
  `package.json` (JSON parse, deps o devDeps) o `Microsoft.AspNetCore.Authentication.JwtBearer`
  en cualquier `.csproj` (substring — primer analyzer que lee `.csproj` como texto; parsear XML
  sería sobre-ingeniería, el nombre del paquete es único). Excluye `node_modules/`. Entrega UN
  `Node(Security, "JWT")` a nivel Sistema (binario "¿usa JWT?", como Docker).
- **Desviación honesta del plan original del ticket:** el ticket sugería reusar la resolución
  "package.json más cercano" de `NodeExpressAnalyzer`. Al implementarlo se vio que esa lógica
  solo importa para atribuir el hallazgo a UN backend concreto entre varios — como el nodo es
  binario a nivel Sistema, con la primera evidencia basta; reusarla habría acoplado dos
  analyzers sin ganar corrección. Se recorren todos los manifiestos, primer match gana, Source
  = ese manifiesto. Documentado en el código. Parse JSON envuelto en try/catch (a diferencia de
  Express que lee un único package.json ya resuelto: acá se recorren varios, uno roto no debe
  tumbar el análisis).
- `SystemGraphBuilder.Build`: bloque nuevo calcado del de Docker (`resultsByLanguage["auth"]`
  → promueve el nodo + edge `Backend→Security` "secured by" confidence 90 SOLO si hay Backend).
  Constante `BackendToSecurityConfidence = 90`.
- DI: `AuthAnalyzer` registrado como `ILanguageAnalyzer` en `AddInfrastructure`.
- Frontend: `NodeCategory.Security` ya coloreado en T31; T32 agregó `JWT` a `TYPE_COLORS`
  (`nodeVisuals.ts`, consistente con los otros Types reales) y `'Security'` a `CATEGORY_ORDER`
  (`GraphTreePanel.tsx`, entre Database e Infrastructure). El layout (`computeLayers` en
  `mapToReactFlow.ts`) es genérico por flechas, así que el nodo Security cae solo a la derecha
  del Backend sin tocar el layout. `NodeDetailPanel` muestra "JWT" tal cual (fallback agnóstico
  ya existente, sin cambio).
- Tests: `AuthAnalyzerTests` (8: CanAnalyze package.json/csproj/ninguno; Analyze con
  jsonwebtoken/passport-jwt/JwtBearer-csproj → nodo Security con Source correcto; sin auth →
  vacío; monorepo → Source del manifiesto correcto), `SystemGraphBuilderTests` (+1: Security
  sin Backend → nodo suelto sin edge), `NodeExpressPipelineTests` extendido (fixture con
  `jsonwebtoken` real + `AuthAnalyzer` en la lista → 5 nodos + 4 edges end-to-end, verifica el
  edge `secured by` con Source y Confidence 90). Total backend: **137 tests** (69 domain +
  28 api + 40 infra), verdes. Frontend `npm run build`/`lint` limpios (solo warnings
  preexistentes). **Verificado en vivo por Carlos** (2026-09-14) contra su repo real
  `airbnb-finance-assistant` (monorepo con `jsonwebtoken ^9.0.3` en el `package.json` del
  backend y sin auth en `client/`): el nodo rosa "JWT" (Category=Security) aparece a la
  derecha del Backend con el edge `Backend→JWT` "secured by · 90% · inferido", sin falso
  positivo en el frontend. Confirmado además autónomamente vía `gh api` que el repo real
  declara esa dependencia (input idéntico al del pipeline test).

## Fase 6 (profundidad) — Jerarquía de nodos (expand/collapse Backend → sus componentes)

Objetivo: cerrar el OTRO gap con Archify, complementario al de los analyzers (ancho): la
**profundidad**. Hoy el grafo es plano (`PRODUCT.md` línea 27: "el MVP solo muestra el nivel 2
Application fijo"). Archify muestra sub-partes dentro de un componente (Security contiene Auth
Service + JWT). Queremos lo mismo: expandir un nodo y ver los componentes que viven dentro.

Diseño de datos (propuesto por Carlos, revisado y aprobado — Método Mixto Regla 1): patrón
**Adjacency List** — un campo `ParentNodeId` nullable en `Node` (null = raíz como Backend; el
id del padre = hijo como un Controller). Decisiones de modelado acordadas:
- **Contención ≠ conexión:** la relación padre-hijo NO es un `Edge`. Los edges siguen siendo
  conexiones de comportamiento (Backend→DB); la jerarquía es composición estructural, aparte.
- **Máximo un padre por construcción:** un solo campo `ParentNodeId` lo garantiza (no se puede
  guardar dos padres en un campo) — "estado inválido irrepresentable", no hay que validarlo.
- **Invariante a validar:** un nodo no puede ser su propio padre (self-reference). Ciclos en
  cadena (A→B→A) son casi imposibles en la práctica porque el builder asigna padres de arriba
  hacia abajo, pero la invariante de self-reference se guarda igual.

Piloto elegido con Carlos: **Backend → sus Controllers/Services** (no Security → Auth Service/
JWT todavía). Razón: `CSharpAnalyzer`/`NodeExpressAnalyzer` YA detectan Controllers/Services
(Category=Code) pero `SystemGraphBuilder` hoy los DESCARTA (los usa solo como evidencia, líneas
10 y 65-70 de `NodeExpressAnalyzer.cs`). Estrenamos toda la plomería de jerarquía (schema +
builder + render anidado) reusando detección existente, sin analyzer nuevo — mínimo riesgo.
Security-con-hijos y otras contenciones quedan de follow-up una vez probada la plomería.

### T33 — Domain + persistencia + contrato API: `Node.ParentNodeId` (fundación)
- Depende de: T3
- Descripción: agregar `ParentNodeId` (`Guid?`, nullable) a `Node` (Domain) con invariante de
  self-reference (un nodo no puede declararse su propio padre) — patrón del constructor
  validador que ya usa `Node`. EF: columna nullable en `NodeConfiguration` + migración (DECISIÓN
  PENDIENTE al arrancar: ¿FK self-referencial real con `OnDelete` explícito, o columna `Guid?`
  simple sin constraint de FK para evitar complejidad de cascada? — empezar por lo más simple
  que sea correcto). Exponer `ParentNodeId` (nullable) en `GraphNodeDto` (`ProjectContracts.cs`)
  + su mapeo, y en el tipo `GraphNode` del frontend (`graph/types.ts`, `parentNodeId?: string |
  null`). SIN cambio de comportamiento: nada asigna padre todavía (todos null) — fundación pura,
  como fue T31.
- Criterio de aceptación: unit test de la invariante (self-parent rechazado) + los casos válidos
  (padre null y padre = otro id); migración aplica limpio contra Postgres; los 137 tests
  existentes siguen pasando; el endpoint del grafo devuelve `parentNodeId` (null para todos hoy);
  `dotnet build` y `npm run build`/`lint` limpios.
- Estado: done
- DECISIÓN RESUELTA (con Carlos): **columna `Guid?` simple, sin FK** self-referencial. La
  integridad la da el agregado (el grafo se guarda/borra como unidad atómica por proyecto en
  `SaveGraphAsync`); una FK real chocaría con el `ExecuteDeleteAsync` masivo (Postgres verifica
  integridad por fila → borrar padre e hijos en un statement violaría la FK a mitad de camino).
- HALLAZGO al implementar (Modo Enseñanza, ajuste honesto del criterio): la invariante
  "self-parent rechazado" resultó **imposible de violar por construcción** — `Node` genera su
  `Id` DENTRO del ctor con `Guid.NewGuid()`, así que nadie puede pasar un `ParentNodeId` igual a
  un `Id` que aún no existe. Un `if (parentNodeId == Id) throw` sería código muerto e
  intesteable. Se documentó el porqué en `Node.cs` y los tests cubren lo que SÍ es real: raíz
  (padre null) e hijo (padre = id de otro nodo). Los ciclos A→B→A no son chequeables a nivel de
  un nodo suelto; los previene el builder (T34).
- `Node.ParentNodeId` (`Guid?`) agregado como parámetro opcional AL FINAL del ctor → todos los
  call sites existentes siguen compilando (caen a null = raíz). EF: `builder.Property(n =>
  n.ParentNodeId)` en `NodeConfiguration`, sin `HasOne`/FK. Migración `AddNodeParentNodeId`
  (solo `AddColumn<Guid> nullable uuid`, verificada antes de aplicar) aplicada limpio contra
  Postgres. `GraphNodeDto` + su único mapeo en `ProjectsController.ToDto` exponen `ParentNodeId`
  (nullable). Frontend `GraphNode` ganó `parentNodeId?: string | null` (opcional → demoGraph y
  el resto no rompen). SIN comportamiento nuevo: todos los nodos tienen padre null hoy (nada lo
  asigna hasta T34).
- Tests: `NodeTests` +2 (raíz con padre null; hijo guarda el id del padre). Total backend: **139
  tests** (71 domain + 28 api + 40 infra), verdes. Frontend `npm run build`/`lint` limpios (solo
  warnings preexistentes). Nota operativa: `dotnet ef` usa `DesignTimeDbContextFactory` con
  `--startup-project src/Stratalens.Infrastructure` (el paquete Design vive ahí, no en Api); para
  `database update` se exporta `ConnectionStrings__DefaultConnection` desde `.env`.

### T34 — Application: `SystemGraphBuilder` expone los hijos del Backend
- Depende de: T33, T10 (pipeline)
- Descripción: hoy `SystemGraphBuilder` descarta los nodos finos (Controllers/Services) que
  producen los analyzers. Cambiar para que los Controllers/Services detectados se PERSISTAN como
  hijos del nodo Backend (`ParentNodeId = backend.Id`), sin dejar de construir el nodo Backend
  grueso como hoy. Los marcadores de DB (DbContext/PgPool) NO son hijos del Backend (son
  evidencia del nodo PostgreSQL) — se siguen descartando como detalle. DECISIONES PENDIENTES al
  arrancar: (1) estabilidad de Id — `Node` es inmutable; para setear el padre en un nodo que ya
  creó el analyzer, ¿el builder recrea el hijo (Id nuevo) o se agrega un factory que preserve el
  Id? (afecta si algún día se quieren los edges entre hijos); (2) los edges inter-hijos (DI de
  `CSharpAnalyzer`) NO se surfacean en este ticket — solo el anidamiento; edges de detalle son
  follow-up.
- Criterio de aceptación: pipeline test — un repo con 2 controllers → el grafo tiene el Backend
  + 2 nodos hijos con `ParentNodeId = backend.Id`; los nodos gruesos (Frontend/DB/Docker/JWT) no
  cambian; los hijos no se duplican; un repo sin controllers detectados → Backend sin hijos (no
  inventar).
- Estado: done
- DECISIÓN RESUELTA (con Carlos): **recrear el hijo con Id nuevo** (no factory que preserve Id).
  El builder crea un `Node` nuevo copiando Name/Type/Category/Metadata del que detectó el
  analyzer, con `parentNodeId = backend.Id`. Suficiente porque T34 NO surfacea edges entre hijos
  (lo único que necesitaría Ids estables); el análisis es idempotente (regenera todo cada vez),
  así que recrear no tiene coste de datos. Además "quién es hijo de quién" es decisión del
  builder (Application), su capa correcta.
- Implementación en `SystemGraphBuilder.BuildBackend`: su tupla de retorno ganó un tercer
  miembro `IReadOnlyList<Node> Children` = los nodos finos del analyzer EXCEPTO el marcador de
  DB (`result.Nodes.Where(n => n.Type != dbMarkerType)`), re-emitidos con `parentNodeId =
  backend.Id`. El marcador de DB (DbContext/PgPool) queda excluido a propósito (es evidencia del
  nodo PostgreSQL, no un componente interno). `Build` agrega los hijos con `nodes.AddRange`
  (rama sin backend → `Array.Empty<Node>()`). Los edges NO cambian (sin edges entre hijos en
  este ticket).
- Tests: `SystemGraphBuilderTests` +1 (backend con Controller+DbContext → Controller cuelga del
  Backend con ParentNodeId, DbContext excluido/→PostgreSQL). Tres tests existentes que asertaban
  conteos exactos se actualizaron porque el comportamiento cambió A PROPÓSITO (los hijos ahora
  son parte del grafo), no por bug: `AnalyzeRepositoryPipelineTests` (3→6 nodos: +3 hijos
  Controller/Service/Repository del fixture C#), `NodeExpressPipelineTests` (5→7: +2 controllers
  del fixture Express) y `ReanalyzeProjectTests` (V1 1→2 con el hijo; V2 2→5 con los 3 hijos).
  Total backend: **140 tests** (71 domain + 28 api + 41 infra), verdes.
- NOTA visual: tras T34 los hijos ya viajan en el grafo pero el frontend aún NO los anida
  (mapToReactFlow no mapea parentNodeId → parentId hasta T35), así que renderizarían PLANOS y
  sin edges. Por eso NO se relanzó la app entre T34 y T35 — la verificación en vivo se hace al
  cerrar T35.

### T35 — Frontend: render anidado + expand/collapse
- Depende de: T34, T12
- Descripción: `mapToReactFlow` emite `parentId` + `extent:'parent'` para los nodos hijos y los
  posiciona relativos al padre (feature nativa de React Flow: sub-flows). Expand/collapse:
  clicar el nodo padre alterna la visibilidad de sus hijos. DECISIONES PENDIENTES al arrancar:
  (1) UX por defecto — ¿colapsado con un badge de conteo ("+3") como Archify, o expandido?;
  (2) cómo dimensionar el nodo contenedor para envolver a los hijos al expandir. Distinguir
  visualmente "contiene" (anidamiento) de los edges de conexión — la contención NO es una flecha.
- Criterio de aceptación: con el grafo de un repo con controllers, el Backend se ve expandible;
  expandir revela los Controllers/Services anidados dentro; colapsar los oculta; los edges
  gruesos (Frontend→Backend, Backend→DB, etc.) siguen renderizando bien.
- Estado: done, verificado en vivo por Carlos (2026-09-14) contra `airbnb-finance-assistant`
  (13 controllers/routes → badge `⊕ 13`, expand a grid de chips, colapsar OK).
- DECISIONES RESUELTAS (con Carlos): (1) render = **contención real** (React Flow sub-flows:
  el Backend se vuelve un contenedor y los hijos viven dentro vía `parentId`+`extent:'parent'`);
  (2) default = **colapsado con badge `⊕ N`** (overview a nivel Sistema primero).
- Implementación: `mapToReactFlow` parte los nodos en raíces (layout izq→der) e hijos (dentro
  del padre); solo emite hijos si el padre está expandido; solo edges entre nodos visibles.
  Estado de expansión en `GraphCanvas` (`Set` de ids, `useCallback` estable). Nodos nuevos:
  `ContainerNode` (Backend expandido: caja con header + chip `⊖`) y `ComponentChip` (hijo
  minimalista: punto + nombre). `TerminalNode` ganó el chip `⊕ N` (con `stopPropagation` para
  no disparar la selección). Registrados los 3 tipos en `nodeTypes`.
- AJUSTES tras la 1ª verificación visual de Carlos (dos hallazgos con screenshot):
  - **Hijos compactos:** 13 tarjetas apiladas parecían una lista enorme → los hijos pasaron de
    `TerminalNode` completo a `ComponentChip` compacto en un **grid** de hasta `GRID_COLS=3`
    columnas (estilo Archify). El contenedor se dimensiona al grid.
  - **Overlap al expandir:** el contenedor ancho (~498px > el `LAYER_WIDTH` fijo de 400) se
    encimaba con PostgreSQL/Docker. Fix: layout **SIZE-AWARE** — la X de cada capa parte del
    ancho REAL de la capa anterior (`maxWidthByLayer` acumulado + `H_GAP`) y dentro de una capa
    los nodos se apilan por su alto real (`yCursorByLayer` + `V_GAP`); se eliminaron
    `LAYER_WIDTH`/`ROW_HEIGHT` fijos. Un contenedor ancho ahora empuja las capas siguientes a la
    derecha sin solaparse.
- AJUSTE de modelado (con Carlos, afecta también a T32): la dirección del edge de seguridad se
  invirtió a **Security→Backend "validates"** (antes Backend→Security "secured by"). Sigue la
  narrativa de arquitectura de Archify ("Validate Token"): la seguridad se ubica del lado de
  ENTRADA y valida las requests hacia el backend, en vez de leerse como dependencia de librería.
  Confidence 90 sin cambio. `SystemGraphBuilder` + `NodeExpressPipelineTests` actualizados.
- DECISIÓN DE PRODUCTO (con Carlos): Carlos pidió `Frontend→Security→Backend` (seguridad en el
  camino de la request). Se **rechazó dibujar `Frontend→Security`** porque NO hay evidencia real
  de que el frontend hable con la auth (la única fuente del nodo JWT es el `package.json` del
  backend) — hacerlo violaría la regla dura de `RULES.md` ("nunca un edge sin Source, nunca
  inventar"), el diferenciador central del producto vs Archify. Alternativa honesta anotada para
  el futuro: un ticket que enseñe al analyzer de frontend a detectar evidencia REAL de auth (un
  `apiCall` a `/login`/`/auth`, o una librería tipo `@auth0/auth0-react`); solo entonces el edge
  `Frontend→Security` sería legítimo, con ese archivo como Source.
- Verificado autónomamente: backend **140 tests** verdes; frontend `tsc --noEmit`/`build`/`lint`
  limpios (solo warnings preexistentes).

**FASE 6 (ancho + profundidad): T31-T35 done.** Ancho = analyzer de Security/JWT (T31-T32).
Profundidad = jerarquía de nodos con expand/collapse (T33-T35). Front→security con evidencia
real queda como posible ticket futuro. Siguen los analyzers de ancho (T36 Message Bus abajo;
Workers/Cloud como follow-up del mismo patrón).

### T36 — Analyzer: detección de Message Bus (`MessageBusAnalyzer`)
- Depende de: T4, T10 (pipeline), extiende el patrón de T30/T32 en `SystemGraphBuilder`
- Descripción: `MessageBusAnalyzer` nuevo (`ILanguageAnalyzer`, `Language="messagebus"`) detecta
  un broker de mensajería por dependencia REAL en el manifiesto (mismo criterio que
  `AuthAnalyzer`, nunca por convención): npm `amqplib`/`amqp-connection-manager`→RabbitMQ,
  `kafkajs`→Kafka, `nats`→NATS; .NET `RabbitMQ.Client`→RabbitMQ, `Confluent.Kafka`→Kafka,
  `MassTransit`→"Message Bus" (genérico, agnóstico al broker). Produce un
  `Node(Category=Infrastructure, Type="MessageBus", Name=broker)` — NO necesita categoría nueva:
  `NodeCategory.Infrastructure` ya lista "Queue". `SystemGraphBuilder` gana un bloque nuevo
  (mismo shape que Docker/Security) que agrega el nodo + edge `Backend→MessageBus` ("messaging",
  Confidence 90) SOLO si hay Backend.
- DECISIONES (con Carlos): Name = **broker específico** (fallback "Message Bus" para libs
  agnósticas); edge label = **"messaging"** (neutral: una dependencia prueba que el backend USA
  el bus, no si publica o consume — no afirmar más que la evidencia); Confidence 90 (evidencia
  de manifiesto, como Docker/Security). Color propio distinto del amarillo de Docker.
- Cómo se construye/verifica SIN un repo real (pregunta de Carlos): igual que todos los
  analyzers previos — con **fixtures en memoria** en los tests (un `package.json`/`.csproj` de
  mentira con la dependencia). Verificación en vivo (opcional, después) requiere un repo propio
  con la dep (el selector solo lista repos `affiliation=owner`, T25).
- Criterio de aceptación: fixture con `amqplib` → `Node(Infrastructure, MessageBus, "RabbitMQ")`
  + `Edge Backend→MessageBus "messaging"` con Source = ruta del manifiesto; fixture con
  `Confluent.Kafka` en `.csproj` → broker "Kafka"; sin ninguna dep de bus → nodo NO aparece
  (nunca inventar); test en `SystemGraphBuilderTests` (bus sin Backend → nodo suelto sin edge).
- Estado: done (verificado por fixtures; verificación en vivo pendiente hasta que Carlos tenga
  un repo propio con un broker — agregar `amqplib`/`kafkajs` a un `package.json` y re-analizar).
- `MessageBusAnalyzer` (gemelo de `AuthAnalyzer`): recorre manifiestos, mapea la dep a su broker
  vía listas ORDENADAS de tuplas (npm: amqplib/amqp-connection-manager→RabbitMQ, kafkajs→Kafka,
  nats→NATS; .NET: RabbitMQ.Client→RabbitMQ, Confluent.Kafka→Kafka, MassTransit→"Message Bus" al
  final por ser agnóstico). Primer manifiesto con broker gana. `Node(Infrastructure, "MessageBus",
  Name=broker)`. Try/catch en el parse JSON (varios manifiestos, uno roto no tumba el análisis).
- `SystemGraphBuilder`: bloque nuevo calcado de Docker/Security → nodo + `Edge Backend→MessageBus`
  "messaging" (confidence 90) solo si hay Backend. Constante `BackendToMessageBusConfidence=90`.
- DI: `MessageBusAnalyzer` registrado. Frontend: token `severe` (#db6d28, naranja real de Primer)
  en `githubDark.ts` + `TYPE_COLORS["MessageBus"]` en `nodeVisuals.ts` (distinto del amarillo de
  Docker) + `TECH_LABELS["MessageBus"]="Message Bus"` en `NodeDetailPanel`. Sin categoría nueva:
  `NodeCategory.Infrastructure` ya cubre "Queue", así que agrupa bajo Infrastructure en el árbol.
- Tests: `MessageBusAnalyzerTests` (9: CanAnalyze; amqplib→RabbitMQ, kafkajs→Kafka, Confluent.
  Kafka-csproj→Kafka, MassTransit→genérico, sin dep→vacío, monorepo Source correcto),
  `SystemGraphBuilderTests` (+1: bus sin Backend → nodo suelto), `NodeExpressPipelineTests`
  extendido (fixture +amqplib +`MessageBusAnalyzer` en la lista → 8 nodos + 5 edges end-to-end,
  verifica `Backend→RabbitMQ "messaging"`). GOTCHA registrado: el pipeline test arma su PROPIA
  lista de analyzers, así que hubo que agregar `new MessageBusAnalyzer()` ahí (no basta con la
  fixture) — el primer run falló (7 vs 8 esperado) justo por eso. Total backend: **149 tests**
  (71 domain + 28 api + 50 infra), verdes. Frontend `tsc`/build/lint limpios.

### T37 — Analyzer: detección de Workers / background jobs (`WorkersAnalyzer`)
- Depende de: T4, T10, extiende el patrón de T36 en `SystemGraphBuilder`
- Descripción: `WorkersAnalyzer` (`Language="workers"`) detecta procesamiento en background por
  dependencia de una librería DEDICADA de jobs (nunca por convención — Carlos marcó Workers como
  el más ambiguo, alto riesgo de falso positivo): npm `bullmq`/`bull`/`bee-queue`/`agenda`; .NET
  `Hangfire`/`Quartz`/`Coravel`. Se excluyen a propósito los cron triviales (node-cron). Produce
  `Node(Category=Worker, Name="Workers", Type="Workers")` con la lib concreta en
  `metadata["framework"]`. `SystemGraphBuilder`: nodo + `Edge Backend→Workers "background jobs"`
  (confidence 90) solo si hay Backend.
- DECISIONES (con Carlos): categoría = **nuevo `NodeCategory.Worker`** (rol arquitectónico
  distinto, Archify lo separa; sin migración, Category es varchar — igual que Security en T31);
  nombre = **genérico "Workers"** (el insight es "hay background processing", no cuál lib; la lib
  va en metadata); edge label = **"background jobs"** (neutral). Color = teal `#39c5cf` (token
  `teal` nuevo, distinto de los 6 en uso).
- Criterio de aceptación: fixture con `bullmq` → `Node(Worker, "Workers", framework=BullMQ)` +
  `Edge Backend→Workers`; `Hangfire` en `.csproj` → framework=Hangfire; `node-cron` (cron
  trivial) → NADA (no inventar); test en `SystemGraphBuilderTests` (workers sin Backend → suelto).
- Estado: done (verificado por fixtures; en vivo requiere repo propio con la dep).
- `WorkersAnalyzer` gemelo de `MessageBusAnalyzer` pero el Node siempre se llama "Workers" y la
  lib va en `metadata["framework"]` (listas ordenadas de tuplas package→framework). Bloque en
  `SystemGraphBuilder` calcado. DI registrado. Frontend: token `teal` (#39c5cf) + `TYPE_COLORS
  ["Workers"]` + `CATEGORY_ORDER` con `'Worker'` (entre Infrastructure y DevOps) + `META_LABELS
  ["framework"]="Framework"` en `NodeDetailPanel`. Tests: `WorkersAnalyzerTests` (6),
  `SystemGraphBuilderTests` (+1), `NodeExpressPipelineTests` extendido (fixture +bullmq +
  `WorkersAnalyzer` en la lista → 9 nodos + 6 edges; se recordó agregar el analyzer a la lista,
  el gotcha de T36). Total backend: **156 tests** (71 domain + 28 api + 57 infra), verdes.
  Frontend `tsc`/build/lint limpios.

### T38 — Analyzer: detección de Cloud provider (`CloudAnalyzer`)
- Depende de: T4, T10, extiende el patrón de T36/T37 en `SystemGraphBuilder`. Último del patrón.
- Descripción: `CloudAnalyzer` (`Language="cloud"`) detecta que la app USA un cloud por el SDK del
  proveedor en el manifiesto, y nombra el nodo por el proveedor (AWS/Azure/GCP). DIFERENCIA con
  los otros analyzers npm: los SDK de cloud son paquetes con PREFIJO (`@aws-sdk/client-s3`,
  `@google-cloud/storage`, `@azure/...`), así que la detección npm recorre las claves de deps y
  matchea por prefijo (`aws-sdk` exacto o `@aws-sdk/` prefijo → AWS; `@google-cloud/` → GCP;
  `@azure/` → Azure), con prioridad fija (AWS, GCP, Azure) para ser determinista. .NET: substring
  de prefijo NuGet (`AWSSDK.`→AWS, `Google.Cloud.`→GCP, `Azure.`→Azure, este último al final por
  ser el más amplio). `Node(Category=Deployment, Name=proveedor, Type="Cloud")`. `SystemGraphBuilder`:
  nodo + `Edge Backend→Cloud "cloud services"` (confidence 90) solo si hay Backend.
- DECISIONES (con Carlos): evidencia = **SDK del proveedor** (manifiesto), no Terraform/IaC (que
  detecta "provisiona infra" en vez de "usa cloud"; queda como posible extensión futura). Categoría
  = **reusar `NodeCategory.Deployment`** (el enum ya lista AWS/Azure/GCP ahí — sin enum nuevo, como
  Message Bus reusó Infrastructure). Color = sky-blue `#79c0ff` (token `sky`, tema "nube"; más
  claro que el accent del Frontend). Edge label = **"cloud services"** (neutral: el SDK prueba uso).
- Criterio de aceptación: fixture con `@aws-sdk/client-s3` → `Node(Deployment, "AWS", Cloud)` +
  `Edge Backend→Cloud`; `aws-sdk` v2 (nombre exacto) también → AWS; `@google-cloud/storage` → GCP;
  `Azure.Storage.Blobs` en `.csproj` → Azure; sin SDK → NADA (no inventar); test en
  `SystemGraphBuilderTests` (cloud sin Backend → suelto).
- Estado: done (verificado por fixtures; en vivo requiere repo propio con la dep).
- Tests: `CloudAnalyzerTests` (7: incluye el caso del prefijo `@aws-sdk/` y el exacto `aws-sdk`),
  `SystemGraphBuilderTests` (+1), `NodeExpressPipelineTests` extendido (+`@aws-sdk/client-s3` +
  `CloudAnalyzer` en la lista → 10 nodos + 7 edges). DI + frontend (token `sky` + `TYPE_COLORS
  ["Cloud"]`; agrupa bajo "Deployment" en el árbol, ya en `CATEGORY_ORDER`). Total backend: **164
  tests** (71 domain + 28 api + 65 infra), verdes. Frontend `tsc`/build/lint limpios.

**FASE 6 COMPLETA (ancho + profundidad). Ancho = 4 categorías nuevas de detección:** Security/JWT
(T32, rosa), Message Bus/RabbitMQ-Kafka (T36, naranja), Workers/background-jobs (T37, teal), Cloud/
AWS-Azure-GCP (T38, sky). **Profundidad** = jerarquía de nodos con expand/collapse (T33-T35).
Todas por dependencia de manifiesto real, confidence 90, nunca inventadas. Posibles follow-ups
futuros del mismo patrón: Terraform/IaC para cloud, front→security con evidencia real, más brokers/
frameworks/providers, y sub-detección fina (ej. qué recursos cloud concretos, como hace Archify).

- DECISIÓN D1 (T23, nota histórica mal ubicada en este archivo): el timeline lee de `GET /projects/{id}/traces` (tiene startedAt→offsets reales); el traceId del último evento LIVE actúa de refreshKey → refetch al llegar un trace en vivo. Sin cambios en backend. Archivos (todo frontend): `live/traceTypes.ts` (TraceDto/SpanDto), `services/traceService.ts`, `hooks/useProjectTraces.ts` (fetch+refetch), `components/TraceTimeline.tsx` (waterfall: left%=offset, width%=duración; raíz morada, hijo teal; ms fuera de la barra), `GraphPage.tsx` (monta el panel dockeado abajo). VERIFICADO end-to-end: waterfall proporcional (raíz backend ancho completo, hijo DB tramo desplazado) + total + refresco en vivo sin recargar. Bug de layout corregido en verificación: la etiqueta de ms iba dentro de la barra y desbordaba en spans cortos → se sacó fuera + `overflow:hidden` en el track + width clamp a (100-left%).

**FASE 4 (LIVE mode) COMPLETA: T18-T23 done.** Modo LIVE end-to-end funcionando y verificado.
