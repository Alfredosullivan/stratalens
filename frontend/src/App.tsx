import { BrowserRouter, Routes, Route, useParams } from 'react-router-dom';
import { LoginPage } from './pages/LoginPage';
import { DashboardPage } from './pages/DashboardPage';
import { NewProjectPage } from './pages/NewProjectPage';
import { GraphPage } from './pages/GraphPage';
import { RequireAuth } from './routes/RequireAuth';

// Traduce el :id de la ruta a la prop que espera GraphPage. GraphPage en sí no conoce
// React Router (recibe projectId como prop plano) — así /demo puede reusarla pasando
// null directo, sin fingir un id de ruta.
function GraphPageRoute() {
  const { id } = useParams<{ id: string }>();
  return <GraphPage projectId={id ?? null} />;
}

// Punto de entrada de la UI (T29): login → dashboard de mis proyectos → elegir repo →
// mapa del proyecto. /login y /demo quedan fuera de RequireAuth: /login porque es
// justamente donde termina un usuario SIN sesión, y /demo porque no necesita backend.
function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/demo" element={<GraphPage projectId={null} />} />

        <Route element={<RequireAuth />}>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/projects/new" element={<NewProjectPage />} />
          <Route path="/projects/:id" element={<GraphPageRoute />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}

export default App;
