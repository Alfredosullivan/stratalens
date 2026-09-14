import { useState } from 'react';
import { GraphCanvas } from '../components/GraphCanvas';
import { NodeDetailPanel } from '../components/NodeDetailPanel';
import { GraphTreePanel } from '../components/GraphTreePanel';
import { useProjectGraph } from '../hooks/useProjectGraph';
import { useLiveTraces } from '../hooks/useLiveTraces';
import { useProjectTraces } from '../hooks/useProjectTraces';
import { TraceTimeline } from '../components/TraceTimeline';
import { demoGraph } from '../graph/demoGraph';
import { reanalyzeProject } from '../services/graphService';
import { ApiError } from '../services/api';
import { HEADER_HEIGHT } from '../layout/constants';
import { github } from '../theme/githubDark';
import type { GraphNode, ProjectGraph } from '../graph/types';

interface GraphPageProps {
  // null = modo demo (grafo de ejemplo, sin backend). No lee la URL directamente: quién
  // resuelve el id (ruta /projects/:id vs. /demo) es responsabilidad del router (T29),
  // esta página solo pinta lo que le pasan.
  projectId: string | null;
}

// Página del grafo. Decide QUÉ grafo mostrar (real vs demo) y en qué estado (cargando /
// error), y delega el pintado al GraphCanvas. Mantiene la selección de nodo para T13.
export function GraphPage({ projectId }: GraphPageProps) {
  // refreshKey (T27/T30): re-analizar no reemplaza el estado local, dispara un refetch
  // del GET real — mismo patrón que useProjectTraces con el traceId del último evento LIVE.
  const [refreshKey, setRefreshKey] = useState(0);
  const state = useProjectGraph(projectId, refreshKey);
  const [selected, setSelected] = useState<GraphNode | null>(null);
  const [showTree, setShowTree] = useState(true); // panel-árbol lateral, visible por defecto
  const [reanalyzing, setReanalyzing] = useState(false);
  const [reanalyzeError, setReanalyzeError] = useState<string | null>(null);

  async function handleReanalyze() {
    if (!projectId) return;
    setReanalyzing(true);
    setReanalyzeError(null);

    try {
      await reanalyzeProject(projectId);
      setRefreshKey((k) => k + 1); // el POST ya devuelve el grafo nuevo; igual refetcheamos por GET para no duplicar la fuente de verdad
    } catch (err) {
      setReanalyzeError(
        err instanceof ApiError && err.status === 404
          ? 'Este proyecto no tiene un repositorio conectado.'
          : err instanceof Error
            ? err.message
            : 'Error al re-analizar.',
      );
    } finally {
      setReanalyzing(false);
    }
  }

  // Modo LIVE (T21): se conecta al hub del proyecto y recibe los traces en vivo.
  const live = useLiveTraces(projectId);

  // Traces del proyecto para el timeline (T23). El traceId del último evento LIVE actúa
  // de refreshKey: al llegar un trace en vivo, se refetchea la lista y aparece aquí.
  const traces = useProjectTraces(projectId, live.latest?.traceId ?? null);
  const traceList = traces.status === 'ready' ? traces.traces : [];
  // Preferimos el trace del último evento en vivo; si no, el último de la lista.
  const selectedTrace =
    traceList.find((t) => t.traceId === live.latest?.traceId) ??
    traceList[traceList.length - 1] ??
    null;

  // Sin projectId → modo demo (grafo de ejemplo). Con projectId → el grafo cargado.
  const graph: ProjectGraph | null =
    state.status === 'ready' ? state.graph : projectId === null ? demoGraph : null;

  return (
    <div style={styles.root}>
      <header style={styles.header}>
        <span style={styles.title}>Stratalens</span>
        <span style={styles.subtitle}>
          {projectId === null
            ? 'Modo demo (sin projectId)'
            : state.status === 'loading'
              ? 'Cargando grafo…'
              : state.status === 'error'
                ? `Error: ${state.message}`
                : `Proyecto ${projectId}`}
        </span>

        {/* Toggle del panel-árbol lateral: única forma de volver a abrirlo tras cerrarlo. */}
        {graph && (
          <button type="button" onClick={() => setShowTree((v) => !v)} style={styles.treeToggle}>
            {showTree ? '◧ ocultar árbol' : '◨ mostrar árbol'}
          </button>
        )}

        {/* Re-análisis bajo demanda (T27): no aplica en modo demo (no hay repo real). */}
        {projectId !== null && (
          <button
            type="button"
            onClick={handleReanalyze}
            disabled={reanalyzing}
            style={styles.treeToggle}
          >
            {reanalyzing ? 'Analizando…' : '↻ Re-analizar'}
          </button>
        )}
        {reanalyzeError && <span style={styles.reanalyzeError}>{reanalyzeError}</span>}

        {/* Indicador del modo LIVE: solo con proyecto real (no en modo demo). */}
        {projectId !== null && (
          <span style={styles.live}>
            <span style={{ ...styles.liveDot, background: liveColor(live.status) }} />
            <span style={styles.liveLabel}>LIVE · {liveText(live.status)}</span>
            {live.latest && (
              <span style={styles.liveTrace}>
                último trace: {Math.round(live.latest.totalDurationMs)} ms ({live.latest.status})
              </span>
            )}
          </span>
        )}
      </header>

      <main style={styles.canvas}>
        {graph ? (
          <GraphCanvas graph={graph} onSelectNode={setSelected} liveEvent={live.latest} />
        ) : (
          <div style={styles.placeholder}>
            {state.status === 'error' ? state.message : 'Cargando…'}
          </div>
        )}
      </main>

      {/* Panel-árbol lateral (dirección visual post-Fase 4, variante B): navegación de
          los mismos nodos, agrupados por categoría. Clicar selecciona el mismo nodo. */}
      {graph && showTree && (
        <GraphTreePanel
          graph={graph}
          selectedNodeId={selected?.id ?? null}
          onSelectNode={setSelected}
          onClose={() => setShowTree(false)}
        />
      )}

      {/* Panel de detalle del nodo seleccionado (T13). */}
      {selected && <NodeDetailPanel node={selected} onClose={() => setSelected(null)} />}

      {/* Timeline waterfall del último trace (T23). Solo con proyecto real (hay traces). */}
      {projectId !== null && <TraceTimeline trace={selectedTrace} />}
    </div>
  );
}

