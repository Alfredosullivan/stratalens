import { Link } from 'react-router-dom';
import { useProjects } from '../hooks/useProjects';
import { HEADER_HEIGHT } from '../layout/constants';
import { github } from '../theme/githubDark';

// Landing tras login (T28): lista los proyectos propios y da entrada al flujo de
// conectar uno nuevo (T29 → /projects/new).
export function DashboardPage() {
  const state = useProjects();

  return (
    <div style={styles.root}>
      <header style={styles.header}>
        <span style={styles.title}>Stratalens</span>
        <Link to="/projects/new" style={styles.newButton}>
          + Nuevo proyecto
        </Link>
      </header>

      <main style={styles.main}>
        {state.status === 'loading' && <p style={styles.muted}>Cargando proyectos…</p>}
        {state.status === 'error' && <p style={styles.error}>Error: {state.message}</p>}

        {state.status === 'ready' && state.projects.length === 0 && (
          <p style={styles.muted}>
            Todavía no tenés proyectos. Conectá un repo de GitHub para analizarlo.
          </p>
        )}

        {state.status === 'ready' && state.projects.length > 0 && (
          <ul style={styles.list}>
            {state.projects.map((project) => (
              <li key={project.id}>
                <Link to={`/projects/${project.id}`} style={styles.item}>
                  <span style={styles.itemName}>{project.name}</span>
                  <span style={styles.itemMeta}>
                    {project.repositoryOwner
                      ? `${project.repositoryOwner}/${project.repositoryName}`
                      : 'proyecto manual'}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </main>
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  root: { width: '100vw', height: '100vh', background: github.canvasDefault },
  header: {
    height: HEADER_HEIGHT,
    boxSizing: 'border-box',
    display: 'flex',
    alignItems: 'center',
    gap: 14,
    padding: '0 18px',
    background: github.canvasSubtle,
    borderBottom: `1px solid ${github.borderDefault}`,
    color: github.fgDefault,
    fontFamily: 'system-ui, sans-serif',
  },
  title: { fontWeight: 700, fontSize: 15 },
  newButton: {
    marginLeft: 'auto',
    padding: '6px 12px',
    borderRadius: 6,
    background: github.accent,
    color: '#0d1117',
    fontWeight: 600,
    fontSize: 12.5,
    textDecoration: 'none',
  },
  main: { padding: '28px 24px', fontFamily: 'system-ui, sans-serif', maxWidth: 640 },
  muted: { color: github.fgMuted, fontSize: 13.5 },
  error: { color: github.danger, fontSize: 13.5 },
  list: { listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: 8 },
  item: {
    display: 'flex',
    flexDirection: 'column',
    gap: 3,
    padding: '12px 14px',
    borderRadius: 8,
    border: `1px solid ${github.borderDefault}`,
    background: github.canvasSubtle,
    textDecoration: 'none',
  },
  itemName: { color: github.fgDefault, fontWeight: 600, fontSize: 14 },
  itemMeta: { color: github.fgMuted, fontSize: 12, fontFamily: 'ui-monospace, monospace' },
};
