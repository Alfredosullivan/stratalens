import { useEffect, useState } from 'react';
import { getProjectGraph } from '../services/graphService';
import type { ProjectGraph } from '../graph/types';

// Estados posibles de la carga del grafo. Modelarlos como unión discriminada obliga a la
// UI a manejar cada caso (nada de "graph puede ser null y además loading y además error").
export type GraphState =
  | { status: 'idle' } // sin projectId: la página mostrará el modo demo
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; graph: ProjectGraph };

// Hook que carga el grafo de un proyecto. Cancela con una bandera para no actualizar el
// estado si el componente se desmontó o cambió el projectId antes de que resolviera.
// `refreshKey` (T30/re-análisis): cambiar su valor dispara un refetch sin depender de
// remontar el componente — mismo patrón que useProjectTraces con el traceId en vivo.
export function useProjectGraph(projectId: string | null, refreshKey?: number): GraphState {
  const [state, setState] = useState<GraphState>({ status: 'idle' });

  useEffect(() => {
    if (!projectId) {
      setState({ status: 'idle' });
      return;
    }

    let cancelled = false;
    // Solo 'loading' en la carga inicial: en un refetch por re-análisis conservamos el
    // grafo actual en pantalla hasta tener el nuevo, para no parpadear a un placeholder.
    setState((prev) => (prev.status === 'ready' ? prev : { status: 'loading' }));

    getProjectGraph(projectId)
      .then((graph) => {
        if (!cancelled) setState({ status: 'ready', graph });
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
