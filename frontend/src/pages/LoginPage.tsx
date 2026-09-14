import { loginUrl } from '../services/authService';
import { github } from '../theme/githubDark';

export function LoginPage() {
  return (
    <div style={styles.root}>
      <div style={styles.card}>
        <span style={styles.title}>stratalens</span>
        <p style={styles.subtitle}>Mapa de arquitectura y observabilidad de tus repos.</p>
        {/* <a> real, no un botón con onClick: el login es una navegación de página
            completa (Challenge de OAuth redirige a GitHub), no una llamada fetch. */}
        <a style={styles.button} href={loginUrl()}>
          Conectar con GitHub
        </a>
      </div>
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  root: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    height: '100vh',
    width: '100vw',
    background: github.canvasDefault,
    fontFamily: 'ui-monospace, "Cascadia Code", "JetBrains Mono", Consolas, monospace',
  },
  card: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'flex-start',
    gap: 14,
    padding: '32px 36px',
    background: github.canvasSubtle,
    border: `1px solid ${github.borderDefault}`,
    borderRadius: 8,
  },
  title: { fontSize: 22, fontWeight: 700, color: github.fgDefault, letterSpacing: 0.5 },
  subtitle: { color: github.fgMuted, fontSize: 13, margin: 0, maxWidth: 280 },
  button: {
    marginTop: 6,
    padding: '9px 16px',
    borderRadius: 6,
    background: github.accent,
    color: '#0d1117',
    fontWeight: 600,
    textDecoration: 'none',
    fontSize: 13,
    cursor: 'pointer',
  },
};
