import { Handle, Position, type NodeProps, type Node } from '@xyflow/react';
import type { GraphFlowNodeData } from '../../graph/mapToReactFlow';
import { github } from '../../theme/githubDark';

type ContainerNodeType = Node<GraphFlowNodeData, 'container'>;

// Nodo contenedor (T35): la versión EXPANDIDA de un nodo con hijos (ej. Backend con sus
// Controllers dentro). Es una caja grande con una cabecera (identidad del nodo + chip para
// colapsar) y un cuerpo vacío: los hijos NO se pintan aquí, los posiciona React Flow encima
// de este cuerpo vía parentId (ver mapToReactFlow). El tamaño llega por `style` en el nodo
// de React Flow, así que la raíz llena el 100% del espacio asignado.
//
// "Estar dentro de" (contención) es composición estructural, no un Edge — por eso el
// anidamiento es visual (caja dentro de caja), no una flecha (decisión de modelado, Fase 6).
export function ContainerNode({ data }: NodeProps<ContainerNodeType>) {
  const { graphNode, color, childCount, onToggleExpand } = data;
  const path = graphNode.metadata.source ?? graphNode.type;

  return (
    <div style={{ ...styles.container, borderColor: color, boxShadow: `0 0 20px -4px ${color}` }}>
      {/* Handles alineados a la cabecera (no al centro de la caja alta) para que los edges
          entren/salgan a la altura del "nodo", no a media caja. */}
      <Handle type="target" position={Position.Left} style={{ ...styles.handle, background: color, top: 23 }} />

      <div style={styles.header}>
        <span style={{ ...styles.dot, background: color, boxShadow: `0 0 6px ${color}` }} />
        <div style={styles.headerText}>
          <span style={styles.name} title={path}>
            {graphNode.name}
          </span>
          <span style={{ ...styles.tech, color }}>
            {graphNode.type} · {childCount} dentro
          </span>
        </div>
        <button
          type="button"
          onClick={(e) => {
            e.stopPropagation();
            onToggleExpand?.(graphNode.id);
          }}
          style={{ ...styles.collapse, borderColor: color, color }}
          title="Colapsar"
        >
          ⊖
        </button>
      </div>

      <Handle type="source" position={Position.Right} style={{ ...styles.handle, background: color, top: 23 }} />
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  container: {
    width: '100%',
    height: '100%',
    // Fondo un punto MÁS oscuro que las tarjetas hijas (canvasSubtle) para que se lea la
    // jerarquía: el contenedor es "el fondo", los hijos flotan encima con su propia superficie.
    background: github.canvasDefault,
    border: '1.5px solid',
    borderRadius: 11,
    fontFamily: 'ui-monospace, "Cascadia Code", "JetBrains Mono", Consolas, monospace',
    color: github.fgDefault,
  },
  handle: { width: 8, height: 8, border: 'none' },
  header: {
    display: 'flex',
    alignItems: 'center',
    gap: 8,
    height: 46,           // = CONTAINER_HEADER en mapToReactFlow: los hijos empiezan debajo
    padding: '0 12px',
    borderBottom: `1px solid ${github.borderDefault}`,
    boxSizing: 'border-box',
  },
  dot: { width: 8, height: 8, borderRadius: '50%', flexShrink: 0 },
  headerText: { display: 'flex', flexDirection: 'column', overflow: 'hidden', flex: 1 },
  name: { fontSize: 14, fontWeight: 700, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' },
  tech: { fontSize: 11, fontWeight: 600, marginTop: 1 },
  collapse: {
    background: 'transparent',
    border: '1px solid',
    borderRadius: 20,
    fontFamily: 'inherit',
    fontSize: 12,
    fontWeight: 700,
    lineHeight: 1,
    width: 22,
    height: 22,
    cursor: 'pointer',
    flexShrink: 0,
  },
};
