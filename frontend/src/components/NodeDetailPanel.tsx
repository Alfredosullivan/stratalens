import type { GraphNode } from '../graph/types';
import { HEADER_HEIGHT } from '../layout/constants';
import { github } from '../theme/githubDark';

interface NodeDetailPanelProps {
  node: GraphNode;
  onClose: () => void;
}

// Etiquetas legibles para los Type técnicos que produce el backend. Si un type no está
// mapeado, se muestra tal cual (agnóstico al stack, como el resto del sistema).
const TECH_LABELS: Record<string, string> = {
  AspNetCore: 'ASP.NET Core',
  React: 'React',
  PostgreSQL: 'PostgreSQL',
  MessageBus: 'Message Bus',
};

// Etiquetas para claves de metadata conocidas (el resto se muestra con su clave cruda).
const META_LABELS: Record<string, string> = {
  source: 'Archivo de origen',
  language: 'Lenguaje',
  framework: 'Framework',
};

// Panel lateral con el detalle del nodo seleccionado: tecnología, categoría y su Source
// (de qué archivo se detectó) — cumpliendo la regla de producto de que todo elemento del
// mapa debe poder explicar de dónde salió.
export function NodeDetailPanel({ node, onClose }: NodeDetailPanelProps) {
  const tech = TECH_LABELS[node.type] ?? node.type;

  // Separamos el "source" (destacado) del resto de metadata.
  const otherMetadata = Object.entries(node.metadata).filter(([key]) => key !== 'source');

  return (
    <aside style={styles.panel}>
      <header style={styles.header}>
        <span style={styles.name}>{node.name}</span>
        <button style={styles.close} onClick={onClose} aria-label="Cerrar">
          ×
        </button>
      </header>

      <dl style={styles.list}>
        <Field label="Tecnología" value={tech} />
        <Field label="Categoría" value={node.category} />
        {node.metadata.source && <Field label="Archivo de origen" value={node.metadata.source} mono />}
      </dl>

      {otherMetadata.length > 0 && (
        <>
          <div style={styles.sectionTitle}>Metadata</div>
          <dl style={styles.list}>
            {otherMetadata.map(([key, value]) => (
              <Field key={key} label={META_LABELS[key] ?? key} value={value} mono />
            ))}
          </dl>
        </>
      )}
    </aside>
  );
}

// Fila etiqueta/valor del panel.
function Field({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div style={styles.field}>
      <dt style={styles.label}>{label}</dt>
      <dd style={{ ...styles.value, ...(mono ? styles.mono : null) }}>{value}</dd>
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  panel: {
    position: 'absolute',
    // Empieza DEBAJO del header (antes: top:0 + padding-top:58px "a ojo" — se
    // desincronizaba si el header cambiaba de tamaño; ver layout/constants.ts).
    top: HEADER_HEIGHT,
    right: 0,
    zIndex: 6,
    width: 320,
    height: `calc(100% - ${HEADER_HEIGHT}px)`,
    background: github.canvasSubtle,
    borderLeft: `1px solid ${github.borderDefault}`,
    padding: '20px',
    color: github.fgDefault,
    fontFamily: 'system-ui, sans-serif',
    overflowY: 'auto',
    boxShadow: '-8px 0 24px rgba(0,0,0,0.3)',
  },
  header: { display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 18 },
  name: { fontSize: 18, fontWeight: 700 },
  close: {
    background: 'transparent',
    border: `1px solid ${github.borderDefault}`,
    borderRadius: 6,
    color: github.fgMuted,
    fontSize: 18,
    lineHeight: 1,
    width: 28,
    height: 28,
    cursor: 'pointer',
  },
  list: { margin: 0 },
  field: { marginBottom: 14 },
  label: { fontSize: 11, textTransform: 'uppercase', letterSpacing: 0.5, color: github.fgMuted, marginBottom: 3 },
  value: { fontSize: 14, margin: 0, wordBreak: 'break-all' },
  mono: { fontFamily: 'Consolas, monospace', fontSize: 12.5, color: github.accent },
  sectionTitle: {
    fontSize: 11,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    color: github.fgMuted,
    borderTop: `1px solid ${github.borderDefault}`,
    paddingTop: 16,
    marginTop: 8,
    marginBottom: 12,
  },
};
