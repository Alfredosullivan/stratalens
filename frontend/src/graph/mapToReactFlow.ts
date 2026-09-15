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
  // Jerarquía (T35): un nodo con hijos (Backend → sus Controllers) puede expandirse.
  // hasChildren/childCount/isExpanded describen el estado; onToggleExpand lo alterna.
  // Undefined para nodos sin hijos (la mayoría) — la tarjeta no muestra chip entonces.
  hasChildren?: boolean;
  childCount?: number;
  isExpanded?: boolean;
  onToggleExpand?: (nodeId: string) => void;
  [key: string]: unknown;
}

// Separación horizontal entre capas. Subido de nuevo (340→400, T30): "containerized · 90%
// · inferido" es más larga que las etiquetas anteriores ("HTTP/REST"/"SQL") y no entraba
// en el hueco entre Backend y Docker, cortándose contra el borde del nodo — mismo síntoma
// que ya se corrigió una vez subiendo este mismo número (280→340, rediseño post-Fase 4).
// Layout SIZE-AWARE (T35): en vez de un ancho de capa fijo, cada capa parte del ancho REAL
// de la anterior, así un contenedor expandido y ancho empuja las capas siguientes a la
// derecha sin solaparse. COLLAPSED_* son el tamaño de una tarjeta TerminalNode; H_GAP deja
// hueco para las etiquetas de los edges (por eso es amplio); V_GAP separa nodos de la misma
// capa apilados verticalmente usando su alto real.
const COLLAPSED_W = 190;
const COLLAPSED_H = 140;
const H_GAP = 200;
const V_GAP = 40;
const ORIGIN = 40;

