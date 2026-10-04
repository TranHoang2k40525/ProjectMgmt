using IdentityExperience.Domain.Entities;

namespace IdentityExperience.Domain.Models;

public class AuthSessionContext
{
    public User User { get; set; } = new();
    public UserProfile? Profile { get; set; }
    public List<string> SystemRoles { get; set; } = [];
}

public enum LoginSessionStatus
{
    Created,
    UserNotFound,
    AccountDisabled,
    EmailNotVerified
}

public enum TokenRotationStatus
{
    Rotated,
    TokenNotFound,
    TokenExpired,
    TokenRevoked,
    ReuseDetected,
    AccountDisabled,
    EmailNotVerified
}
