# ARCHITECTURE.md — Decisiones Técnicas

> **Qué es este archivo:** responde el *cómo*. A diferencia de `PRODUCT.md`, aquí sí importan las tecnologías. Y a diferencia de `RULES.md`, aquí registras **decisiones** (pueden cambiar con justificación), no restricciones absolutas.

## Stack

- **Backend:** ASP.NET Core Web API (.NET) + Entity Framework Core
- **Frontend:** React + TypeScript + Vite + React Flow (canvas del grafo)
- **Base de datos:** PostgreSQL (modelo de grafo implementado sobre tablas relacionales — no Neo4j en el MVP)
- **Autenticación:** GitHub OAuth (único mecanismo de login; sirve también para autorizar lectura de repos)
- **Realtime:** SignalR (sobre WebSockets, con fallback automático) para el modo LIVE
- **Telemetría:** OpenTelemetry .NET SDK, instrumentando el proyecto demo (y, más adelante, cualquier backend analizado que lo adopte)
- **Análisis de código:** Roslyn (C#) para repos .NET; subproceso Node.js con TypeScript Compiler API para repos React/TS
- **Infraestructura:** Docker (contenedores locales + compose), GitHub Actions (CI), Vercel (frontend) + Railway (backend + Postgres)

## Arquitectura general

Clean Architecture en el backend, con capas **Domain / Application / Infrastructure / Api**. El frontend es una SPA independiente que consume la API vía REST + SignalR.

El análisis de código se trata como un **plugin system**: cada lenguaje/stack tiene un analyzer que implementa una interfaz común (`ILanguageAnalyzer`) y vive en Infrastructure. Esto es intencional — el Contexto maestro (secciones 18-19) pide una arquitectura basada en adapters/connectors para no llenar el core de lógica específica de cada proveedor o lenguaje. El mismo patrón de adapter se reutiliza para proveedores de infraestructura (GitHub hoy; Vercel/Railway/AWS como adapters futuros de `IProviderConnector`).

## Diagrama de dependencias entre capas

```
Api → Application → Domain
Infrastructure → Application (implementa sus abstracciones: IRepository, ILanguageAnalyzer, IProviderConnector)
Domain no depende de nada

Frontend (React) → Api (REST + SignalR), nunca accede a Infrastructure/DB directamente
Analyzers.Node (subproceso TS) → invocado por Infrastructure vía Process.Start, se comunica por stdin/stdout JSON
```

## Decisiones registradas (estilo ADR)

### Decisión: Modelo de grafo sobre PostgreSQL, no Neo4j
- **Contexto:** El sistema necesita persistir nodos y edges de un grafo de arquitectura, y hacer queries de tipo "dependencias de X", "quién usa Y".
- **Alternativas consideradas:** Neo4j (base de datos de grafos nativa) — mejor para queries de grafo muy profundas, pero introduce una segunda tecnología de persistencia, más operación (otro contenedor, otro backup, otra curva de aprendizaje) sin que el MVP la necesite todavía.
- **Decisión:** Tablas `Nodes` y `Edges` en PostgreSQL, con `EdgeId → SourceNodeId/TargetNodeId` y JSONB para `metadata`. Las queries de dependencias en el MVP son de 1-2 saltos, PostgreSQL las resuelve bien con índices normales.
- **Consecuencias:** Si en el futuro el grafo crece a queries de muchos saltos (ej. "todos los servicios afectados indirectamente por X" a 5+ niveles) o a 10,000+ nodos con navegación intensiva, se reevalúa Neo4j. La migración es aislada porque el acceso a datos vive detrás de `IGraphRepository` (Application), no se filtra a Api ni Domain.

### Decisión: SignalR para el modo LIVE, no WebSockets crudos ni SSE
- **Contexto:** El frontend necesita recibir eventos runtime (`TRACE_STARTED`, `REQUEST_COMPLETED`, etc.) en tiempo real mientras el usuario tiene el mapa abierto.
- **Alternativas consideradas:** WebSockets crudos — control total pero hay que reimplementar reconexión, heartbeats y fallback a mano. Server-Sent Events — más simple, pero es unidireccional (el cliente no puede pedir "suscribirme solo a este proyecto" por el mismo canal) y no es idiomático en ASP.NET Core.
- **Decisión:** SignalR, con un Hub por proyecto (`ArchitectureMapHub`) al que el cliente se suscribe tras seleccionar un proyecto. Reconexión automática y negociación de transporte (WebSocket → long polling) vienen resueltas por el framework.
- **Consecuencias:** Acopla el backend a .NET para el canal realtime (aceptable, ya estamos en ASP.NET Core). El evento normalizado (sección 21 del contexto) se serializa igual sin importar el transporte, así que cambiar de mecanismo más adelante no afecta al modelo de eventos.

### Decisión: Análisis de código híbrido — Roslyn nativo + subproceso Node.js para TS
- **Contexto:** El MVP necesita detectar componentes en dos stacks distintos: backend ASP.NET Core/C# y frontend React/TypeScript. El backend del producto está en C#.
- **Alternativas consideradas:** (1) Reimplementar un parser de TS/JSX en C# — evita el subproceso, pero un parser propio nunca va a estar tan actualizado ni ser tan preciso como el compilador real de TypeScript, y JSX tiene reglas de parsing no triviales. (2) Mover todo el análisis a un servicio Node.js separado — consistente en un solo lenguaje para analyzers, pero divide el backend en dos runtimes desplegables y complica la orquestación para el MVP.
- **Decisión:** Analyzers detrás de `ILanguageAnalyzer` en Infrastructure. El analyzer de C# usa Roslyn (`Microsoft.CodeAnalysis`) directamente, in-process. El analyzer de React/TS invoca un subproceso Node.js standalone (`analyzers-node/`) que usa el TypeScript Compiler API real, y devuelve JSON por stdout que Infrastructure deserializa.
- **Consecuencias:** El subproceso Node.js es una dependencia de despliegue adicional (debe estar disponible en el contenedor del backend). A cambio, la detección de imports/JSX es tan precisa como el propio compilador de TS. Si en el futuro se vuelve un cuello de botella operativo, se puede extraer a un microservicio HTTP propio sin tocar `ILanguageAnalyzer`.

### Decisión: Ingesta de OpenTelemetry propia, sin Jaeger/Tempo/Collector en el MVP
- **Contexto:** El contexto maestro pide OpenTelemetry como base para tracing (sección 10), pero no exige un backend de observabilidad completo desde el día uno.
- **Alternativas consideradas:** Desplegar un OpenTelemetry Collector + Jaeger/Tempo como backend de trazas — es el estándar de la industria, pero añade 2-3 servicios más de infraestructura para un MVP que solo necesita mostrar traces básicos en el mapa.
- **Decisión:** El proyecto demo exporta spans vía OTLP/HTTP directamente a un endpoint propio (`POST /api/v1/telemetry/traces`) en la Api, que los normaliza al modelo de evento (sección 21) y los persiste en PostgreSQL (tabla `Traces`/`Spans`).
- **Consecuencias:** No es un backend OTel completo (sin sampling avanzado, sin correlación con logs todavía). Es intencionalmente el mínimo necesario para demostrar el modo LIVE. El exporter usa el protocolo estándar OTLP, así que un backend real (Tempo/Jaeger) se puede añadir después como otro receiver sin tocar la instrumentación del lado del proyecto analizado.

### Decisión: GitHub OAuth como único mecanismo de autenticación
- **Contexto:** La app necesita autenticar al usuario y, por separado, autorizar lectura de sus repositorios.
- **Alternativas consideradas:** Auth propio (JWT + email/password) con conexión a GitHub como paso adicional — desacopla identidad de la fuente del código, útil si algún día se soportan otros proveedores de repos, pero duplica trabajo (registro, recuperación de contraseña, verificación de email) que no aporta valor al MVP.
- **Decisión:** Un solo flujo "Sign in with GitHub". El `access_token` de GitHub se usa tanto para identificar al usuario como para leer el repositorio conectado, con los scopes mínimos necesarios (`repo:read` o `public_repo` según el caso).
- **Consecuencias:** Si más adelante se soporta GitLab/Bitbucket, hay que introducir una tabla de identidad propia separada del proveedor (`Users` desacoplado de `GitHubIdentity`) — se deja el modelo de datos preparado para eso desde ahora (ver "Integraciones externas").

## Integraciones externas

- **GitHub API (OAuth + REST/GraphQL)** — Autenticación del usuario y lectura de contenido del repositorio conectado (árbol de archivos, contenido de archivos puntuales para análisis). Se abstrae detrás de `IProviderConnector` (Infrastructure), implementado por `GitHubAdapter`. Application y Domain nunca llaman al SDK de GitHub directamente.
- **OpenTelemetry SDK / OTLP** — El proyecto demo (y cualquier repo instrumentado por el usuario) exporta traces vía OTLP hacia el endpoint de ingesta propio. Del lado de la app, la ingesta se abstrae detrás de `ITelemetryIngestor` (Application/Infrastructure), para poder cambiar el backend de almacenamiento de trazas sin tocar el resto del sistema.
- **Token storage** — El `access_token` de GitHub se cifra en reposo usando ASP.NET Core Data Protection API antes de persistirse; nunca se loguea ni se expone en respuestas de la API (ver `RULES.md`).
