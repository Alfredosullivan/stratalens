using System.Security.Cryptography;
using System.Text;
using Stratalens.Application.Abstractions;

namespace Stratalens.Infrastructure.Security;

// Implementa la generación y el hash de claves de ingesta.
// - Generación: 32 bytes aleatorios criptográficos (RandomNumberGenerator) → 256 bits
//   de entropía, imposible de adivinar. Prefijo "om_ing_" para identificar el tipo de clave.
// - Hash: SHA-256 DETERMINISTA, no bcrypt. Razón: la clave es un token aleatorio de alta
//   entropía (no una contraseña débil que haya que proteger con salt+coste alto), necesitamos
//   BUSCAR el proyecto por el hash (bcrypt lleva salt por-registro → no indexable) y se valida
//   en cada request de ingesta (SHA-256 es rápido; bcrypt sería un lastre innecesario).
public class IngestKeyService : IIngestKeyService
{
    private const string Prefix = "om_ing_";

    public GeneratedIngestKey Generate()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);

        // Base64 url-safe sin padding: seguro para viajar en un header HTTP.
        var token = Convert.ToBase64String(randomBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var plainKey = Prefix + token;
        return new GeneratedIngestKey(plainKey, Hash(plainKey));
    }

    public string Hash(string plainKey)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainKey));
        return Convert.ToHexString(hashBytes); // 64 chars hex, determinista
    }
}
