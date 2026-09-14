import { apiGet } from './api';
import type { TraceDto } from '../live/traceTypes';

// Servicio de traces: encapsula el endpoint de lectura de T17. Los componentes piden
// "los traces de este proyecto" sin conocer la ruta ni el formato.
export function getProjectTraces(projectId: string): Promise<TraceDto[]> {
  return apiGet<TraceDto[]>(`/api/v1/projects/${projectId}/traces`);
}
