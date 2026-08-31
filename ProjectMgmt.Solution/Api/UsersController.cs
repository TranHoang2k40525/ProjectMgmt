using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.IdentityAccess.Contracts;

namespace ProjectMgmt.Solution.Api;

[Route("api/users")]
public sealed class UsersController(IUserLookupService users, IUserSkillService skills) : ApiControllerBase
{
    [HttpGet("{userId:guid}/display")]
    public async Task<ActionResult<UserDisplayInfo>> GetDisplay(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await users.GetDisplayInfoAsync(userId, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost("skills/query")]
    public Task<IReadOnlyList<UserSkillInfo>> GetSkills(
        [FromBody] IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken) =>
        skills.GetUserSkillsAsync(userIds, cancellationToken);
}
