// Paleta única de la app: tokens REALES del tema oscuro de GitHub (Primer Design System),
// no colores inventados. Antes había ~10 hex ligeramente distintos de "gris azulado" y
// "verde/teal" repartidos en 8 archivos — lo opuesto a minimalista. Centralizar aquí sigue
// la misma idea que layout/constants.ts: un valor que se repite en varios sitios vive en
// UN solo lugar de verdad, así que cambiarlo (o corregirlo) no exige tocar 8 archivos.
//
// Nombres = los tokens oficiales de Primer (canvas-default, fg-muted, accent, success...),
// para poder buscar "cómo se ve esto en GitHub.com" y encontrar el mismo nombre.
export const github = {
  canvasDefault: '#0d1117', // fondo de página/canvas
  canvasSubtle: '#161b22',  // superficie elevada: paneles, header, tarjetas de nodo
  borderDefault: '#30363d', // bordes y separadores
  fgDefault: '#e6edf3',     // texto principal
  fgMuted: '#8b949e',       // texto secundario, líneas estáticas/inferidas, neutro sin diseñar

  accent: '#58a6ff',    // Frontend (React)
  done: '#a371f7',      // Backend (AspNetCore)
  success: '#3fb950',   // Database (PostgreSQL) + edges runtime confirmados
  attention: '#d29922', // Infraestructura/DevOps/Docker + resalte de trace en vivo
  sponsors: '#db61a2',  // Security (JWT/auth): rosa distinto de los 5 anteriores, no choca
  severe: '#db6d28',    // Message Bus (RabbitMQ/Kafka): naranja, distinto del amarillo Docker
  teal: '#39c5cf',      // Workers (background jobs): teal, distinto de los 6 anteriores
  sky: '#79c0ff',       // Cloud (AWS/Azure/GCP): azul cielo, más claro que el accent del Frontend
  danger: '#f85149',    // reservado: futuro estado ERROR de un trace (no usado todavía)
} as const;
