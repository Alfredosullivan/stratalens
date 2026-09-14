# PRODUCT.md — Contexto de Negocio

> **Qué es este archivo:** responde el *qué* y el *por qué* del sistema. Si puedes cambiar PostgreSQL por SQL Server, o React por Vue, sin tocar una sola palabra de este documento, está bien escrito. Ninguna decisión técnica debería vivir aquí — eso va en `ARCHITECTURE.md`.

## Problema

Para entender cómo funciona realmente una aplicación full-stack, un desarrollador tiene que revisar manualmente muchas herramientas distintas (código, GitHub, CI/CD, infraestructura, logs, métricas) y armar el mapa mental él mismo. No existe un lugar único que muestre, de forma visual, cómo están conectadas las piezas de un sistema — ni en diseño (qué existe) ni en tiempo real (qué está pasando ahora mismo).

## Usuarios / Roles

- **Developer (único rol en el MVP)** — Se autentica con GitHub, conecta uno de sus repositorios, dispara el análisis, y explora el mapa de arquitectura resultante (estático y, si el proyecto lo tiene instrumentado, en modo LIVE).

> Roles de equipo/organización (admin, viewer, member) quedan fuera del MVP — ver "Fuera de alcance".

## Funcionalidades

Alcance MVP (Contexto maestro, sección 36), en orden de flujo de usuario:

- [x] Login con GitHub OAuth (esto es a la vez autenticación de la app y autorización para leer repos).
- [x] Crear un proyecto dentro de la app.
- [ ] Conectar un repositorio de GitHub al proyecto **eligiéndolo de una lista** (Fase 5): el análisis ya sabe procesar cualquier repo dado (`AnalyzeRepositoryUseCase`), pero falta el flujo "ver mis repos → elegir uno" (no hay endpoint para listar los repos del usuario ni UI para elegir).
- [x] Analizar el repositorio y detectar automáticamente: frontend, backend, base de datos.
- [x] Detectar dependencias básicas entre esos componentes (ej. frontend llama a un endpoint del backend; backend usa tal base de datos).
- [x] Construir un grafo de arquitectura (nodos + relaciones) a partir de lo detectado.
- [x] Mostrar el grafo en un canvas interactivo (zoom, pan, selección).
- [x] Al seleccionar un nodo, mostrar su metadata (tipo, tecnología, de qué archivo se detectó).
- [ ] Expandir/colapsar nodos para pasar de vista general a vista detallada (jerarquía mínima: System → Application) — no construido; el MVP solo muestra el nivel 2 (Application) fijo.
- [x] Instrumentar el proyecto demo con OpenTelemetry y recibir traces básicos.
- [x] Modo LIVE: cuando llega una request real con trace, animar/resaltar el flujo en el mapa (frontend → backend → base de datos) y mostrar duración total.

## Reglas de negocio

- Toda relación (edge) detectada automáticamente debe tener un **origen** (`source`): el archivo o configuración de la que se dedujo. Nunca se muestra una conexión sin poder explicar de dónde salió (Contexto maestro, secciones 43 y 45).
- Toda relación que no provenga de una observación runtime confirmada (trace real) debe llevar un **nivel de confianza** y presentarse como inferencia, nunca como hecho absoluto (sección 44).
- Debe distinguirse siempre entre **Static Discovery** (lo que el código/config sugiere que existe) y **Runtime Observability** (lo que un trace real confirmó que ocurrió) — nunca se mezclan sin etiquetar cuál es cuál (sección 42).
- Un proyecto pertenece a un único usuario propietario (no hay equipos en el MVP); un usuario solo puede ver y analizar sus propios proyectos.
- La app nunca almacena el código fuente del repositorio analizado, solo el resultado del análisis (nodos, relaciones, metadata) — ver `RULES.md` para el detalle técnico de cómo se verifica.

## Fuera de alcance

Explícitamente fuera de esta fase (Contexto maestro, sección 36, "el MVP no necesita soportar"):

- Proveedores de repositorio distintos a GitHub (GitLab, Bitbucket, ZIP, directorio local).
- Múltiples lenguajes/stacks más allá de React/TS (frontend) y ASP.NET Core/C# (backend) — Node.js backend queda como adapter futuro, no en el MVP.
- Bases de datos distintas a PostgreSQL como target de análisis.
- DevSecOps completo (SAST, secret scanning, dependency scanning integrados al mapa) — la detección de Docker de la Fase 5 es presencia de infraestructura (¿hay un Dockerfile?), no escaneo de seguridad; sigue sin haber SAST/secret-scanning/dependency-scanning.
- Architecture Health scoring.
- Time Travel / Replay de eventos pasados.
- Comparación entre environments (Production vs Staging).
- Logs integrados (el MVP solo trae traces básicos de OpenTelemetry, no logs).
- Export a PNG/SVG/Mermaid/documentación autogenerada.
- Equipos, roles, permisos granulares, multi-usuario por proyecto.
- Búsqueda global y filtros avanzados (se evalúan después de validar el grafo base).

**No en esta fase (Fase 5), sí en la visión de producto** — a diferencia de la lista de arriba, esto no está descartado, solo no se construye todavía: Kubernetes y proveedores cloud (AWS/Azure/GCP) como target de detección. La Fase 5 empieza por Docker (la señal de infraestructura más simple, mismo patrón de analyzer ya existente); K8s/cloud son analyzers futuros del mismo tipo, a añadir cuando Docker esté probado.

## Glosario

| Término | Significado |
|---|---|
| **Node** | Un componente del sistema representado en el grafo (frontend, backend, base de datos, servicio externo, etc.). |
| **Edge** | Una relación dirigida entre dos nodos (ej. "Frontend usa HTTP/REST hacia Backend"). |
| **Static Discovery** | Relación o nodo detectado analizando código/configuración, sin necesidad de tráfico real. |
| **Runtime Observability** | Relación o evento confirmado por telemetría real (trace de OpenTelemetry). |
| **Source** | Referencia al archivo/configuración/trace de la que se dedujo un nodo o edge. Obligatorio en todo elemento detectado. |
| **Confidence** | Porcentaje que indica qué tan seguro está el sistema de que una relación inferida (no observada en runtime) es correcta. |
| **Trace** | Secuencia de spans que representa el recorrido completo de una request a través del sistema, identificada por un `traceId`. |
| **Span** | Un tramo individual dentro de un trace (ej. "Backend procesando la request", con su duración). |
| **LIVE mode** | Modo de visualización donde el mapa refleja actividad runtime real en vez de solo la arquitectura estática. |
| **Level** | Nivel de zoom/detalle del grafo (1-System, 2-Application, 3-Code, 4-Runtime). En el MVP solo se soportan los niveles 1 y 2. |
| **Project** | Unidad de trabajo en la app: agrupa un repositorio conectado, su grafo de arquitectura y su telemetría. |
