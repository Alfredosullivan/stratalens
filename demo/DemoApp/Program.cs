using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// --- Configuración ---
// DB propia del demo (separada de la del producto), endpoint OTLP de nuestro producto
// y la clave de ingesta. Configuramos el exporter EN CÓDIGO (no por env vars) para evitar
// ambigüedades: con HttpProtobuf y Endpoint fijado en código, se usa la URL TAL CUAL
// (hay que incluir la ruta completa /api/v1/telemetry/traces — nuestro endpoint).
var dbConnection = builder.Configuration.GetConnectionString("DemoDb")
    ?? "Host=localhost;Port=5433;Database=demo_app;Username=observability;Password=devlocalpass";
var otlpEndpoint = builder.Configuration["Telemetry:OtlpEndpoint"]
    ?? "http://localhost:5080/api/v1/telemetry/traces";
var ingestKey = builder.Configuration["Telemetry:IngestKey"] ?? "";

// --- Postgres: NpgsqlDataSource. Npgsql emite un Activity por cada comando SQL,
//     que OpenTelemetry recoge vía AddNpgsql() → aparece como span hijo del request. ---
var dataSource = new NpgsqlDataSourceBuilder(dbConnection).Build();
builder.Services.AddSingleton(dataSource);

// --- OpenTelemetry: trazas del backend demo ---
builder.Services.AddOpenTelemetry()
    // service.name = "Backend": es el SourceNode que verá nuestro normalizador OTLP.
    // Se alinea a propósito con el Name del nodo backend del grafo ("Backend"), para que
    // la resolución Opción A (match case-insensitive Name/Type) case y el hop se promueva
    // a Edge de runtime (T20) y se anime en el mapa (T22). En un producto real este
    // acoplamiento no existiría; es un atajo consciente del demo.
    .ConfigureResource(resource => resource.AddService("Backend"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()   // span SERVER: el backend atendiendo la request
        .AddNpgsql()                      // span CLIENT: la query a Postgres
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);        // ruta completa, se usa tal cual
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
            options.Headers = $"X-Ingest-Key={ingestKey}";   // autenticación de ingesta
        }));

var app = builder.Build();

// --- Inicialización de datos: tabla + seed, para que la query devuelva algo ---
await using (var conn = await dataSource.OpenConnectionAsync())
{
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        CREATE TABLE IF NOT EXISTS pedidos (
            id     SERIAL PRIMARY KEY,
            cliente TEXT NOT NULL,
            total  NUMERIC(10,2) NOT NULL
        );
        INSERT INTO pedidos (cliente, total)
        SELECT 'Cliente demo', 99.90
        WHERE NOT EXISTS (SELECT 1 FROM pedidos);
        """;
    await cmd.ExecuteNonQueryAsync();
}

// Sirve el "frontend" mínimo (wwwroot/index.html) que dispara la request.
app.UseDefaultFiles();
app.UseStaticFiles();

// Endpoint que consulta Postgres: genera un trace SERVER → CLIENT(SQL).
app.MapGet("/pedidos", async (NpgsqlDataSource ds, CancellationToken ct) =>
{
    var pedidos = new List<object>();
    await using var conn = await ds.OpenConnectionAsync(ct);
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id, cliente, total FROM pedidos ORDER BY id";
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    while (await reader.ReadAsync(ct))
    {
        pedidos.Add(new { id = reader.GetInt32(0), cliente = reader.GetString(1), total = reader.GetDecimal(2) });
    }
    return Results.Ok(pedidos);
});

app.Run();
