# Project Context: Visual Architecture & Runtime Observability Platform

## 1. Product Vision

Quiero construir una aplicación web que permita visualizar, analizar y monitorear una aplicación de software completa mediante un mapa interactivo.

La idea central es crear una especie de "mapa vivo" de una aplicación, donde cada componente importante del sistema aparezca como un nodo y las relaciones entre ellos aparezcan como conexiones.

La herramienta debe poder representar tanto:

1. La arquitectura estática del proyecto.
2. Las dependencias entre componentes del código.
3. La infraestructura y servicios externos.
4. El pipeline de DevOps/DevSecOps.
5. El flujo real de información entre componentes.
6. Métricas y eventos en tiempo real.
7. Problemas, errores, latencias y posibles puntos críticos.

No quiero que sea únicamente un diagrama visual.

El objetivo a largo plazo es crear una herramienta de "Architecture Intelligence + Runtime Observability", donde un desarrollador pueda abrir un proyecto y entender visualmente cómo funciona todo el sistema.

---

# 2. Problema que quiero resolver

Actualmente, para entender una aplicación full-stack relativamente grande, un desarrollador normalmente tiene que revisar múltiples lugares:

- VS Code
- GitHub
- package.json
- variables de entorno
- código frontend
- código backend
- APIs
- base de datos
- Docker
- GitHub Actions
- Vercel
- Railway
- AWS/Azure/GCP
- logs
- métricas
- documentación
- OpenAPI/Swagger
- etc.

Quiero centralizar esa información.

La herramienta debería permitir responder visualmente preguntas como:

- ¿Qué frontend tengo?
- ¿Dónde está desplegado?
- ¿Qué backend utiliza?
- ¿Qué endpoints existen?
- ¿Qué servicios llaman a esos endpoints?
- ¿Qué base de datos utiliza el backend?
- ¿Qué tablas utiliza cada servicio?
- ¿Qué servicios externos existen?
- ¿Cómo se despliega el proyecto?
- ¿Qué ocurre cuando hago un push a GitHub?
- ¿Qué ocurre durante el CI/CD?
- ¿Dónde está alojada cada parte?
- ¿Qué componentes dependen de otros?
- ¿Qué está fallando?
- ¿Qué componente está causando una latencia?
- ¿Qué requests están pasando actualmente por el sistema?
- ¿Cuál es el flujo real de una petición?

---

# 3. Concepto principal: Application Map

La aplicación debe tener como elemento principal un canvas interactivo que represente la arquitectura como un grafo.

Ejemplo conceptual:

GitHub
    |
    v
GitHub Actions
    |
    v
Vercel
    |
    v
React Frontend
    |
    | HTTPS / REST
    v
ASP.NET Core API
    |
    +------------------+
    |                  |
    v                  v
PostgreSQL          External API
    |
    v
Database

Pero la arquitectura debe ser completamente dinámica.

No se debe asumir que todas las aplicaciones utilizan:

- React
- .NET
- PostgreSQL
- Vercel
- Railway
- GitHub

Cada proyecto puede utilizar tecnologías diferentes.

Ejemplos:

React -> Node.js -> PostgreSQL

Angular -> .NET -> SQL Server

Vue -> Java -> MySQL

Next.js -> Node.js -> MongoDB

Mobile App -> API Gateway -> Microservices -> Redis -> PostgreSQL

Frontend -> AWS -> Lambda -> DynamoDB

Kubernetes -> múltiples microservicios -> múltiples databases

La herramienta debe ser agnóstica respecto al stack.

---

# 4. Tipos de nodos

El sistema debe manejar diferentes categorías de nodos.

## Application

- Frontend
- Backend
- API
- Microservice
- Monolith
- Mobile Application
- Desktop Application

## Code

- Component
- Module
- Controller
- Service
- Repository
- Function
- Class
- Package
- Library

## Infrastructure

