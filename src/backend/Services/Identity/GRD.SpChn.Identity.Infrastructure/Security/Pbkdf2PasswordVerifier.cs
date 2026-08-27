using System.Security.Cryptography;
using GRD.SpChn.Identity.Application.Abstractions;

namespace GRD.SpChn.Identity.Infrastructure.Security;

internal sealed class Pbkdf2PasswordVerifier : IPasswordVerifier
{
    public bool Verify(string password, string encodedHash)
    {
        try
        {
            var parts = encodedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4 ||
                !string.Equals(parts[0], "pbkdf2-sha256", StringComparison.Ordinal) ||
                !int.TryParse(parts[1], out var iterations) ||
                iterations < 100_000)
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
