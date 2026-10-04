using System.Security.Cryptography;
using System.Text;
using IdentityExperience.Application.IServices;
using Microsoft.Extensions.Configuration;

namespace IdentityExperience.Infrastructure.Services;

public class OtpCodeService : IOtpCodeService
{
    private const int OtpLowerBound = 100_000;
    private const int OtpUpperBound = 1_000_000;
    private readonly byte[] _hashKey;

    public OtpCodeService(IConfiguration configuration)
    {
        var hashKey = configuration["Otp:HashKey"];
        if (string.IsNullOrWhiteSpace(hashKey) || Encoding.UTF8.GetByteCount(hashKey) < 32)
        {
            throw new InvalidOperationException(
                "Missing or weak Otp:HashKey. Configure at least 32 UTF-8 bytes through User Secrets or Otp__HashKey.");
        }

        _hashKey = Encoding.UTF8.GetBytes(hashKey);
    }

    public string GenerateCode()
    {
        return RandomNumberGenerator
            .GetInt32(OtpLowerBound, OtpUpperBound)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public string Hash(string normalizedEmail, string purpose, string code)
    {
        var payload = Encoding.UTF8.GetBytes(BuildPayload(normalizedEmail, purpose, code));
        return Convert.ToHexString(HMACSHA256.HashData(_hashKey, payload));
    }

    public bool Verify(string normalizedEmail, string purpose, string code, string codeHash)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(codeHash))
        {
            return false;
        }

        try
        {
            var expectedHash = Convert.FromHexString(Hash(normalizedEmail, purpose, code));
            var storedHash = Convert.FromHexString(codeHash);
            return CryptographicOperations.FixedTimeEquals(expectedHash, storedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string BuildPayload(string normalizedEmail, string purpose, string code)
    {
        return $"{normalizedEmail.Trim().ToUpperInvariant()}:{purpose.Trim()}:{code.Trim()}";
    }
}
