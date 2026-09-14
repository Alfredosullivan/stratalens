import type { ProjectGraph } from './types';

// Grafo de ejemplo para el "modo demo" (cuando se abre la app sin ?projectId). Refleja
// exactamente lo que produce el pipeline del backend contra el repo demo: los 3 nodos
// gruesos y sus relaciones. Sirve para ver el canvas sin necesitar login + análisis real.
export const demoGraph: ProjectGraph = {
  nodes: [
    {
      id: 'n-frontend',
      name: 'Frontend',
      type: 'React',
      category: 'Application',
      metadata: { source: 'frontend/package.json', language: 'typescript' },
    },
    {
      id: 'n-backend',
      name: 'Backend',
      type: 'AspNetCore',
      category: 'Application',
      metadata: { source: 'backend/Demo.Api.csproj', language: 'csharp' },
    },
    {
      id: 'n-database',
      name: 'PostgreSQL',
      type: 'PostgreSQL',
      category: 'Database',
      metadata: { source: 'backend/Infrastructure/AppDbContext.cs' },
    },
  ],
  edges: [
    {
      id: 'e-front-back',
      source: 'n-frontend',
      target: 'n-backend',
      type: 'HTTP/REST',
      sourceFile: 'frontend/src/services/productService.ts',
      confidence: 80,
      sourceType: 'Static',
    },
    {
      // Este par ya fue CONFIRMADO por un trace real (promovido en T20): se ve distinto
      // del estático — sólido y teal, etiquetado "confirmado", no "inferido".
      id: 'e-back-db',
      source: 'n-backend',
      target: 'n-database',
      type: 'SQL',
      sourceFile: 'trace:0001…',
      confidence: 100,
      sourceType: 'Runtime',
    },
  ],
};
