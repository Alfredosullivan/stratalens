import { apiGet, apiPost } from './api';
import type { ProjectSummary, CreatedProject } from '../projects/types';

// Dashboard de "mis proyectos" (T28).
export function listProjects(): Promise<ProjectSummary[]> {
  return apiGet<ProjectSummary[]>('/api/v1/projects');
}

// Crea el proyecto desde un repo elegido y lo analiza de inmediato (T26, síncrono).
export function createProjectFromRepository(owner: string, name: string): Promise<CreatedProject> {
  return apiPost<CreatedProject>('/api/v1/projects/from-repository', { owner, name });
}
