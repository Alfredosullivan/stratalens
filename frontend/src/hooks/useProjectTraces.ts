import { useEffect, useState } from 'react';
import { getProjectTraces } from '../services/traceService';
import type { TraceDto } from '../live/traceTypes';

// Estados de la carga de traces (unión discriminada, como useProjectGraph).
export type TracesState =
  | { status: 'idle' } // sin projectId
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; traces: TraceDto[] };

// Carga los traces de un proyecto. `refreshKey` (D1 de T23): cuando llega un trace en vivo
// (ej. el traceId del último evento LIVE), cambia y dispara un refetch, de modo que el
// trace recién ingestado aparece en el timeline. El backend persiste ANTES de emitir el
// evento LIVE, así que para cuando refetcheamos el trace ya está disponible por GET.
export function useProjectTraces(projectId: string | null, refreshKey?: string | null): TracesState {
  const [state, setState] = useState<TracesState>({ status: 'idle' });

  useEffect(() => {
    if (!projectId) {
      setState({ status: 'idle' });
      return;
    }

    let cancelled = false;
    // Solo mostramos 'loading' en la primera carga; en refetch conservamos lo ya cargado
    // para no parpadear el panel cada vez que llega un trace.
    setState((prev) => (prev.status === 'ready' ? prev : { status: 'loading' }));

    getProjectTraces(projectId)
      .then((traces) => {
        if (!cancelled) setState({ status: 'ready', traces });
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setState({ status: 'error', message: error instanceof Error ? error.message : 'Error desconocido' });
        }
      });

    return () => {
      cancelled = true;
    };
  }, [projectId, refreshKey]);

  return state;
}
