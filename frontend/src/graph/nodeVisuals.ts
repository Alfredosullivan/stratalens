import type { GraphNode } from './types';
import { github } from '../theme/githubDark';

// Color de identidad de cada nodo (borde, glow, acentos). Paleta = tokens reales de
// GitHub Dark (theme/githubDark.ts): Frontend accent-azul, Backend done-púrpura,
// Database success-verde, infraestructura/DevOps/CI attention-amarillo.
//
// Se resuelve primero por TYPE (la tecnología concreta: "React", "AspNetCore",
// "PostgreSQL"...) porque Category sola no alcanza — Frontend y Backend son ambos
// NodeCategory.Application, así que por categoría no se pueden distinguir. Mismo patrón
// que TECH_LABELS en NodeDetailPanel: mapa por Type conocido + fallback genérico.
const TYPE_COLORS: Record<string, string> = {
  React: github.accent,       // Frontend
  AspNetCore: github.done,    // Backend
  PostgreSQL: github.success, // Database
  Docker: github.attention,   // Infraestructura (T30)
  JWT: github.sponsors,       // Security (T32)
  MessageBus: github.severe,  // Message Bus (T36): naranja, distinto de Docker
  Workers: github.teal,       // Workers (T37): teal
  Cloud: github.sky,          // Cloud AWS/Azure/GCP (T38): azul cielo
};

// Fallback por categoría para Types aún no mapeados arriba. Para categorías sin nodos
// reales todavía (External, Code, Deployment) se usa el gris neutro de GitHub — "sin
// diseñar todavía" es más honesto que inventar un color no aprobado.
const CATEGORY_FALLBACK_COLORS: Record<string, string> = {
  Database: github.success,
  Infrastructure: github.attention,
  DevOps: github.attention,
  Deployment: github.attention,
  Security: github.sponsors, // rosa propio: un nodo de auth no debe verse igual que Docker
};

export function resolveNodeColor(node: GraphNode): string {
  return TYPE_COLORS[node.type] ?? CATEGORY_FALLBACK_COLORS[node.category] ?? github.fgMuted;
}
