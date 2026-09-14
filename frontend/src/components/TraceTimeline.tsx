import type { SpanDto, TraceDto } from '../live/traceTypes';
import { github } from '../theme/githubDark';

interface TraceTimelineProps {
  trace: TraceDto | null;
}

// Panel waterfall de una request (T23): cada span es una barra cuya POSICIÓN horizontal
// es su inicio relativo al arranque del trace y cuyo ANCHO es su duración, ambos como
// fracción de la duración total. Así se ve, proporcional, que el span raíz (backend)
// abarca toda la request y el hijo (query a la DB) es un tramo dentro de él.
export function TraceTimeline({ trace }: TraceTimelineProps) {
  if (!trace) {
    return (
      <div style={styles.panel}>
        <div style={styles.empty}>Esperando un trace… (haz una request al demo)</div>
      </div>
    );
  }

  // Orden temporal para pintar el recorrido de arriba abajo.
  const spans = [...trace.spans].sort((a, b) => Date.parse(a.startedAt) - Date.parse(b.startedAt));
  const traceStart = spans.length > 0 ? Date.parse(spans[0].startedAt) : 0;
  const total = trace.durationMs > 0 ? trace.durationMs : 1; // evita división por cero

  return (
    <div style={styles.panel}>
      <header style={styles.header}>
        <span style={styles.title}>⚡ Timeline de la request</span>
        <span style={styles.total}>
          total <strong>{trace.durationMs.toFixed(1)} ms</strong> · {trace.traceId.slice(0, 8)}…
        </span>
      </header>

      <div style={styles.rows}>
        {spans.map((span) => {
          const offsetMs = Date.parse(span.startedAt) - traceStart;
          const leftPct = clampPct((offsetMs / total) * 100);
          // Ancho mínimo 1.5% (visible) y máximo lo que quede hasta el 100% (no desborda).
          const widthPct = Math.max(1.5, Math.min((span.durationMs / total) * 100, 100 - leftPct));
          const isRoot = span.parentSpanId === null;

          return (
            <div key={span.spanId} style={styles.row}>
              <span style={styles.label} title={describe(span)}>
                {describe(span)}
              </span>
              <div style={styles.track}>
                <div
                  style={{
                    ...styles.bar,
                    left: `${leftPct}%`,
                    width: `${widthPct}%`,
                    // Raíz (backend) mismo color que el nodo Backend; hijo mismo color que Database.
                    background: isRoot ? github.done : github.success,
                  }}
                />
              </div>
              {/* La duración va FUERA de la barra: así no desborda en spans muy cortos. */}
              <span style={styles.rowMs}>{span.durationMs.toFixed(1)} ms</span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

// Etiqueta legible del span: "origen → destino: operación" (o solo "origen: operación").
function describe(span: SpanDto): string {
  return span.targetNode
    ? `${span.sourceNode} → ${span.targetNode}: ${span.operation}`
    : `${span.sourceNode}: ${span.operation}`;
}

const clampPct = (value: number, min = 0): number => Math.max(min, Math.min(100, value));

const styles: Record<string, React.CSSProperties> = {
  panel: {
    position: 'absolute',
    bottom: 0,
    left: 0,
    right: 0,
    zIndex: 5,
    height: 190,
    background: github.canvasSubtle,
    borderTop: `1px solid ${github.borderDefault}`,
    padding: '12px 18px',
    color: github.fgDefault,
    fontFamily: 'system-ui, sans-serif',
    boxSizing: 'border-box',
    overflowY: 'auto',
    overflowX: 'hidden', // el waterfall nunca provoca scroll horizontal
  },
  header: { display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', marginBottom: 12 },
  title: { fontWeight: 700, fontSize: 13 },
  total: { color: github.fgMuted, fontSize: 12 },
  empty: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    height: '100%',
    color: github.fgMuted,
    fontSize: 13,
  },
  rows: { display: 'flex', flexDirection: 'column', gap: 8 },
  row: { display: 'flex', alignItems: 'center', gap: 12 },
  label: {
    width: 260,
    flexShrink: 0,
    fontSize: 12,
    color: github.fgMuted,
    whiteSpace: 'nowrap',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
  },
  track: {
    position: 'relative',
    flex: 1,
    minWidth: 0,        // permite que el flex item se encoja sin empujar la fila
    height: 22,
    background: github.canvasDefault,
    borderRadius: 4,
    overflow: 'hidden', // recorta cualquier barra que roce el borde
  },
  bar: { position: 'absolute', top: 0, height: 22, borderRadius: 4, minWidth: 2 },
  rowMs: {
    width: 64,
    flexShrink: 0,
    textAlign: 'right',
    fontSize: 11,
    color: github.fgMuted,
    fontVariantNumeric: 'tabular-nums',
  },
};
