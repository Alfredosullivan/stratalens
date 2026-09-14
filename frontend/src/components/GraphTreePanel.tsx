import type { GraphNode, ProjectGraph } from '../graph/types';
import { resolveNodeColor } from '../graph/nodeVisuals';
import { HEADER_HEIGHT } from '../layout/constants';
import { github } from '../theme/githubDark';

interface GraphTreePanelProps {
  graph: ProjectGraph;
  selectedNodeId: string | null;
  // Mismo callback que ya usa GraphCanvas al clicar un nodo: clicar aquí selecciona el
  // MISMO nodo (abre el panel de detalle, T13) — es otra puerta de entrada, no otro dato.
  onSelectNode: (node: GraphNode) => void;
  // Cierra el panel. El botón de reabrirlo vive en el header de GraphPage (toggle) —
  // una vez cerrado, este componente ni se monta, así que no puede reabrirse solo.
  onClose: () => void;
}

// Orden de categorías: stack principal primero, soporte/infra después. Cualquier
// categoría no listada (futuras) cae al final, en vez de desaparecer.
const CATEGORY_ORDER = ['Application', 'Database', 'Infrastructure', 'DevOps', 'Deployment', 'External', 'Code'];

// Panel-árbol lateral (dirección visual post-Fase 4, variante B del mockup): navegación
// estilo file-explorer de los nodos YA existentes en el grafo, agrupados por Category.
// Deliberadamente NO pretende ser la estructura real de carpetas del repo (el grafo hoy
// no la conoce) — agrupa por categoría de dominio, que es el dato honesto que sí existe.
export function GraphTreePanel({ graph, selectedNodeId, onSelectNode, onClose }: GraphTreePanelProps) {
  const groups = groupByCategory(graph.nodes);

  return (
    <aside style={styles.panel}>
      <div style={styles.header}>
        <span style={styles.headerTitle}>Árbol</span>
        <button type="button" style={styles.close} onClick={onClose} aria-label="Ocultar árbol">
          ×
        </button>
      </div>

      {groups.map(([category, nodes]) => (
        <div key={category} style={styles.group}>
          <div style={styles.categoryLabel}>▾ {category}</div>
          {nodes.map((node) => {
            const color = resolveNodeColor(node);
            const isSelected = node.id === selectedNodeId;
            return (
              <button
                key={node.id}
                type="button"
                onClick={() => onSelectNode(node)}
                style={{
                  ...styles.item,
                  color: isSelected ? github.fgDefault : github.fgMuted,
                  background: isSelected ? 'rgba(255,255,255,0.06)' : 'transparent',
                  borderLeft: `2px solid ${isSelected ? color : 'transparent'}`,
                }}
              >
                <span style={{ ...styles.dot, background: color, boxShadow: `0 0 5px ${color}` }} />
                {node.name}
              </button>
            );
          })}
        </div>
      ))}
    </aside>
  );
}

// Agrupa por categoría respetando CATEGORY_ORDER; categorías sin nodos no aparecen.
function groupByCategory(nodes: GraphNode[]): Array<[string, GraphNode[]]> {
  const byCategory = new Map<string, GraphNode[]>();
  for (const node of nodes) {
    const list = byCategory.get(node.category) ?? [];
    list.push(node);
    byCategory.set(node.category, list);
  }

  const ordered = CATEGORY_ORDER.filter((c) => byCategory.has(c));
  const rest = [...byCategory.keys()].filter((c) => !CATEGORY_ORDER.includes(c));
  return [...ordered, ...rest].map((c) => [c, byCategory.get(c)!]);
}

const styles: Record<string, React.CSSProperties> = {
  panel: {
    position: 'absolute',
    // Empieza DEBAJO del header (ver layout/constants.ts): antes tapaba el título de la
    // página porque usaba top:0 con la altura del header sin descontar.
    top: HEADER_HEIGHT,
    left: 0,
    zIndex: 6,
    width: 210,
    height: `calc(100% - ${HEADER_HEIGHT}px)`,
    background: github.canvasSubtle,
    borderRight: `1px solid ${github.borderDefault}`,
    padding: '0 0 20px',
    overflowY: 'auto',
    fontFamily: 'ui-monospace, "Cascadia Code", "JetBrains Mono", Consolas, monospace',
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '10px 14px',
    borderBottom: `1px solid ${github.borderDefault}`,
    marginBottom: 8,
  },
  headerTitle: { fontSize: 11, letterSpacing: 0.8, color: github.fgMuted, textTransform: 'uppercase' },
  close: {
    background: 'transparent',
    border: `1px solid ${github.borderDefault}`,
    borderRadius: 6,
    color: github.fgMuted,
    fontSize: 14,
    lineHeight: 1,
    width: 22,
    height: 22,
    cursor: 'pointer',
  },
  group: { marginBottom: 14 },
  categoryLabel: {
    padding: '4px 16px',
    fontSize: 11,
    letterSpacing: 0.5,
    color: github.fgMuted,
  },
  item: {
    display: 'flex',
    alignItems: 'center',
    gap: 8,
    width: '100%',
    padding: '6px 16px',
    background: 'none',
    border: 'none',
    borderLeft: '2px solid transparent',
    font: 'inherit',
    fontSize: 12.5,
    textAlign: 'left',
    cursor: 'pointer',
  },
  dot: { width: 7, height: 7, borderRadius: '50%', flexShrink: 0 },
};
