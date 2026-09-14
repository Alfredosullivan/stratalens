import { useEffect, useState } from 'react';
import { listRepositories } from '../services/githubService';
import type { Repository } from '../github/types';

export type RepositoriesState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; repositories: Repository[] };

export function useRepositories(): RepositoriesState {
  const [state, setState] = useState<RepositoriesState>({ status: 'loading' });

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });

    listRepositories()
      .then((repositories) => {
        if (!cancelled) setState({ status: 'ready', repositories });
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
