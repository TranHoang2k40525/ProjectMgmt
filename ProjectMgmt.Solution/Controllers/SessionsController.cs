using System.Security.Claims;
using IdentityExperience.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectMgmt.IdentityAccess.Contracts;

namespace ProjectMgmt.Solution.Controllers;

[Route("api/users/me/sessions")]
[Route("api/v1/users/me/sessions")]
public sealed class SessionsController(IdentityExperienceDbContext identity, ICurrentUserContext currentUser) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSessions()
    {
        var userId = currentUser.UserId;
        if (!userId.HasValue) return Unauthorized();
        var sessionHash = User.FindFirstValue("session_hash");
        var tokens = await identity.RefreshTokens.AsNoTracking().Where(t => t.UserId == userId && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow).OrderByDescending(t => t.CreatedAt).ToListAsync();
        return Ok(tokens.Select(t => new { t.Id, device = "computer", deviceName = string.IsNullOrEmpty(t.UserAgent) ? "Thiết bị không xác định" : t.UserAgent, browser = t.UserAgent ?? "", ipAddress = t.CreatedByIp ?? "", location = "", lastActive = t.CreatedAt, isCurrent = t.TokenHash == sessionHash }));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var userId = currentUser.UserId;
        if (!userId.HasValue) return Unauthorized();
        // Conditional update is atomic with refresh rotation: a rotated parent is never a live session.
        var token = await identity.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (token is null) return NotFound();
        var changed = await identity.RefreshTokens.Where(t => t.Id == id && t.UserId == userId && !t.IsRevoked).ExecuteUpdateAsync(setters => setters.SetProperty(t => t.IsRevoked, true));
        if (changed == 0) return Conflict(new { message = "Phiên đã thay đổi. Hãy tải lại danh sách phiên." });
        return Ok(new { success = true, isCurrent = token.TokenHash == User.FindFirstValue("session_hash") });
    }
}
