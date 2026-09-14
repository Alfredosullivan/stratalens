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

**TODO EL ROADMAP CONOCIDO (T1-T30) TERMINADO Y VERIFICADO EN VIVO.** No quedan tickets pendientes en `SDD/TASKS.md`.

- DECISIÓN D1 (T23, nota histórica mal ubicada en este archivo): el timeline lee de `GET /projects/{id}/traces` (tiene startedAt→offsets reales); el traceId del último evento LIVE actúa de refreshKey → refetch al llegar un trace en vivo. Sin cambios en backend. Archivos (todo frontend): `live/traceTypes.ts` (TraceDto/SpanDto), `services/traceService.ts`, `hooks/useProjectTraces.ts` (fetch+refetch), `components/TraceTimeline.tsx` (waterfall: left%=offset, width%=duración; raíz morada, hijo teal; ms fuera de la barra), `GraphPage.tsx` (monta el panel dockeado abajo). VERIFICADO end-to-end: waterfall proporcional (raíz backend ancho completo, hijo DB tramo desplazado) + total + refresco en vivo sin recargar. Bug de layout corregido en verificación: la etiqueta de ms iba dentro de la barra y desbordaba en spans cortos → se sacó fuera + `overflow:hidden` en el track + width clamp a (100-left%).

**FASE 4 (LIVE mode) COMPLETA: T18-T23 done.** Modo LIVE end-to-end funcionando y verificado.
