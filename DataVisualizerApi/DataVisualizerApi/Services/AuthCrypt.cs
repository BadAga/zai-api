using System.Security.Cryptography;
using System.Text;

public interface IAuthCrypto
{
    byte[] ComputeEmailHash(string email);
    string HashPassword(string password);
    bool VerifyPassword(string password, string storedHash);
}

public sealed class AuthCrypto : IAuthCrypto
{
    private readonly string _publicSalt = "users:v1";
    private readonly int _iterations = 200_000;

    public byte[] ComputeEmailHash(string email)
    {
        var normalized = (email ?? "").Trim().ToLowerInvariant();
        var salt = Encoding.UTF8.GetBytes(_publicSalt);
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(normalized),
            salt,
            _iterations,
            HashAlgorithmName.SHA256,
            32
        );
    }

    public string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            _iterations,
            HashAlgorithmName.SHA256,
            32
        );

        return $"{_iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            var parts = storedHash.Split('.', 3);
            if (parts.Length != 3) return false;

            int iterations = int.Parse(parts[0]);
            byte[] salt = Convert.FromBase64String(parts[1]);
            byte[] expectedHash = Convert.FromBase64String(parts[2]);

            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                32
            );

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }
}
