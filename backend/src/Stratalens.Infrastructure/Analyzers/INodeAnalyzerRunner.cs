namespace Stratalens.Infrastructure.Analyzers;

// Seam sobre el subproceso Node. Aísla el "CÓMO se ejecuta Node y se lee su stdout" de
// la lógica de mapeo JSON → Node/Edge. Gracias a esta costura:
//   - El mapeo se testea sin Node instalado (inyectando un runner falso con JSON fijo).
//   - La estrategia de ejecución se puede cambiar (stdin hoy; temp dir + type-checker
//     en el futuro) sin tocar al ReactTypeScriptAnalyzer.
// Es el ÚNICO lugar del sistema que sabe cómo llegan los archivos al proceso Node.
public interface INodeAnalyzerRunner
{
    // Recibe los archivos del repo y devuelve el JSON crudo que produjo el analyzer Node.
    Task<string> RunAsync(IReadOnlyList<NodeAnalyzerFile> files, CancellationToken ct = default);
}

// Par archivo→contenido que se envía al subproceso Node. Vive aquí (no en Application)
// porque es un detalle del transporte hacia Node, no un concepto del dominio.
public record NodeAnalyzerFile(string Path, string Content);