- Server
- Container
- Docker
- Kubernetes cluster
- Pod
- Load Balancer
- API Gateway
- CDN
- Queue
- Cache

## Databases

- PostgreSQL
- MySQL
- SQL Server
- MongoDB
- Redis
- DynamoDB
- etc.

## External services

- Stripe
- Auth0
- Firebase
- SendGrid
- Google APIs
- AWS services
- Azure services
- etc.

## DevOps

- GitHub
- GitLab
- Bitbucket
- GitHub Actions
- Jenkins
- Azure DevOps
- CircleCI
- etc.

## Deployment platforms

- Vercel
- Railway
- Render
- AWS
- Azure
- Google Cloud
- DigitalOcean
- etc.

---

# 5. Relaciones entre nodos

Las conexiones tampoco deben ser genéricas.

Quiero representar el tipo de relación.

Ejemplos:

Frontend -> Backend

Relationship:

HTTP / REST

Backend -> Database

Relationship:

SQL

Backend -> Redis

Relationship:

Cache

GitHub -> GitHub Actions

Relationship:

Push / Trigger

GitHub Actions -> Railway

Relationship:

Deployment

GitHub Actions -> Vercel

Relationship:

Deployment

Backend -> Stripe

Relationship:

External API

Service -> Repository

Relationship:

Code dependency

Controller -> Service

Relationship:

Method call

Repository -> PostgreSQL

Relationship:

Database access

El usuario debe poder seleccionar una conexión y ver información sobre ella.

---

# 6. Static Architecture Analysis

Una de las funcionalidades principales debe ser analizar automáticamente un repositorio.

Idealmente el usuario podrá conectar:

- GitHub repository
- GitLab repository
- Bitbucket repository
- ZIP del proyecto
- Directorio local, si posteriormente se crea un agente/CLI

El sistema debe analizar el código y detectar componentes automáticamente.

Por ejemplo:

React:

App
|
+-- components
+-- pages
+-- hooks
+-- services
+-- contexts

Backend .NET:

Controllers
|
+-- Services
|
+-- Repositories
|
+-- EF Core
|
+-- PostgreSQL

Node.js:

Routes
|
+-- Controllers
|
+-- Services
|
+-- Repositories
|
+-- PostgreSQL

El sistema debe utilizar análisis de código, ASTs y otras técnicas apropiadas para detectar estas relaciones.

No quiero depender únicamente de nombres de archivos.

---

# 7. Dependency Graph

La aplicación debe poder construir un dependency graph.

Ejemplo:

Dashboard.jsx
    |
    v
DashboardService.js
    |
    v
/api/reports
    |
    v
ReportsController
    |
    v
ReportsService
    |
    v
ReportRepository
    |
    v
PostgreSQL

El usuario debe poder hacer zoom y navegar desde un nivel macro hasta un nivel muy detallado.

---

# 8. Diferentes niveles de visualización

Este punto es muy importante.

No quiero mostrar absolutamente todos los archivos de un proyecto grande al mismo tiempo.

Debe existir un sistema de niveles.

## Level 1 — System

Mostrar únicamente:

- Frontend
- Backend
- Database
- Infrastructure
- External services
- CI/CD

Ejemplo:

Frontend -> Backend -> Database

## Level 2 — Application

Expandir:

Frontend
- React
- Router
- API client
- State management

Backend
- Controllers
- Services
- Repositories

Database
- PostgreSQL
- Tables

## Level 3 — Code

Mostrar:

Controller
    |
Service
    |
Repository
    |
Entity

## Level 4 — Runtime

Mostrar:

Request
    |
Controller
    |
Service
    |
Database query
    |
Response

El usuario debe poder hacer drill-down.

---

# 9. Live Runtime Mode

Esta es una de las características más importantes del producto.

Quiero un modo llamado:

LIVE

En este modo, el mapa representa lo que está ocurriendo realmente en la aplicación.

Por ejemplo:

User
    |
    | POST /api/login
    v
