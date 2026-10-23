using IdentityExperience.Application.Dto;
using IdentityExperience.Domain.Entities;

namespace IdentityExperience.Application.IServices;

public interface ITokenService
{
    TokenSet CreateTokenSet(
        User user,
        UserProfile? profile,
        IReadOnlyCollection<string> systemRoles,
        DateTime nowUtc);

    string HashRefreshToken(string refreshToken);
}
