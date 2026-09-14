import { Navigate, Outlet } from 'react-router-dom';
import { useCurrentUser } from '../hooks/useCurrentUser';
import { github } from '../theme/githubDark';

// Envuelve las rutas que requieren sesión. Mientras se resuelve /auth/me no renderiza el
// contenido protegido (evita el parpadeo de "dashboard vacío" antes de saber si hay
// sesión); si no la hay, redirige a /login en vez de dejar pasar un 401 crudo al resto
// de la app.
export function RequireAuth() {
  const user = useCurrentUser();

  if (user.status === 'loading') {
    return <div style={styles.loading}>Verificando sesión…</div>;
  }

  if (user.status === 'error') {
    return <Navigate to="/login" replace />;
  }

  return <Outlet />;
}

const styles: Record<string, React.CSSProperties> = {
  loading: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    height: '100vh',
    width: '100vw',
    background: github.canvasDefault,
    color: github.fgMuted,
    fontFamily: 'system-ui, sans-serif',
  },
};