Frontend
    |
    | HTTPS
    v
Backend
    |
    | SQL
    v
PostgreSQL

Cuando ocurre una petición real, el mapa debe mostrar visualmente el flujo.

Ejemplo:

Frontend
    |
    | REQUEST
    v
Backend
    |
    | QUERY
    v
PostgreSQL

Después:

PostgreSQL
    |
    | RESPONSE
    v
Backend
    |
    | RESPONSE
    v
Frontend

La conexión podría mostrar actividad, cantidad de requests, latencia, errores, etc.

---

# 10. Distributed Tracing

Para el modo runtime quiero utilizar estándares existentes siempre que sea posible.

OpenTelemetry debe ser considerado como una tecnología fundamental.

El objetivo es poder asociar una petición mediante un trace ID.

Ejemplo:

trace_id: abc123

Frontend
    |
    | abc123
    v
API
    |
    | abc123
    v
AuthService
    |
    | abc123
    v
PostgreSQL

La interfaz podría mostrar:

Request #18291

Frontend:
12 ms

Network:
31 ms

Backend:
87 ms

Database:
73 ms

Total:
143 ms

---

# 11. Request Timeline

Al seleccionar una request quiero poder ver un timeline.

Ejemplo:

09:43:21.012
Frontend request

        ↓ 31 ms

09:43:21.043
API received request

        ↓ 4 ms

09:43:21.047
AuthController

        ↓ 7 ms

09:43:21.054
AuthService

        ↓ 73 ms

09:43:21.127
PostgreSQL query

        ↓ 12 ms

09:43:21.139
Response

Esto debe ayudar a identificar cuellos de botella.

---

# 12. Metrics

Cada nodo puede tener métricas dependiendo del tipo.

Para servicios:

- Requests/sec
- Error rate
- Average latency
- P95 latency
- P99 latency
- CPU
- Memory

Para databases:

- Query count
- Query latency
- Connections
- Slow queries
- Errors

Para infraestructura:

- CPU
- Memory
- Network
- Disk
- Container health

Para frontend:

- Page load
- API requests
- Errors
- Web Vitals

La interfaz debe mostrar únicamente métricas relevantes para cada tipo de nodo.

---

# 13. Logs

En el futuro quiero integrar logs.

Al seleccionar un nodo:

Backend API

debería poder aparecer algo como:

Recent logs

09:43:21
POST /api/login 200 143ms

09:43:22
GET /api/users 200 82ms

09:43:23
POST /api/orders 500 923ms

Los logs deben poder relacionarse con traces cuando exista un trace ID.

---

# 14. Error Visualization

Los errores deben aparecer directamente en el mapa.

Ejemplo:

Backend
[3 errors]

Database
[12 errors]

External API
[1 timeout]

Al seleccionar el nodo:

Error rate:
2.4%

Recent errors:

500 Internal Server Error

Timeout

Connection refused

El sistema debe permitir navegar desde el error hasta el trace correspondiente cuando sea posible.

---

# 15. Architecture Health

Quiero una sección que evalúe la arquitectura.

Ejemplo:

Architecture Health

Score: 84/100

Warnings:

- Frontend has direct dependency on 14 backend endpoints.
- Service X has excessive dependencies.
- Database query latency is high.
- Production configuration differs from repository configuration.
- Service Y has no telemetry.
- External API has elevated error rate.

Esto debe ser una funcionalidad separada del simple monitoring.

---

# 16. DevOps / DevSecOps Map

El mapa también debe representar el pipeline de desarrollo y deployment.

Ejemplo:

Developer
    |
    v
GitHub
    |
    v
Pull Request
    |
    v
GitHub Actions
    |
    +--> Lint
    |
    +--> Unit Tests
    |
    +--> Integration Tests
    |
    +--> Security Scan
    |
    +--> Build
    |
    v
Deploy
    |
    +--> Vercel
    |
    +--> Railway
    |
    +--> Database

Debe ser posible visualizar:

