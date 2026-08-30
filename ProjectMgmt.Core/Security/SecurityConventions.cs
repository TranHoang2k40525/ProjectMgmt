using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ProjectMgmt.BuildingBlocks.Security;

public static class ClaimNames
{
    public const string UserId = "user_id";
    public const string SecurityStamp = "security_stamp";
}

public enum ScopeType
{
    System,
    Organization,
    Project
}

public static class PermissionCode
{
    public static bool IsValid(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var parts = code.Split('.', StringSplitOptions.TrimEntries);
        return parts.Length >= 2 && parts.All(part => part.Length > 0 && part.All(character =>
            char.IsLower(character) || char.IsDigit(character) || character == '-'));
    }
}

public interface ICurrentUser
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }
}

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId
    {
        get
        {
            var rawValue = Principal?.FindFirstValue(ClaimNames.UserId)
                ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(rawValue, out var userId) ? userId : null;
        }
    }
}
