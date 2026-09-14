import { apiGet, apiPost } from './api';
import type { ProjectGraph } from '../graph/types';

// Servicio del grafo: encapsula el endpoint concreto de T11. Los componentes no conocen
// la ruta ni el formato de la petición, solo piden "el grafo de este proyecto".
export function getProjectGraph(projectId: string): Promise<ProjectGraph> {
  return apiGet<ProjectGraph>(`/api/v1/projects/${projectId}/graph`);
}

// Re-analiza un proyecto ya conectado a un repo (T27): repite el análisis con el
// contenido ACTUAL. Devuelve el grafo ya actualizado (mismo shape que getProjectGraph).
export function reanalyzeProject(projectId: string): Promise<ProjectGraph> {
  return apiPost<ProjectGraph>(`/api/v1/projects/${projectId}/analyze`);
}