- Git repository
- branches
- pull requests
- CI
- tests
- security scans
- builds
- deployments
- environments
- production infrastructure

---

# 17. DevSecOps

En una versión posterior quiero integrar seguridad.

Por ejemplo:

GitHub
    |
    v
Pull Request
    |
    +--> ESLint
    +--> Tests
    +--> npm audit
    +--> Dependabot
    +--> SAST
    +--> Secret scanning
    +--> Container scan
    |
    v
Build
    |
    v
Deploy

El mapa podría mostrar fallos de seguridad.

Ejemplo:

[WARNING]

Dependency vulnerability detected

Package:
example-package

Severity:
High

Used by:
Backend -> Service X

---

# 18. Infrastructure Discovery

La aplicación debería poder integrarse con APIs de proveedores.

Por ejemplo:

GitHub API
Vercel API
Railway API
AWS API
Azure API
Google Cloud API

Con permisos apropiados, debería poder descubrir:

- proyectos
- deployments
- servicios
- environments
- databases
- containers
- domains
- logs
- métricas

No se debe asumir que todos los proveedores estarán disponibles.

El sistema debe tener una arquitectura basada en adapters/connectors.

Ejemplo conceptual:

Provider
|
+-- GitHubAdapter
+-- VercelAdapter
+-- RailwayAdapter
+-- AWSAdapter
+-- AzureAdapter
+-- GCPAdapter

---

# 19. Connector Architecture

Quiero evitar un backend lleno de lógica específica de cada proveedor.

Utilizar una abstracción:

IProviderConnector

Con métodos conceptuales como:

discoverResources()
getDeployments()
getServices()
getMetrics()
getLogs()

Cada proveedor implementará su propio adapter.

---

# 20. Real-time Architecture

El frontend debe recibir eventos en tiempo real.

Considerar:

WebSockets

o

Server-Sent Events

según qué sea más apropiado.

Ejemplo:

Backend
    |
    | event
    v
WebSocket
    |
    v
React UI

Eventos:

REQUEST_STARTED
REQUEST_COMPLETED
REQUEST_FAILED
TRACE_STARTED
TRACE_COMPLETED
DEPLOYMENT_STARTED
DEPLOYMENT_COMPLETED
ERROR_DETECTED
METRIC_UPDATED
SERVICE_DOWN
DATABASE_SLOW_QUERY

---

# 21. Event Model

El sistema debe tener un modelo de eventos normalizado.

Ejemplo conceptual:

{
  eventType: "TRACE_COMPLETED",
  timestamp: "...",
  traceId: "...",
  source: "backend",
  destination: "postgresql",
  duration: 73,
  status: "success"
}

Esto permitirá que diferentes fuentes de datos puedan alimentar el mismo mapa.

---

# 22. Graph Data Model

El backend debe tener un modelo de grafo.

Node:

{
  id,
  type,
  name,
  category,
  metadata
}

Edge:

{
  id,
  source,
  target,
  type,
  protocol,
  metadata
}

Runtime event:

{
  id,
  traceId,
  sourceNode,
  targetNode,
  type,
  timestamp,
  duration,
  status
}

La implementación interna puede utilizar PostgreSQL inicialmente.

No asumir que necesitamos Neo4j desde el comienzo.

Evaluar primero si PostgreSQL es suficiente para el MVP.

---

# 23. Frontend

Quiero que la interfaz tenga una apariencia profesional de herramienta para desarrolladores.

No debe parecer un dashboard administrativo genérico.

Debe sentirse como una herramienta de observabilidad/engineering.

El canvas debe ser el protagonista.

Tecnologías candidatas:

React
TypeScript
Vite

Para el grafo:

React Flow u otra librería equivalente.

Debe soportar:

- zoom
- pan
- selección
- expand/collapse
- minimap
- búsqueda
- filtros
- agrupaciones
- navegación entre niveles
- animaciones de tráfico
- estados de nodos
- conexiones activas

