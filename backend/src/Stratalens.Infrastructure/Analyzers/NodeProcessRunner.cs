using System.Diagnostics;
using System.Text.Json;

namespace Stratalens.Infrastructure.Analyzers;

// Implementación real del seam: lanza `node analyze.mjs`, le envía los archivos por stdin
// (JSON) y devuelve su stdout. Es la ÚNICA clase acoplada al sistema operativo (proceso
// externo); por eso vive detrás de INodeAnalyzerRunner y no se mezcla con el mapeo.
public class NodeProcessRunner : INodeAnalyzerRunner
{
    // camelCase para que el JSON coincida con lo que espera/produce el analyzer Node
    // (files[].path, files[].content).
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly string _scriptPath;
    private readonly string _nodeExecutable;

    public NodeProcessRunner(string scriptPath, string nodeExecutable = "node")
    {
        _scriptPath = scriptPath;
        _nodeExecutable = nodeExecutable;
    }

    public async Task<string> RunAsync(IReadOnlyList<NodeAnalyzerFile> files, CancellationToken ct = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _nodeExecutable,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(_scriptPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("No se pudo iniciar el proceso de Node.");

        // Enviamos el input por stdin y cerramos para que Node sepa que terminó la entrada.
        var payload = JsonSerializer.Serialize(new { files }, JsonOptions);
        await process.StandardInput.WriteAsync(payload.AsMemory(), ct);
        process.StandardInput.Close();

        // Leemos stdout y stderr en paralelo para no bloquear si uno se llena.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"El analyzer Node terminó con código {process.ExitCode}: {stderr}");
        }

        return stdout;
    }
}
