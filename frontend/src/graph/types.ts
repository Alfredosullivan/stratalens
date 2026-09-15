// Tipos que reflejan el contrato del backend (GraphResponse de T11). Se mantienen
// separados de los tipos de React Flow: el DTO del API es una cosa, la representación
// visual es otra. La conversión entre ambos vive en mapToReactFlow.

export interface GraphNode {
  id: string;
  name: string;
  type: string;      // ej. "React", "AspNetCore", "PostgreSQL"
  category: string;  // ej. "Application", "Database"
  metadata: Record<string, string>;
  // Jerarquía (T33): id del nodo padre, o null/ausente si es raíz. Lo usará el render
  // anidado de React Flow (T35). Null para todos los nodos hoy — nada asigna padre hasta T34.
  parentNodeId?: string | null;
}

export interface GraphEdge {
  id: string;
  source: string;      // id del nodo origen
  target: string;      // id del nodo destino
  type: string;        // ej. "HTTP/REST", "SQL"
  sourceFile: string;  // archivo/config de donde se dedujo (evidencia)
  confidence: number;  // 0..100
  sourceType: string;  // "Static" | "Runtime"
}

export interface ProjectGraph {
  nodes: GraphNode[];
  edges: GraphEdge[];
}
