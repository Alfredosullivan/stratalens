# RULES.md — Reglas y Restricciones

> **Qué es este archivo:** restricciones que un código debe cumplir aunque compile y funcione. Si `ARCHITECTURE.md` dice "cómo construimos", este archivo dice "qué nunca debe pasar". Un agente de IA puede generar algo que compila perfecto y aun así viola una regla de aquí — por eso cada regla, cuando sea posible, indica cómo se verifica. Una regla escrita pero no verificada es una esperanza, no una barrera.

## Reglas de arquitectura / capas

| Regla | Cómo se verifica |
|---|---|
| Api (Controllers/Hubs) nunca accede directo a `DbContext` ni a repositorios concretos, solo a interfaces de Application | code review / architecture test con dependency-cruiser o similar |
| Domain no depende de Application ni de Infrastructure | análisis de referencias de proyecto (`.csproj`) — Domain no referencia ningún otro proyecto |
| Application no depende de Infrastructure, solo define interfaces (`IGraphRepository`, `ILanguageAnalyzer`, `IProviderConnector`, `ITelemetryIngestor`) | análisis de referencias de proyecto |
| Cada analyzer de lenguaje implementa `ILanguageAnalyzer` y vive en Infrastructure — nunca lógica de detección de stack dentro de Application o Api | code review |
| Cada proveedor de infraestructura (GitHub, y futuros Vercel/Railway/AWS) implementa `IProviderConnector` — prohibido código específico de un proveedor fuera de su adapter | code review |
| El frontend (React) solo habla con el backend vía REST/SignalR — nunca accede a PostgreSQL ni a GitHub API directamente | code review |

## Convenciones de nombres y estilo

| Regla | Cómo se verifica |
|---|---|
| PascalCase para clases/métodos/propiedades en C#, camelCase para funciones/variables en TypeScript | `dotnet format` / analyzer C#, ESLint en frontend |
| Comentarios de código siempre en español (regla global de CLAUDE.md) | code review |
| Nombres de entidades del dominio (`Node`, `Edge`, `Trace`, `Span`, `Project`) se usan tal cual en código — no sinónimos por módulo (ej. nunca `Vertex` en vez de `Node`) | code review contra el Glosario de `PRODUCT.md` |

## Reglas de seguridad

| Regla | Cómo se verifica |
|---|---|
| El `access_token` de GitHub se cifra en reposo (Data Protection API) antes de persistirse | code review + test de integración que verifica que la columna en DB no contiene el token en texto plano |
| El `access_token` nunca aparece en logs, respuestas de API ni mensajes de error | code review + grep de logs en CI |
| Los scopes de OAuth solicitados a GitHub son los mínimos necesarios para leer repos (nunca `admin:org`, `delete_repo`, etc.) | code review de la configuración de OAuth |
| Todo endpoint de la Api (excepto login/callback de OAuth) requiere autenticación válida | tests de integración por endpoint |
| Un usuario solo puede leer/modificar proyectos de los que es dueño (ownership check) | tests de integración (intento de acceso cruzado entre usuarios debe devolver 403/404) |
| Nunca se persiste el código fuente del repositorio analizado — solo el resultado del análisis (nodos, edges, metadata mínima como nombre de archivo) | code review del pipeline de analyzers, ningún analyzer escribe contenido crudo de archivo en DB |
| Toda entrada de usuario (nombre de proyecto, URL de repo, etc.) se valida con FluentValidation antes de tocar la base de datos | code review + tests |

## Reglas de documentación de API

| Regla | Cómo se verifica |
|---|---|
| Todo endpoint nuevo de la Api se documenta en Swagger/OpenAPI | code review |
| Todo evento de SignalR nuevo se documenta (nombre, payload, cuándo se emite) en el mismo lugar que la API REST | code review |

## Reglas del modelo de grafo (específicas de este producto)

| Regla | Cómo se verifica |
|---|---|
| Todo `Edge` creado por un analyzer estático debe tener `Source` (archivo/config de origen) no vacío | validación a nivel de dominio (invariante de la entidad `Edge`) + unit test |
| Todo `Edge` que no provenga de un trace runtime confirmado debe tener `Confidence < 100` | invariante de dominio + unit test |
| Un `Edge` con `Confidence = 100` solo puede originarse desde un trace de OpenTelemetry observado (`SourceType = Runtime`) | invariante de dominio + unit test |

## Reglas de testing

| Regla | Cómo se verifica |
|---|---|
| Cada analyzer de lenguaje se prueba contra al menos un fixture real (proyecto React de ejemplo, proyecto .NET de ejemplo) | tests unitarios del analyzer, cobertura mínima en CI |
| Toda invariante de dominio (`Node`, `Edge`, `Trace`) tiene al menos un unit test | cobertura mínima en CI |
| Todo endpoint crítico (auth, análisis de repo, obtención de grafo) tiene al menos un test de integración | CI |

## Prohibido siempre

- Lógica de negocio (decidir qué es un nodo, cómo se calcula confidence, etc.) dentro de Controllers o Hubs.
- Duplicar un analyzer, adapter o servicio que ya existe en vez de extenderlo.
- Acceder a entidades de EF Core directamente como DTOs de respuesta de la Api (siempre mapear a un DTO explícito).
- Mostrar una relación en el mapa como si fuera un hecho confirmado cuando en realidad es una inferencia estática sin confirmar por runtime.
- Almacenar o loguear tokens, credenciales o secretos del usuario en texto plano.
- Analizar o almacenar código de un repositorio sin que el usuario lo haya conectado explícitamente.