---

# 24. UI Conceptual

La interfaz podría tener:

+-------------------------------------------------------+
| Logo | Project | Environment | Search | LIVE | User   |
+-------------------------------------------------------+
|                                                       |
|                                                       |
|                   ARCHITECTURE MAP                    |
|                                                       |
|       GitHub ----> CI/CD ----> Vercel                |
|                                  |                    |
|                                  v                    |
|                              Frontend                 |
|                                  |                    |
|                                  v                    |
|                               Backend                 |
|                              /       \                |
|                             /         \               |
|                       PostgreSQL    External API      |
|                                                       |
|                                                       |
+----------------------+--------------------------------+
| Selected Node        | Details                        |
|                      |                                |
| Backend API          | Requests: 1,248/min            |
| Status: Healthy      | Latency: 82ms                  |
|                      | Errors: 0.4%                   |
+----------------------+--------------------------------+

---

# 25. Node Details

Al seleccionar un nodo debe aparecer un panel lateral.

Ejemplo:

Backend API

Type:
Application

Technology:
ASP.NET Core

Environment:
Production

Status:
Healthy

Requests:
1,248/min

Latency:
82ms

Errors:
0.4%

Dependencies:

PostgreSQL
Redis
Stripe

Endpoints:

GET /api/users
POST /api/login
GET /api/orders

Actions:

View traces
View logs
View source
View dependencies
View deployments

---

# 26. Search

Debe existir búsqueda global.

Ejemplo:

Buscar:

AuthService

El sistema debe encontrar:

AuthService

Backend -> AuthController -> AuthService

AuthService -> UserRepository

AuthService -> PostgreSQL

También:

GET /api/login

trace_id

database table

GitHub workflow

etc.

---

# 27. Filters

Quiero filtros para controlar la complejidad visual.

Filtros:

- Frontend
- Backend
- Database
- Infrastructure
- DevOps
- External services
- Code
- Runtime
- Errors
- Warnings
- Healthy
- Unhealthy

También filtros por environment:

- Development
- Staging
- Production

---

# 28. Environments

La aplicación debe diferenciar ambientes.

Ejemplo:

Development

Frontend
Backend
Database

Staging

Frontend
Backend
Database

Production

Frontend
Backend
Database

El usuario debe poder cambiar de environment.

También debería ser posible comparar environments posteriormente.

---

# 29. Architecture Comparison

Feature futura.

Comparar:

Production vs Staging

Ejemplo:

Production:
Frontend -> Railway -> PostgreSQL

Staging:
Frontend -> Railway -> PostgreSQL

Warning:

Production database differs from staging database version.

---

# 30. Import / Export

La arquitectura debe poder exportarse.

Formatos potenciales:

- JSON
- PNG
- SVG
- Mermaid
- Architecture documentation

Por ejemplo:

Generate architecture documentation.

---

# 31. AI Integration

En una etapa posterior quiero integrar IA.

La IA podría analizar el mapa y responder preguntas como:

"¿Qué pasa cuando un usuario inicia sesión?"

Respuesta:

Frontend AuthContext
    ↓
POST /api/login
    ↓
AuthController
    ↓
AuthService
    ↓
UserRepository
    ↓
PostgreSQL

También:

"¿Por qué /api/orders está lento?"

La IA podría analizar:

trace
logs
database queries
latency
dependencies

y generar una explicación.

Importante:

La IA no debe inventar relaciones.

Debe utilizar únicamente datos detectados o telemetry disponible.

---

# 32. Architecture Explorer

Quiero que el usuario pueda seleccionar cualquier elemento y navegar por sus relaciones.

Ejemplo:

Selecciono:

PostgreSQL

Mostrar:

Used by:

OrderService
UserService
AuthService

Tables:

users
orders
payments

Queries:

SELECT users
SELECT orders
INSERT payments

Esto convierte el mapa en una herramienta de exploración.

---

