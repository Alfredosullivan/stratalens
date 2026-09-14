import { useEffect, useState } from 'react';
import { listProjects } from '../services/projectsService';
import type { ProjectSummary } from '../projects/types';

export type ProjectsState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; projects: ProjectSummary[] };

export function useProjects(): ProjectsState {
  const [state, setState] = useState<ProjectsState>({ status: 'loading' });

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });

    listProjects()
      .then((projects) => {
        if (!cancelled) setState({ status: 'ready', projects });
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setState({ status: 'error', message: error instanceof Error ? error.message : 'Error desconocido' });
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  return state;
}
