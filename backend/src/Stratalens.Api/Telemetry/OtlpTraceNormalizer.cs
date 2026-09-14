using Google.Protobuf;
using Stratalens.Application.Models;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OtlpSpan = OpenTelemetry.Proto.Trace.V1.Span;
using OtlpStatusCode = OpenTelemetry.Proto.Trace.V1.Status.Types.StatusCode;

namespace Stratalens.Api.Telemetry;

// Traduce un payload OTLP (ExportTraceServiceRequest) a la lista de TelemetrySpan
// normalizados (Application, sección 21 del contexto maestro). Es TRADUCCIÓN de formato
// de transporte, no lógica de negocio: no decide qué es un nodo ni calcula confidence,
// solo remapea los campos de OTLP al vocabulario del grafo.
//
// Estructura OTLP: ExportTraceServiceRequest → resourceSpans[] → scopeSpans[] → spans[].
// El "service.name" vive en los atributos del recurso e identifica al servicio emisor.
public static class OtlpTraceNormalizer
{
    public static IReadOnlyList<TelemetrySpan> Normalize(ExportTraceServiceRequest request)
    {
        var result = new List<TelemetrySpan>();

        foreach (var resourceSpans in request.ResourceSpans)
        {
            // service.name del recurso = el servicio que originó estos spans (SourceNode).
            var serviceName = GetAttribute(resourceSpans.Resource?.Attributes, "service.name") ?? "unknown";

            foreach (var scopeSpans in resourceSpans.ScopeSpans)
                foreach (var span in scopeSpans.Spans)
                    result.Add(MapSpan(span, serviceName));
        }

        return result;
    }

    private static TelemetrySpan MapSpan(OtlpSpan span, string serviceName)
    {
        // OTLP marca los tiempos en nanosegundos Unix. 1 tick = 100 ns, así que ticks = nanos/100.
        // UnixEpoch tiene Kind=Utc → el resultado es UTC, como exige la invariante de Span.
        var startedAt = DateTime.UnixEpoch.AddTicks((long)(span.StartTimeUnixNano / 100));

        // Duración = fin - inicio, en ms. Guarda contra spans mal formados (fin < inicio).
        var durationNanos = span.EndTimeUnixNano >= span.StartTimeUnixNano
            ? span.EndTimeUnixNano - span.StartTimeUnixNano
            : 0;
        var durationMs = durationNanos / 1_000_000.0;

        return new TelemetrySpan(
            TraceId: ToHex(span.TraceId),
            SpanId: ToHex(span.SpanId),
            // parent_span_id vacío ⇒ span raíz del trace (ParentSpanId null).
            ParentSpanId: span.ParentSpanId.Length == 0 ? null : ToHex(span.ParentSpanId),
            SourceNode: serviceName,
            TargetNode: ResolveTarget(span),
            Operation: span.Name,
            StartedAt: startedAt,
            DurationMs: durationMs,
            Status: MapStatus(span.Status?.Code));
    }

    // Deriva el nodo destino de atributos semánticos convencionales de OTel (best-effort MVP),
    // en orden de especificidad. Los atributos de base de datos (db.system[.name]) van antes
    // que server.address: para una llamada a Postgres queremos identificar el nodo "postgresql",
    // no el host "localhost". db.system.name es la convención nueva; db.system la anterior.
    private static string? ResolveTarget(OtlpSpan span) =>
        GetAttribute(span.Attributes, "peer.service")
        ?? GetAttribute(span.Attributes, "db.system.name")
        ?? GetAttribute(span.Attributes, "db.system")
        ?? GetAttribute(span.Attributes, "server.address")
        ?? GetAttribute(span.Attributes, "net.peer.name");

    private static string MapStatus(OtlpStatusCode? code) => code switch
    {
        OtlpStatusCode.Ok => "OK",
        OtlpStatusCode.Error => "ERROR",
        _ => "UNSET"
    };

    private static string? GetAttribute(IEnumerable<KeyValue>? attributes, string key) =>
        attributes?.FirstOrDefault(a => a.Key == key)?.Value?.StringValue;

    private static string ToHex(ByteString bytes) => Convert.ToHexString(bytes.Span).ToLowerInvariant();
}
