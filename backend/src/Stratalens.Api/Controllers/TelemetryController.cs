using Google.Protobuf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stratalens.Api.Auth;
using Stratalens.Api.Telemetry;
using Stratalens.Application.UseCases;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace Stratalens.Api.Controllers;

// Ingesta de telemetría OTLP. Se autentica con la CLAVE DE INGESTA del proyecto
// (esquema IngestKey), no con la cookie de usuario: lo llama una máquina (el demo),
// no una persona. El endpoint solo traduce HTTP ↔ dominio; la persistencia vive en el use case.
[ApiController]
[Route("api/v1/telemetry")]
[Authorize(AuthenticationSchemes = IngestKeyAuthenticationHandler.SchemeName)]
public class TelemetryController : ControllerBase
{
    private const string OtlpProtobufContentType = "application/x-protobuf";

    private readonly IngestTelemetryUseCase _ingest;

    public TelemetryController(IngestTelemetryUseCase ingest)
    {
        _ingest = ingest;
    }

    // Recibe spans en OTLP/HTTP protobuf, los normaliza y los persiste asociados al
    // proyecto de la clave de ingesta (nunca al projectId del body: no se confía en el emisor).
    [HttpPost("traces")]
    [Consumes(OtlpProtobufContentType)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> IngestTraces(CancellationToken ct)
    {
        var projectId = User.GetProjectId();
        if (projectId is null)
            return Unauthorized(); // el esquema IngestKey garantiza el claim; defensa extra

        // OTLP/HTTP protobuf no es JSON → no hay model binding: leemos el cuerpo crudo.
        ExportTraceServiceRequest request;
        try
        {
            using var buffer = new MemoryStream();
            await Request.Body.CopyToAsync(buffer, ct);
            request = ExportTraceServiceRequest.Parser.ParseFrom(buffer.ToArray());
        }
        catch (InvalidProtocolBufferException)
        {
            return BadRequest("Payload OTLP inválido.");
        }

        var spans = OtlpTraceNormalizer.Normalize(request);
        if (spans.Count > 0)
            await _ingest.ExecuteAsync(projectId.Value, spans, ct);

        // OTLP espera 200 con un ExportTraceServiceResponse; vacío = éxito total.
        var response = new ExportTraceServiceResponse();
        return File(response.ToByteArray(), OtlpProtobufContentType);
    }
}
