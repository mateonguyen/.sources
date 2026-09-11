using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using ThucLuc.DbManager.Configuration;

namespace ThucLuc.DbManager.Services;

public interface IAdminCredentialValidator
{
    bool Validate(string userName, string password);
}

public sealed class AdminCredentialValidator(IOptionsMonitor<OpsOptions> options) : IAdminCredentialValidator
{
    public bool Validate(string userName, string password)
    {
        var authentication = options.CurrentValue.Authentication;
        if (!authentication.Enabled)
        {
            return true;
        }

        if (!string.Equals(userName, authentication.AdminUser, StringComparison.Ordinal)
            || string.IsNullOrEmpty(password)
            || !File.Exists(authentication.PasswordHashFile))
        {
            return false;
        }

        return AdminPasswordHasher.Verify(password, File.ReadAllText(authentication.PasswordHashFile).Trim());
    }
}

public static class AdminPasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 10)
        {
            throw new InvalidOperationException("Mật khẩu quản trị phải có ít nhất 10 ký tự.");
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string encoded)
    {
        try
        {
            var parts = encoded.Split('$');
            if (parts.Length != 4
                || !string.Equals(parts[0], "pbkdf2-sha256", StringComparison.Ordinal)
                || !int.TryParse(parts[1], out var iterations)
                || iterations < 100_000)
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
