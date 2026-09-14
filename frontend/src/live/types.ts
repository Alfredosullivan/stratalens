// Tipos del evento LIVE que emite el backend por SignalR (evento 'TraceIngested').
// Son el espejo de LiveTraceEvent/LiveTraceHop del backend (Application/Models):
// el contrato realtime es una cosa aparte del DTO REST del grafo, igual que graph/types.

export interface LiveTraceHop {
  sourceNode: string;
  targetNode: string | null; // null si el span no es una llamada saliente
  operation: string;
  durationMs: number;
  status: string;
}

export interface LiveTraceEvent {
  traceId: string;
  totalDurationMs: number; // duración del span raíz: abarca toda la request
  status: string;          // status del span raíz ("OK" | "ERROR" | ...)
  hops: LiveTraceHop[];    // spans ordenados por tiempo de inicio (recorrido real)
}
