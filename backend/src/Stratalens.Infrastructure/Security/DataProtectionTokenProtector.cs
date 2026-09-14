using Microsoft.AspNetCore.DataProtection;
using Stratalens.Application.Abstractions;

namespace Stratalens.Infrastructure.Security;

// Implementa ITokenProtector con la Data Protection API de ASP.NET Core.
// La API gestiona las llaves de cifrado (rotación, almacenamiento) por nosotros;
// nunca hardcodeamos ni manejamos llaves a mano.
public class DataProtectionTokenProtector : ITokenProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionTokenProtector(IDataProtectionProvider provider)
    {
        // El "purpose" aísla criptográficamente este uso: un token cifrado para este
        // propósito no puede descifrarse con un protector de otro propósito distinto.
        _protector = provider.CreateProtector("GitHubAccessToken.v1");
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
