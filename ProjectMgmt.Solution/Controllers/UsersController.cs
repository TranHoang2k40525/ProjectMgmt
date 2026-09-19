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

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDisplayInfo>>> GetAllUsers(CancellationToken cancellationToken)
    {
        var sampleIds = new[]
        {
            Guid.Parse("11111111-0000-0000-0000-000000000001"),
            Guid.Parse("11111111-0000-0000-0000-000000000002"),
            Guid.Parse("11111111-0000-0000-0000-000000000003")
        };
        var users = await _users.GetDisplayInfoAsync(sampleIds, cancellationToken);
        if (users.Count == 0)
        {
            users = new List<UserDisplayInfo>
            {
                new UserDisplayInfo(sampleIds[0], "Hoàng Admin (System Lead)", null),
                new UserDisplayInfo(sampleIds[1], "Nguyễn Văn Dev (Technical Lead)", null),
                new UserDisplayInfo(sampleIds[2], "Trần Thị Scrum (Scrum Master)", null)
            };
        }
        return Ok(users);
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
