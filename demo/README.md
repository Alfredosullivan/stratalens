# Demo instrumentado — Stratalens

App mínima **frontend → backend ASP.NET Core → Postgres**, instrumentada con el SDK oficial
de **OpenTelemetry .NET**, que exporta sus traces por **OTLP/HTTP protobuf** al endpoint de
ingesta de Stratalens (`POST /api/v1/telemetry/traces`).

Es la app **observada** para probar la Fase 3 (Runtime). No forma parte del producto: vive
fuera de `backend.sln` y tiene su propia base de datos (`demo_app`).

## Qué demuestra

Una request real a `/pedidos` genera un trace con dos spans:

- **SERVER** — el backend atendiendo la request (`GET /pedidos`), instrumentación `AddAspNetCoreInstrumentation`.
- **CLIENT** — la query a Postgres, instrumentación `AddNpgsql`.

El normalizador OTLP del producto mapea `service.name` → `SourceNode` (`Backend`) y
`db.system[.name]` → `TargetNode` (`postgresql`). El `service.name` se fija a `Backend`
(no `demo-backend`) a propósito: así casa con el `Name` del nodo backend del grafo y el
hop se promueve a Edge de runtime (T20) y se anima en el mapa (T22).

## Cómo ejecutarlo

Requisitos: el Postgres local de dev en el puerto `5433` (docker-compose de la raíz) y el
backend del producto corriendo en `http://localhost:5080`.

### 1. Crear la base de datos del demo (una vez)

```bash
docker exec stratalens-db createdb -U observability demo_app
```

### 2. Obtener una clave de ingesta

Al crear un proyecto en el producto (`POST /api/v1/projects`), la respuesta incluye
`ingestKey` **una única vez**. Guárdala: es la credencial que autentica al demo.

### 3. Arrancar el demo

```bash
cd demo/DemoApp
ASPNETCORE_URLS="http://localhost:5090" \
ConnectionStrings__DemoDb="Host=localhost;Port=5433;Database=demo_app;Username=observability;Password=devlocalpass" \
Telemetry__OtlpEndpoint="http://localhost:5080/api/v1/telemetry/traces" \
Telemetry__IngestKey="<tu-clave-de-ingesta>" \
dotnet run --no-launch-profile
```

Abre `http://localhost:5090`, pulsa **"Pedir /pedidos"** (o `curl http://localhost:5090/pedidos`).
En unos segundos el trace queda persistido y se puede leer en
`GET /api/v1/projects/{id}/traces` del producto.

## Notas técnicas

- **Ruta del exporter:** el endpoint OTLP se configura **en código** con la ruta completa
  (`.../api/v1/telemetry/traces`). Con `HttpProtobuf` y `Endpoint` fijado en código, la URL
  se usa tal cual — el exporter NO le añade `/v1/traces` (eso solo pasa con la variable de
  entorno *general* `OTEL_EXPORTER_OTLP_ENDPOINT`).
- **Autenticación:** la clave viaja en el header `X-Ingest-Key` (opción `Headers` del exporter),
  que resuelve el esquema `IngestKey` del producto.
- **Flush:** en desarrollo se puede acelerar el envío con `OTEL_BSP_SCHEDULE_DELAY=1000` (ms).