# 33. Time Travel / Replay

Feature futura.

Quiero poder seleccionar:

09:43:00 - 09:44:00

y reproducir visualmente los eventos que ocurrieron durante ese intervalo.

Ejemplo:

09:43:01
Request

09:43:02
Database query

09:43:03
External API

09:43:04
Error

El usuario podría "reproducir" el tráfico.

---

# 34. Scalability

Debe diseñarse pensando en proyectos pequeños y grandes.

Un proyecto pequeño:

10 nodes

Un proyecto grande:

10,000+ nodes

No se debe renderizar todo simultáneamente.

Considerar:

- clustering
- virtualization
- lazy loading
- hierarchical graphs
- collapsing
- aggregation

Ejemplo:

100 microservices

Mostrar inicialmente:

API Gateway
|
+-- 25 Services
+-- 8 Databases
+-- 4 Queues

Después el usuario puede expandir.

---

# 35. Architecture Layers

El mapa debe poder cambiar entre diferentes perspectivas.

Perspective:

SYSTEM

APPLICATION

CODE

INFRASTRUCTURE

DEVOPS

RUNTIME

SECURITY

Ejemplo:

SYSTEM:

Frontend -> Backend -> Database

CODE:

Controller -> Service -> Repository

INFRASTRUCTURE:

Vercel -> Railway -> PostgreSQL

DEVOPS:

GitHub -> Actions -> Deploy

RUNTIME:

Request -> API -> Database

SECURITY:

PR -> SAST -> Dependency Scan -> Deploy

Esto evita intentar representar absolutamente todo en una sola vista.

---

# 36. MVP

No quiero intentar construir todas las funcionalidades anteriores inmediatamente.

El MVP debe enfocarse en demostrar la idea central.

MVP recomendado:

1. Crear proyecto.
2. Conectar un repositorio GitHub.
3. Analizar el repositorio.
4. Detectar frontend/backend/database.
5. Detectar dependencias básicas.
6. Construir un architecture graph.
7. Mostrarlo en React Flow.
8. Permitir seleccionar nodos.
9. Mostrar metadata.
10. Permitir zoom/pan.
11. Expandir/collapse.
12. Crear una arquitectura jerárquica.
13. Tener una primera integración con OpenTelemetry.
14. Mostrar traces básicos.
15. Mostrar flujo de requests en modo LIVE.

El MVP no necesita soportar inicialmente:

- todos los cloud providers
- todos los lenguajes
- todos los databases
- AI
- Kubernetes completo
- security scanning completo

Primero debemos demostrar que el concepto funciona.

---

# 37. Primera tecnología objetivo

Para el primer proyecto quiero optimizar para aplicaciones web modernas.

Primera combinación soportada:

Frontend:

React / TypeScript

Backend:

Node.js / TypeScript

.NET / C#

Database:

PostgreSQL

Infrastructure:

GitHub

Docker

Vercel

Railway

Telemetry:

OpenTelemetry

Esto no significa que la arquitectura final deba estar limitada a estos componentes.

Debe diseñarse para agregar nuevos adapters posteriormente.

---

# 38. Proposed Technical Architecture

Evaluar una arquitectura:

Frontend:

React
TypeScript
Vite
React Flow

Backend:

ASP.NET Core o Node.js

Base de datos:

PostgreSQL

Realtime:

WebSocket

Telemetry:

OpenTelemetry

Repository analysis:

AST parsers

GitHub integration:

GitHub API

Authentication:

OAuth

Containerization:

Docker

CI:

GitHub Actions

Antes de implementar, analizar si existe una arquitectura mejor.

No asumir automáticamente que estas tecnologías son definitivas.

---

# 39. Security Requirements

La aplicación manejará información potencialmente sensible de proyectos.

Por lo tanto:

- Nunca almacenar secrets del repositorio.
- Nunca mostrar API keys.
- OAuth debe utilizar scopes mínimos.
- Tokens deben almacenarse de forma segura.
- Secrets deben estar cifrados.
- Logs no deben contener credenciales.
- No enviar código privado a servicios externos sin consentimiento.
- Separar proyectos y usuarios.
- Implementar autorización.
- Validar webhooks.
- Verificar firmas.
- Rate limiting.
- Audit logging.

---

# 40. Privacy

La herramienta puede analizar código fuente.

Por eso el usuario debe saber exactamente:

- Qué archivos se analizan.
- Qué información se almacena.
- Qué información se envía a servicios externos.
- Qué información se utiliza para AI.
- Qué telemetry se almacena.

Debe existir una arquitectura que permita minimizar la información almacenada.

---

# 41. Product Philosophy

La herramienta no debe obligar al desarrollador a documentar manualmente su arquitectura.

La idea es:

"Your architecture should document itself."

Es decir:

El código + infraestructura + telemetry deberían generar automáticamente el mapa.

La documentación deja de ser exclusivamente manual.

---

# 42. Important distinction

Debemos diferenciar claramente:

STATIC DISCOVERY

Lo que el sistema cree que existe según:

- código
- configuración
- infraestructura
- repositorio
- deployment configuration

vs

RUNTIME OBSERVABILITY

Lo que realmente está ocurriendo según:

- traces
- logs
- metrics
- events

Ejemplo:

Static:

Frontend -> Backend -> PostgreSQL

Runtime:

Actualmente no existen requests.

O:

Frontend -> Backend -> PostgreSQL
           |
           -> Stripe

37 requests/min

Esto es importante porque una dependencia puede existir en el código pero no estar siendo utilizada actualmente.

---

# 43. Source of Truth

Cada relación debe tener un origen.

Ejemplo:

Frontend -> Backend

Source:

Detected from:
src/services/api.ts

Backend -> PostgreSQL

Source:

Detected from:
appsettings.json
Entity Framework configuration

GitHub -> Railway

Source:

Detected from:
GitHub Actions workflow
Railway deployment configuration

Runtime:

Source:

OpenTelemetry trace

Esto permitirá diferenciar:

"Detected"

de:

"Observed"

---

# 44. Confidence

Las relaciones descubiertas automáticamente deberían tener un nivel de confianza.

Ejemplo:

Frontend -> Backend

Confidence:
98%

Reason:
Detected API URL + HTTP client usage

Backend -> PostgreSQL

Confidence:
100%

Reason:
EF Core PostgreSQL configuration

Possible dependency:

Backend -> Redis

Confidence:
62%

Reason:
Redis package detected but runtime usage not confirmed.

Esto evitará presentar inferencias como hechos.

---

# 45. Explainability

Cuando el sistema detecte una relación, el usuario debería poder preguntar:

"Why are these connected?"

Y obtener:

Relationship detected because:

src/services/userService.ts
calls:

GET /api/users

which resolves to:

UsersController.GetUsers()

which uses:

UserRepository

which queries:

PostgreSQL

La herramienta debe intentar ser explicable.

---

# 46. Project Architecture

Quiero que antes de escribir código se diseñe una arquitectura limpia y escalable.

Evitar:

- god files
- god components
- global mutable state
- tight coupling
- provider-specific business logic
- hardcoded infrastructure assumptions

Utilizar principios como:

- SOLID
- Clean Architecture cuando sea apropiado
- separation of concerns
- dependency inversion
- modular architecture
- testability

---

# 47. Testing

La aplicación debe tener tests desde el principio.

Backend:

- unit tests
- integration tests
- API tests

Frontend:

- component tests
- integration tests

Analyzer:

- tests con proyectos de ejemplo

Cada parser debe probarse con fixtures reales.

Ejemplos:

React fixture

Node fixture

.NET fixture

PostgreSQL fixture

---

# 48. Demo Project

Crear un proyecto de demostración que represente una aplicación full-stack realista.

Ejemplo:

React Frontend
    |
    v
