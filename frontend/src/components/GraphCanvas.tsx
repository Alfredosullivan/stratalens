import { useMemo } from 'react';
import { Background, Controls, MiniMap, ReactFlow } from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import type { GraphNode, ProjectGraph } from '../graph/types';
import type { LiveTraceEvent } from '../live/types';
import { mapToReactFlow, type GraphFlowNodeData } from '../graph/mapToReactFlow';
import { resolveTracePath } from '../graph/resolveTracePath';
import { TerminalNode } from './nodes/TerminalNode';
import { github } from '../theme/githubDark';

// Definido FUERA del componente: React Flow re-monta todos los nodos custom si el objeto
// nodeTypes cambia de referencia en cada render (trampa habitual de la librería).
const nodeTypes = { terminal: TerminalNode };

interface GraphCanvasProps {
  graph: ProjectGraph;
  // Se dispara al seleccionar un nodo del DOMINIO (o null al hacer click en el vacío).
  // Pasamos el GraphNode, no el nodo de React Flow: la página no depende de la librería.
  onSelectNode?: (node: GraphNode | null) => void;
  // Último trace recibido en vivo (T22): su recorrido se resalta/anima en el mapa.
  liveEvent?: LiveTraceEvent | null;
}

// Canvas del grafo. Solo se ocupa de PINTAR: recibe el grafo del dominio, lo convierte a
// la representación de React Flow (memoizada) y monta el lienzo con zoom/pan/minimapa.
// No sabe de dónde salió el grafo (fetch, demo...), eso es responsabilidad de la página.
export function GraphCanvas({ graph, onSelectNode, liveEvent }: GraphCanvasProps) {
  // Edges del recorrido del último trace (vacío si no hay evento o no resuelve a edges).
  const highlightedEdgeIds = useMemo(
    () => (liveEvent ? resolveTracePath(graph, liveEvent) : new Set<string>()),
    [graph, liveEvent],
  );

  const { nodes, edges } = useMemo(
    () => mapToReactFlow(graph, highlightedEdgeIds),
    [graph, highlightedEdgeIds],
  );

  return (
    <>
      <ReactFlow
        nodes={nodes}
        edges={edges}
        nodeTypes={nodeTypes}
        fitView
        onNodeClick={(_, node) => onSelectNode?.((node.data as GraphFlowNodeData).graphNode)}
        onPaneClick={() => onSelectNode?.(null)}
        style={{ background: github.canvasDefault }}
      >
        <Background color={github.canvasSubtle} gap={22} />
        <Controls />
        <MiniMap pannable zoomable maskColor="rgba(13,17,23,0.7)" nodeColor={github.canvasSubtle} />
      </ReactFlow>

      {/* Badge de la última request en vivo: duración total + status del trace (T22). */}
      {liveEvent && (
        <div style={styles.liveBadge}>
          <span style={styles.bolt}>⚡</span>
          <span>
            Última request: <strong>{Math.round(liveEvent.totalDurationMs)} ms</strong> ({liveEvent.status})
          </span>
        </div>
      )}
    </>
  );
}

const styles: Record<string, React.CSSProperties> = {
  liveBadge: {
    position: 'absolute',
    // Por encima del panel de timeline (T23, dockeado abajo ~190px) para no solaparse.
    bottom: 202,
    left: 18,
    zIndex: 5,
    display: 'flex',
    alignItems: 'center',
    gap: 8,
    padding: '8px 14px',
    background: github.canvasSubtle,
    border: `1px solid ${github.attention}`,
    borderRadius: 10,
    color: github.fgDefault,
    fontFamily: 'system-ui, sans-serif',
    fontSize: 13,
  },
  bolt: { color: github.attention, fontSize: 15 },
};
