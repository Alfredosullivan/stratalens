import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useRepositories } from '../hooks/useRepositories';
import { createProjectFromRepository } from '../services/projectsService';
import { ApiError } from '../services/api';
import { HEADER_HEIGHT } from '../layout/constants';
import { github } from '../theme/githubDark';

// Selector de repos (T25) que dispara crear+analizar (T26, síncrono) y navega al mapa
// resultante. Mientras dura la creación, el resto de la lista queda deshabilitada: el
// análisis puede tardar unos segundos y no tiene sentido dejar disparar dos a la vez.
export function NewProjectPage() {
  const state = useRepositories();
  const navigate = useNavigate();
  const [creating, setCreating] = useState<string | null>(null); // "owner/name" en curso
  const [error, setError] = useState<string | null>(null);

  async function handlePick(owner: string, name: string) {
    setError(null);
    setCreating(`${owner}/${name}`);

    try {
      const created = await createProjectFromRepository(owner, name);
      navigate(`/projects/${created.id}`);
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        setError(`Ya existe un proyecto para ${owner}/${name}.`);
      } else {
        setError(err instanceof Error ? err.message : 'Error desconocido al crear el proyecto.');
      }
      setCreating(null);
    }
  }

  return (
    <div style={styles.root}>
      <header style={styles.header}>
        <span style={styles.title}>Elegir repositorio</span>
      </header>

      <main style={styles.main}>
        {error && <p style={styles.error}>{error}</p>}
        {state.status === 'loading' && <p style={styles.muted}>Cargando repos…</p>}
        {state.status === 'error' && <p style={styles.error}>Error: {state.message}</p>}

        {state.status === 'ready' && state.repositories.length === 0 && (
          <p style={styles.muted}>No se encontraron repos propios en tu cuenta de GitHub.</p>
        )}

        {state.status === 'ready' && state.repositories.length > 0 && (
          <ul style={styles.list}>
            {state.repositories.map((repo) => {
              const key = `${repo.owner}/${repo.name}`;
              const isCreating = creating === key;
              return (
                <li key={key}>
                  <button
                    type="button"
                    style={styles.item}
                    disabled={creating !== null}
                    onClick={() => handlePick(repo.owner, repo.name)}
                  >
                    <span style={styles.itemName}>{key}</span>
                    <span style={styles.itemMeta}>
                      {isCreating ? 'Analizando…' : repo.isPrivate ? 'privado' : 'público'}
                    </span>
                  </button>
                </li>
              );
            })}
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
  main: { padding: '28px 24px', fontFamily: 'system-ui, sans-serif', maxWidth: 640 },
  muted: { color: github.fgMuted, fontSize: 13.5 },
  error: { color: github.danger, fontSize: 13.5, marginBottom: 12 },
  list: { listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: 8 },
  item: {
    width: '100%',
    display: 'flex',
    flexDirection: 'column',
    gap: 3,
    padding: '12px 14px',
    borderRadius: 8,
    border: `1px solid ${github.borderDefault}`,
    background: github.canvasSubtle,
    textAlign: 'left',
    cursor: 'pointer',
    font: 'inherit',
  },
  itemName: { color: github.fgDefault, fontWeight: 600, fontSize: 14 },
  itemMeta: { color: github.fgMuted, fontSize: 12, fontFamily: 'ui-monospace, monospace' },
};
