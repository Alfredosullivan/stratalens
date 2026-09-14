using Stratalens.Application.Models;

namespace Stratalens.Application.Abstractions;

// Puerto (Ports & Adapters) para notificar en vivo un trace recién ingestado.
// Application define el CONTRATO sin conocer el transporte: el adaptador concreto
// (SignalR) vive en la capa que puede ver el Hub (Api), no aquí. Así Application no
// se acopla a .NET/SignalR y el mecanismo realtime se puede cambiar sin tocar el use case.
public interface ILiveTraceNotifier
{
    // Emite el evento LIVE a los clientes suscritos al proyecto. Es best-effort: si no
    // hay suscriptores, no pasa nada — notificar nunca debe tumbar la ingesta.
    Task TraceIngestedAsync(Guid projectId, LiveTraceEvent liveEvent, CancellationToken ct = default);
}
