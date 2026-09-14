// Tipos de lectura de traces del backend (endpoint GET /projects/{id}/traces, T17).
// Espejo de TraceDto/SpanDto del backend. El timeline (T23) usa startedAt + durationMs
// de cada span para calcular su posición y ancho en el waterfall.

export interface SpanDto {
  spanId: string;
  parentSpanId: string | null; // null = span raíz del trace
  sourceNode: string;
  targetNode: string | null;
  operation: string;
  startedAt: string; // ISO 8601 (UTC)
  durationMs: number;
  status: string;
}

export interface TraceDto {
  traceId: string;
  durationMs: number; // duración del span raíz: abarca toda la request
  spans: SpanDto[];
}
