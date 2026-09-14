namespace Stratalens.Application.Abstractions;

// Genera y verifica claves de ingesta de telemetría (una API key por proyecto).
// La clave en claro solo existe en el instante de generarla; en reposo se guarda
// únicamente su hash. Application define el contrato; Infrastructure elige la criptografía.
public interface IIngestKeyService
{
    // Genera una clave nueva: devuelve el texto plano (se muestra UNA sola vez al usuario)
    // y su hash (lo único que se persiste).
    GeneratedIngestKey Generate();

    // Hashea una clave entrante para compararla contra el hash almacenado.
    // Determinista: la misma clave siempre da el mismo hash (permite buscar por él).
    string Hash(string plainKey);
}

// Resultado de generar una clave: el par (texto plano, hash) del mismo secreto.
public record GeneratedIngestKey(string PlainKey, string Hash);