// Traduce el estado de la conexión LIVE a un color y a un texto legible.
function liveColor(status: ReturnType<typeof useLiveTraces>['status']): string {
  switch (status) {
    case 'connected':
      return github.success; // recibiendo
    case 'connecting':
    case 'reconnecting':
      return github.attention; // en transición
    default:
      return github.fgMuted; // desconectado
  }
}

function liveText(status: ReturnType<typeof useLiveTraces>['status']): string {
  switch (status) {
    case 'connected':
      return 'conectado';
    case 'connecting':
      return 'conectando…';
    case 'reconnecting':
      return 'reconectando…';
    default:
      return 'desconectado';
  }
}

const styles: Record<string, React.CSSProperties> = {
  root: { position: 'relative', width: '100vw', height: '100vh', background: github.canvasDefault },
  header: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    // Alto FIJO (no intrínseco por padding): los paneles laterales necesitan un número
    // exacto para empezar justo debajo, no una altura que "depende del contenido".
    height: HEADER_HEIGHT,
    boxSizing: 'border-box',
    zIndex: 5,
    display: 'flex',
    alignItems: 'center',
    gap: 14,
    padding: '0 18px',
    // Superficie plana (sin transparencia): estilo minimalista de GitHub, no overlay.
    background: github.canvasSubtle,
    borderBottom: `1px solid ${github.borderDefault}`,
    color: github.fgDefault,
    fontFamily: 'system-ui, sans-serif',
  },
  title: { fontWeight: 700, fontSize: 15 },
  subtitle: { color: github.fgMuted, fontSize: 13 },
  treeToggle: {
    background: 'transparent',
    border: `1px solid ${github.borderDefault}`,
    borderRadius: 6,
    color: github.fgMuted,
    fontSize: 11.5,
    padding: '5px 10px',
    cursor: 'pointer',
    fontFamily: 'inherit',
  },
  reanalyzeError: { color: github.danger, fontSize: 11.5 },
  live: { marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 8, fontSize: 12 },
  liveDot: { width: 8, height: 8, borderRadius: '50%', display: 'inline-block' },
  liveLabel: { color: github.fgDefault, fontWeight: 600, letterSpacing: 0.4 },
  liveTrace: { color: github.fgMuted },
  canvas: { width: '100%', height: '100%' },
  placeholder: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    height: '100%',
    color: github.fgMuted,
    fontFamily: 'system-ui, sans-serif',
  },
};
