import type { ProjectGraph } from './types';
import type { LiveTraceEvent } from '../live/types';

// Resuelve el recorrido de un trace en vivo a los edges del grafo que lo componen.
// Función PURA (sin estado ni efectos): fácil de razonar y testear, igual que mapToReactFlow.
//
// Cada hop del evento trae SourceNode/TargetNode como strings de OTel (ej. "Backend",
// "postgresql"). Se resuelven al id del nodo con la MISMA convención Opción A del backend
// (match exacto case-insensitive contra name, type o el alias metadata "otelName"). Si un
// extremo no casa con ningún nodo, ese hop se ignora (nunca resaltamos un edge inventado).
export function resolveTracePath(graph: ProjectGraph, event: LiveTraceEvent): Set<string> {
  const nodeIdByKey = buildNodeIndex(graph);
  const edgeIds = new Set<string>();

  for (const hop of event.hops) {
    if (!hop.targetNode) continue; // sin destino no hay relación entre dos nodos

    const sourceId = nodeIdByKey.get(normalize(hop.sourceNode));
    const targetId = nodeIdByKey.get(normalize(hop.targetNode));
    if (!sourceId || !targetId) continue; // extremo sin match → no resaltar

    // Buscamos el edge del grafo que une ese par (en la dirección del hop).
    const edge = graph.edges.find((e) => e.source === sourceId && e.target === targetId);
    if (edge) edgeIds.add(edge.id);
  }

  return edgeIds;
}

// Índice de nodos por sus claves de resolución (name, type, alias otelName), normalizadas.
// Si dos nodos comparten clave, gana el primero (no resolvemos a un match ambiguo).
function buildNodeIndex(graph: ProjectGraph): Map<string, string> {
  const index = new Map<string, string>();

  const add = (key: string | undefined, id: string) => {
    if (!key) return;
    const k = normalize(key);
    if (!index.has(k)) index.set(k, id);
  };

  for (const node of graph.nodes) {
    add(node.name, node.id);
    add(node.type, node.id);
    add(node.metadata?.otelName, node.id);
  }

  return index;
}

const normalize = (value: string): string => value.trim().toLowerCase();
