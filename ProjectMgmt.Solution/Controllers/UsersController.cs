using Microsoft.AspNetCore.Mvc;
using ProjectMgmt.IdentityAccess.Contracts;

namespace ProjectMgmt.Solution.Controllers;

[Route("api/users")]
public class UsersController : ApiControllerBase
{
    private readonly IUserLookupService _users;
    private readonly IUserSkillService _skills;

    public UsersController(IUserLookupService users, IUserSkillService skills)
    {
        _users = users;
        _skills = skills;
    }

    [HttpGet("{userId:guid}/display")]
    public async Task<ActionResult<UserDisplayInfo>> GetDisplay(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _users.GetDisplayInfoAsync(userId, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost("skills/query")]
    public Task<IReadOnlyList<UserSkillInfo>> GetSkills(
        [FromBody] IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken) =>
        _skills.GetUserSkillsAsync(userIds, cancellationToken);
}
