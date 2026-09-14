using Stratalens.Domain.Entities;

namespace Stratalens.Application.Models;

// Resultado parcial que produce UN analyzer: los nodos y edges que detectó
// en su stack (ej. el analyzer de C# detecta Controllers → Services).
public record AnalysisResult(IReadOnlyList<Node> Nodes, IReadOnlyList<Edge> Edges);

// Grafo COMPLETO y persistido de un proyecto, tal como se recupera para mostrarlo.
// Es estructuralmente igual a AnalysisResult a propósito distinto: uno es la salida
// de un analyzer (parcial, en memoria), el otro es el grafo total ya guardado.
// Mantenerlos separados preserva el "ubiquitous language": el nombre dice qué es.
public record ProjectGraph(IReadOnlyList<Node> Nodes, IReadOnlyList<Edge> Edges);
