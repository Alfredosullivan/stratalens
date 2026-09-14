import { MarkerType, type Edge as RFEdge, type Node as RFNode } from '@xyflow/react';
import type { GraphEdge, GraphNode, ProjectGraph } from './types';
import { resolveNodeColor } from './nodeVisuals';
import { github } from '../theme/githubDark';

// Datos que cada nodo de React Flow lleva en su `data`: la etiqueta visible, el nodo
// original del dominio (para el panel de detalle de T13, sin re-buscarlo) y su color de
// identidad ya resuelto (graph/nodeVisuals.ts) — TerminalNode solo pinta, no decide.
export interface GraphFlowNodeData {
  label: string;
  graphNode: GraphNode;
  color: string;
  [key: string]: unknown;
}

// Separación horizontal entre capas. Subido de nuevo (340→400, T30): "containerized · 90%
// · inferido" es más larga que las etiquetas anteriores ("HTTP/REST"/"SQL") y no entraba
// en el hueco entre Backend y Docker, cortándose contra el borde del nodo — mismo síntoma
// que ya se corrigió una vez subiendo este mismo número (280→340, rediseño post-Fase 4).
const LAYER_WIDTH = 400;
const ROW_HEIGHT = 130;  // separación vertical entre nodos de la misma capa

// Paleta de edges según su naturaleza (RULES.md: nunca mezclar Static y Runtime sin
// etiquetar cuál es cuál). El resalte en vivo es un tercer estado, transitorio.
const STATIC_COLOR = github.fgMuted;    // inferido: gris apagado
const RUNTIME_COLOR = github.success;   // confirmado por trace
const HIGHLIGHT_COLOR = github.attention; // recorrido del último trace en vivo

// Convierte el grafo del API en nodos/edges de React Flow. Es una función PURA (sin
// estado ni efectos): fácil de razonar y de testear. Aquí vive la única lógica de la
// tarea que merece prueba unitaria.
//
// highlightedEdgeIds: edges del recorrido del último trace en vivo (T22). Se pintan
// distintos y animados; el resto queda con su estilo por sourceType.
export function mapToReactFlow(
  graph: ProjectGraph,
  highlightedEdgeIds: ReadonlySet<string> = new Set(),
): { nodes: RFNode[]; edges: RFEdge[] } {
  const layers = computeLayers(graph.nodes, graph.edges);

  // Cuántos nodos llevamos colocados en cada capa (para apilarlos en vertical).
  const rowInLayer: Record<number, number> = {};

  const nodes: RFNode[] = graph.nodes.map((n) => {
    const layer = layers[n.id] ?? 0;
    const row = rowInLayer[layer] ?? 0;
    rowInLayer[layer] = row + 1;

    return {
      id: n.id,
      type: 'terminal',
      position: { x: layer * LAYER_WIDTH + 40, y: row * ROW_HEIGHT + 40 },
      // Guardamos el nodo original en data para el panel de detalle (T13); el pintado
      // (estética "ventana de terminal") vive en components/nodes/TerminalNode.
      data: { label: n.name, graphNode: n, color: resolveNodeColor(n) } satisfies GraphFlowNodeData,
    };
  });

  const edges: RFEdge[] = graph.edges.map((e) => {
    const isRuntime = e.sourceType === 'Runtime';
    const isHighlighted = highlightedEdgeIds.has(e.id);

    // Color base según naturaleza; el resalte en vivo lo sobrescribe.
    const baseColor = isRuntime ? RUNTIME_COLOR : STATIC_COLOR;
    const color = isHighlighted ? HIGHLIGHT_COLOR : baseColor;

    // La etiqueta hace explícita la naturaleza de la relación: runtime = hecho confirmado
    // (Confidence 100), estático = inferencia con su % de confianza.
    const label = isRuntime ? `${e.type} · confirmado` : `${e.type} · ${e.confidence}% · inferido`;

    return {
      id: e.id,
      source: e.source,
      target: e.target,
      label,
      // Solo animamos lo que está confirmado o en vivo; lo estático queda quieto para que
      // el movimiento signifique algo (dato real fluyendo), no ruido permanente.
      animated: isHighlighted || isRuntime,
      style: {
        stroke: color,
        strokeWidth: isHighlighted ? 3 : 2,
        // Estático punteado (inferido), runtime/resaltado sólido (confirmado/observado).
        strokeDasharray: isRuntime || isHighlighted ? undefined : '6 4',
        // Glow neón SOLO en lo confirmado/en vivo — lo estático queda apagado a propósito:
        // el brillo significa "esto es un hecho", no decoración uniforme (mockup aprobado).
        filter: isRuntime || isHighlighted ? `drop-shadow(0 0 4px ${color})` : undefined,
      },
      labelStyle: { fill: isHighlighted ? HIGHLIGHT_COLOR : github.fgDefault, fontSize: 11, fontWeight: 600 },
      // fillOpacity:1 es la parte que importa: la caja de fondo de la etiqueta en React
      // Flow viene al 75% de opacidad por defecto (su CSS base), así que sobre un canvas
      // oscuro con glow detrás el texto se mezclaba con lo que hay debajo. Forzarla a
      // opaca + darle padding/radio la convierte en una píldora legible de verdad.
      labelBgStyle: { fill: github.canvasSubtle, fillOpacity: 1, stroke: github.borderDefault, strokeWidth: 1 },
      labelBgPadding: [6, 4],
      labelBgBorderRadius: 4,
      markerEnd: { type: MarkerType.ArrowClosed, color },
    };
  });

  return { nodes, edges };
}

// Asigna a cada nodo una "capa" según la profundidad del grafo siguiendo las flechas:
// un nodo sin dependencias entrantes queda en la capa 0, y cada destino cae una capa
// más a la derecha que su origen. Con relajación iterativa (|nodes| pasadas) el
// resultado es estable para un grafo dirigido acíclico.
function computeLayers(nodes: GraphNode[], edges: GraphEdge[]): Record<string, number> {
  const layer: Record<string, number> = {};
  for (const n of nodes) {
    layer[n.id] = 0;
  }

  for (let pass = 0; pass < nodes.length; pass++) {
    for (const e of edges) {
      // Ignorar edges que referencien nodos ausentes (defensivo).
      if (!(e.source in layer) || !(e.target in layer)) {
        continue;
      }
      if (layer[e.target] < layer[e.source] + 1) {
        layer[e.target] = layer[e.source] + 1;
      }
    }
  }

  return layer;
}
