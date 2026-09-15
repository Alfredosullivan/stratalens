import { Handle, Position, type NodeProps, type Node } from '@xyflow/react';
import type { GraphFlowNodeData } from '../../graph/mapToReactFlow';
import { github } from '../../theme/githubDark';

type TerminalNodeType = Node<GraphFlowNodeData, 'terminal'>;

// Nodo custom con estética "ventana de terminal" (dirección visual aprobada post-Fase 4):
// barra superior con la ruta de origen + un punto de color, cuerpo con nombre/tecnología.
// El color de identidad (borde, punto, glow) llega ya resuelto en `data.color`
// (graph/nodeVisuals.ts) — este componente solo pinta, no decide colores.
//
// Handles a Left/Right (no Top/Bottom, el default de React Flow): el layout de
// computeLayers es estrictamente izquierda→derecha, así que las conexiones deben entrar
// y salir por los lados para seguir el flujo visual del grafo.
export function TerminalNode({ data }: NodeProps<TerminalNodeType>) {
  const { graphNode, color, hasChildren, childCount, isExpanded, onToggleExpand } = data;

  // La barra superior muestra el origen de detección (mismo dato que ya ve el usuario en
  // el panel de detalle, T13) — nunca texto inventado. Sin metadata.source, cae al Type.
  const path = graphNode.metadata.source ?? graphNode.type;

  // Chip de expandir (T35): solo si el nodo tiene hijos y está colapsado. stopPropagation
  // evita que el click de expandir dispare también onNodeClick (selección/panel de detalle).
  const showExpandChip = hasChildren && !isExpanded;

  return (
    <div style={{ ...styles.card, borderColor: color, boxShadow: `0 0 14px -2px ${color}` }}>
      <Handle type="target" position={Position.Left} style={{ ...styles.handle, background: color }} />

      <div style={styles.bar}>
        <span style={{ ...styles.dot, background: color, boxShadow: `0 0 6px ${color}` }} />
        <span style={styles.path} title={path}>
          {path}
        </span>
      </div>

      <div style={styles.body}>
        <div style={styles.name}>{graphNode.name}</div>
        <div style={{ ...styles.tech, color }}>{graphNode.type}</div>
        <div style={styles.category}>{graphNode.category}</div>

        {showExpandChip && (
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              onToggleExpand?.(graphNode.id);
            }}
            style={{ ...styles.expandChip, borderColor: color, color }}
            title={`Ver ${childCount} componentes internos`}
          >
            ⊕ {childCount}
          </button>
        )}
      </div>

      <Handle type="source" position={Position.Right} style={{ ...styles.handle, background: color }} />
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  card: {
    width: 190,
    background: github.canvasSubtle,
    border: '1.5px solid',
    borderRadius: 9,
    overflow: 'hidden',
    fontFamily: 'ui-monospace, "Cascadia Code", "JetBrains Mono", Consolas, monospace',
    color: github.fgDefault,
  },
  handle: { width: 8, height: 8, border: 'none' },
  bar: {
    display: 'flex',
    alignItems: 'center',
    gap: 6,
    padding: '6px 10px',
    // Superficie plana (sin gradiente): el estilo minimalista de GitHub usa color sólido,
    // no degradados — la única separación entre bar/body es el borde inferior.
    background: github.canvasDefault,
    borderBottom: `1px solid ${github.borderDefault}`,
  },
  dot: { width: 8, height: 8, borderRadius: '50%', flexShrink: 0 },
  path: {
    fontSize: 11,
    color: github.fgMuted,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
  body: { padding: '9px 11px 11px' },
  name: { fontSize: 14, fontWeight: 700 },
  tech: { fontSize: 11.5, marginTop: 2, fontWeight: 600 },
  category: { fontSize: 10, color: github.fgMuted, marginTop: 7 },
  expandChip: {
    marginTop: 9,
    padding: '2px 9px',
    background: 'transparent',
    border: '1px solid',
    borderRadius: 20,
    fontFamily: 'inherit',
    fontSize: 11,
    fontWeight: 700,
    cursor: 'pointer',
  },
};
