namespace Stratalens.Application.Abstractions;

// Cifra y descifra secretos (el access_token de GitHub) antes de persistirlos.
// Application define el contrato; Infrastructure lo implementa con la Data Protection
// API de ASP.NET Core. Así el resto del sistema nunca maneja las llaves ni el algoritmo.
public interface ITokenProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
