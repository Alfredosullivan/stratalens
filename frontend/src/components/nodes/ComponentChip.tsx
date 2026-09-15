import type { NodeProps, Node } from '@xyflow/react';
import type { GraphFlowNodeData } from '../../graph/mapToReactFlow';
import { github } from '../../theme/githubDark';

type ComponentChipType = Node<GraphFlowNodeData, 'component'>;

// Chip minimalista de un componente interno (T35): un hijo dentro de un contenedor (ej. un
// Controller dentro del Backend expandido). Mucho más compacto que TerminalNode — solo un
// punto de color + el nombre — para que muchos hijos (13 controllers) se lean como bloques
// compactos en grid, no como una lista enorme. Sin handles: los hijos no tienen edges hoy.
// Sigue siendo clicable: onNodeClick lo selecciona y abre el panel de detalle (T13).
export function ComponentChip({ data }: NodeProps<ComponentChipType>) {
  const { graphNode, color } = data;

  return (
    <div
      style={{ ...styles.chip, borderColor: color }}
      title={`${graphNode.name} · ${graphNode.type}`}
    >
      <span style={{ ...styles.dot, background: color, boxShadow: `0 0 5px ${color}` }} />
      <span style={styles.name}>{graphNode.name}</span>
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  chip: {
    display: 'flex',
    alignItems: 'center',
    gap: 7,
    // Dimensiones fijas = CHIP_W/CHIP_H en mapToReactFlow (border-box para que el borde no
    // sume al tamaño y descuadre el grid).
    width: 150,
    height: 34,
    boxSizing: 'border-box',
    padding: '0 10px',
    background: github.canvasSubtle,
    border: '1px solid',
    borderRadius: 7,
    fontFamily: 'ui-monospace, "Cascadia Code", "JetBrains Mono", Consolas, monospace',
    color: github.fgDefault,
    cursor: 'pointer',
  },
  dot: { width: 7, height: 7, borderRadius: '50%', flexShrink: 0 },
  name: { fontSize: 12, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' },
};
