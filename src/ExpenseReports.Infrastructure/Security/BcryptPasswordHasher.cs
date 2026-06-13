using ExpenseReports.Application.Abstractions;

namespace ExpenseReports.Infrastructure.Security;

// BCrypt was chosen over a raw hash (SHA-256) because it is deliberately slow and
// salts each hash automatically, which is what makes offline brute-forcing of a
// leaked database impractical. The salt is stored inside the resulting hash
// string, so Verify needs nothing else to check a password.
internal sealed class BcryptPasswordHasher : IPasswordHasher
{
    // Work factor 12 = 2^12 rounds: a common balance between login latency and
    // resistance to brute force. Raising it makes both hashing and attacks slower.
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash) =>
        BCrypt.Net.BCrypt.Verify(password, passwordHash);
}
