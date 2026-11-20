using System.Security.Cryptography;

namespace RaceDay.Api.Services;

public interface IPasswordHasher
{
    /// <summary>Returns a salted, one-way hash suitable for storage.</summary>
    string Hash(string password);

    /// <summary>Checks a plain-text password against a stored hash in constant time.</summary>
    bool Verify(string password, string storedHash);
}

/// <summary>
/// PBKDF2 (HMAC-SHA256) password hashing with a unique random salt per password.
/// Stored format: PBKDF2$iterations$salt(base64)$hash(base64) - well under the 255 characters of Users.PasswordHash.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Prefix = "PBKDF2";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 210_000;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