// Dimensiones del anidamiento (T35). Los hijos son CHIPS compactos (punto + nombre), no
// tarjetas completas, y se acomodan en un GRID de varias columnas — así muchos hijos (ej. 13
// controllers) no se ven como una lista enorme, sino como bloques compactos estilo Archify.
// El contenedor se dimensiona al grid: header + filas necesarias + padding.
const CHIP_W = 150;
const CHIP_H = 34;
const CHIP_GAP = 10;
const GRID_COLS = 3;          // máximo de columnas del grid de hijos
const GRID_PAD = 14;          // padding interior del contenedor alrededor del grid
const CONTAINER_HEADER = 46;  // alto de la cabecera del contenedor (el grid empieza debajo)

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
// expandedIds/onToggleExpand: jerarquía (T35). Un nodo con hijos se dibuja colapsado
// (tarjeta + chip "⊕ N") o expandido (contenedor con los hijos dentro).
export function mapToReactFlow(
  graph: ProjectGraph,
  highlightedEdgeIds: ReadonlySet<string> = new Set(),
  expandedIds: ReadonlySet<string> = new Set(),
  onToggleExpand?: (nodeId: string) => void,
): { nodes: RFNode[]; edges: RFEdge[] } {
  // Partición: nodos raíz (sin padre → van al layout izq→der de siempre) e hijos (con
  // parentNodeId → se posicionan DENTRO de su padre, no en el layout principal).
  const childrenByParent = new Map<string, GraphNode[]>();
  const roots: GraphNode[] = [];
  for (const n of graph.nodes) {
    if (n.parentNodeId) {
      const list = childrenByParent.get(n.parentNodeId) ?? [];
      list.push(n);
      childrenByParent.set(n.parentNodeId, list);
    } else {
      roots.push(n);
    }
  }

  // El layout (capas por profundidad) se calcula SOLO sobre las raíces: los hijos no tienen
  // posición propia en el flujo, viven dentro de su padre.
  const layers = computeLayers(roots, graph.edges);

  // 1. Tamaño de cada raíz según su estado (colapsada = tarjeta; expandida = contenedor grid).
  interface PlacedRoot {
    node: GraphNode;
    layer: number;
    width: number;
    height: number;
    kids: GraphNode[];
    isExpanded: boolean;
  }
  const placed: PlacedRoot[] = roots.map((n) => {
    const kids = childrenByParent.get(n.id) ?? [];
    const isExpanded = kids.length > 0 && expandedIds.has(n.id);
    let width = COLLAPSED_W;
    let height = COLLAPSED_H;
    if (isExpanded) {
      const cols = Math.min(GRID_COLS, kids.length);
      const rows = Math.ceil(kids.length / cols);
      width = GRID_PAD * 2 + cols * CHIP_W + (cols - 1) * CHIP_GAP;
      height = CONTAINER_HEADER + GRID_PAD * 2 + rows * CHIP_H + (rows - 1) * CHIP_GAP;
    }
    return { node: n, layer: layers[n.id] ?? 0, width, height, kids, isExpanded };
  });

  // 2. X de cada capa = acumulado del ancho REAL de las capas anteriores + hueco. Así un
  //    contenedor ancho empuja las capas siguientes a la derecha sin solaparse.
  const maxWidthByLayer = new Map<number, number>();
  for (const p of placed) {
    maxWidthByLayer.set(p.layer, Math.max(maxWidthByLayer.get(p.layer) ?? 0, p.width));
  }
  const orderedLayers = [...maxWidthByLayer.keys()].sort((a, b) => a - b);
  const layerX = new Map<number, number>();
  let cursorX = ORIGIN;
  for (const layer of orderedLayers) {
    layerX.set(layer, cursorX);
    cursorX += maxWidthByLayer.get(layer)! + H_GAP;
  }

  const nodes: RFNode[] = [];
  // Ids de nodos realmente visibles (un hijo colapsado no se dibuja) → filtra los edges.
  const visibleIds = new Set<string>();
  // Cursor vertical por capa: apila los nodos usando su ALTO real, sin solaparse.
  const yCursorByLayer = new Map<number, number>();

  for (const p of placed) {
    const n = p.node;
    const x = layerX.get(p.layer)!;
    const y = yCursorByLayer.get(p.layer) ?? ORIGIN;
    yCursorByLayer.set(p.layer, y + p.height + V_GAP);
    const position = { x, y };

    const data: GraphFlowNodeData = {
      label: n.name,
      graphNode: n,
      color: resolveNodeColor(n),
      hasChildren: p.kids.length > 0,
      childCount: p.kids.length,
      isExpanded: p.isExpanded,
      onToggleExpand,
    };

    if (p.isExpanded) {
      // Grid de hijos: hasta GRID_COLS columnas. Los hijos se emiten con parentId +
      // extent:'parent' (React Flow los posiciona relativos a la esquina del contenedor).
      const cols = Math.min(GRID_COLS, p.kids.length);
      nodes.push({ id: n.id, type: 'container', position, data, style: { width: p.width, height: p.height } });
      visibleIds.add(n.id);

      p.kids.forEach((c, i) => {
        const col = i % cols;
        const rowIdx = Math.floor(i / cols);
        nodes.push({
          id: c.id,
          type: 'component',          // chip compacto (no tarjeta completa)
          parentId: n.id,             // React Flow: este nodo vive DENTRO del contenedor
          extent: 'parent',           // no puede arrastrarse fuera de los límites del padre
          position: {
            x: GRID_PAD + col * (CHIP_W + CHIP_GAP),
            y: CONTAINER_HEADER + GRID_PAD + rowIdx * (CHIP_H + CHIP_GAP),
          },
          data: { label: c.name, graphNode: c, color: resolveNodeColor(c) } satisfies GraphFlowNodeData,
        });
        visibleIds.add(c.id);
      });
    } else {
      // Colapsado (o sin hijos): tarjeta normal. TerminalNode muestra el chip "⊕ N" si
      // hasChildren && !isExpanded.
      nodes.push({ id: n.id, type: 'terminal', position, data });
      visibleIds.add(n.id);
    }
  }

  const edges: RFEdge[] = graph.edges
    // Solo edges entre nodos visibles: si un extremo es un hijo colapsado (no dibujado),
    // el edge tampoco se dibuja. En T34 no hay edges entre hijos, pero esto lo deja correcto.
    .filter((e) => visibleIds.has(e.source) && visibleIds.has(e.target))
    .map((e) => {
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
