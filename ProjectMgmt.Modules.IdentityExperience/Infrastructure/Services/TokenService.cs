using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using IdentityExperience.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IdentityExperience.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly byte[] _signingKey;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        _signingKey = Encoding.UTF8.GetBytes(_options.SigningKey);

        if (string.IsNullOrWhiteSpace(_options.Issuer)
            || string.IsNullOrWhiteSpace(_options.Audience)
            || _signingKey.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT is not configured. Issuer, Audience and a SigningKey of at least 32 UTF-8 bytes are required.");
        }

        if (_options.AccessTokenMinutes is < 1 or > 60 || _options.RefreshTokenDays is < 1 or > 90)
        {
            throw new InvalidOperationException("JWT token lifetimes are outside the permitted range.");
        }
    }

    public TokenSet CreateTokenSet(
        User user,
        UserProfile? profile,
        IReadOnlyCollection<string> systemRoles,
        DateTime nowUtc)
    {
        var accessTokenExpiresAt = nowUtc.AddMinutes(_options.AccessTokenMinutes);
        var refreshTokenExpiresAt = nowUtc.AddDays(_options.RefreshTokenDays);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, profile?.DisplayName ?? user.Email),
            new("security_stamp", user.SecurityStamp.ToString())
        };

        claims.AddRange(systemRoles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(_signingKey),
            SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: nowUtc,
            expires: accessTokenExpiresAt,
            signingCredentials: credentials);

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return new TokenSet
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(jwt),
            RefreshToken = refreshToken,
            RefreshTokenHash = HashRefreshToken(refreshToken),
            AccessTokenExpiresAt = accessTokenExpiresAt,
            RefreshTokenExpiresAt = refreshTokenExpiresAt
        };
    }

    public string HashRefreshToken(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
    }
}