ASP.NET Core API
    |
    +--> PostgreSQL
    |
    +--> Redis
    |
    +--> External API

GitHub
    |
    v
GitHub Actions
    |
    +--> Tests
    +--> Lint
    +--> Security
    |
    v
Vercel + Railway

Este proyecto debe utilizar OpenTelemetry.

La demo será importante para demostrar la capacidad del producto.

---

# 49. User Experience

El usuario debería tener un flujo muy sencillo:

1. Sign up / Login.
2. Create Project.
3. Connect GitHub.
4. Select Repository.
5. Analyze.
6. Application generates architecture map.
7. User explores the graph.
8. User enables LIVE mode.
9. Real traffic appears.
10. User selects traces.
11. User investigates performance/errors.

La primera experiencia debe ser rápida.

---

# 50. Long-term Product

La visión completa es llegar a algo similar conceptualmente a:

"Google Maps for software architecture."

Pero no solamente un mapa.

Debe combinar:

Architecture Discovery
+
Dependency Graph
+
Infrastructure Mapping
+
DevOps Mapping
+
DevSecOps Mapping
+
Distributed Tracing
+
Logs
+
Metrics
+
Runtime Events
+
Architecture Analysis

Todo dentro de una interfaz visual.

---

# 51. What I want from you as the coding agent

Antes de implementar:

1. Analiza esta visión.
2. Identifica posibles problemas técnicos.
3. Identifica funcionalidades que deberían estar fuera del MVP.
4. Propón una arquitectura técnica concreta.
5. Propón estructura de carpetas.
6. Propón modelo de datos.
7. Propón API.
8. Propón sistema de eventos.
9. Propón estrategia de análisis de código.
10. Propón estrategia OpenTelemetry.
11. Propón estrategia de realtime.
12. Propón estrategia de autenticación.
13. Propón estrategia de integración con GitHub.
14. Propón estrategia de testing.
15. Propón estrategia de deployment.
16. Propón roadmap por fases.

No empieces inmediatamente a crear todo el producto.

Primero quiero una fase de análisis y diseño.

Después de presentar la arquitectura propuesta, espera mi aprobación antes de comenzar la implementación.

---

# 52. Development Philosophy

Priorizar:

- arquitectura sólida
- código mantenible
- modularidad
- observabilidad
- seguridad
- testing
- documentación
- extensibilidad

No quiero un prototipo que simplemente "se vea bonito".

Quiero construir una base que eventualmente pueda convertirse en un producto real.

Al mismo tiempo, evitar overengineering en el MVP.

La regla debe ser:

"Build the smallest architecture that can evolve into the full vision."

---

# 53. Final Product Definition

La definición resumida del producto es:

Una plataforma web que analiza automáticamente una aplicación y sus recursos de infraestructura, construye un mapa interactivo de su arquitectura, identifica dependencias entre componentes y posteriormente utiliza telemetry para representar en tiempo real cómo fluye la información por el sistema.

El usuario debe poder pasar de:

"¿Cómo está construida mi aplicación?"

a:

"¿Qué está ocurriendo ahora mismo?"

y posteriormente a:

"¿Por qué está ocurriendo?"

Todo desde el mismo mapa.

---

# 54. Initial Success Criteria

Consideraré exitoso el MVP si puedo conectar un repositorio real y obtener automáticamente algo parecido a:

GitHub
    |
    v
GitHub Actions
    |
    +----------------+
    |                |
    v                v
Frontend           Backend
React              ASP.NET Core
    |                |
    |                v
    |            PostgreSQL
    |
    +------ HTTPS ------>

Y posteriormente ejecutar una acción real en la aplicación y observar:

Frontend
    |
    | request
    v
Backend
    |
    | database query
    v
PostgreSQL

con:

- trace ID
- timestamps
- duration
- status
- latency

y poder seleccionar cualquiera de esos elementos para investigar qué ocurrió.

Ese es el núcleo del producto.