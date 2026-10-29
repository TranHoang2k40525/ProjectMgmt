using System.ComponentModel.DataAnnotations;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planning.Domain.Entities;
using Planning.Infrastructure;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.Solution.Security;

namespace ProjectMgmt.Solution.Controllers;

public sealed class CreatePokerSessionRequest
{
    [Required] public Guid IssueId { get; set; }
    public Guid? SprintId { get; set; }
}

public sealed class SubmitPokerVoteRequest
{
    [Range(0, 100)] public decimal VoteValue { get; set; }
}

public sealed class FinalizePokerRequest
{
    [Range(0, 100)] public decimal StoryPoints { get; set; }
}

public sealed class RejectStoryRequest
{
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
}

public sealed class CreateRetroCardRequest
{
    [Required, RegularExpression("^(Well|Wrong|Action)$")]
    public string ColumnType { get; set; } = "Well";
    [Required, StringLength(2000, MinimumLength = 1)] public string Content { get; set; } = "";
    public bool IsAnonymous { get; set; }
}

public sealed class CompleteSprintRolloverRequest
{
    [Required, RegularExpression("^(Backlog|NextSprint)$")]
    public string TargetDestination { get; set; } = "Backlog";
    public Guid? NextSprintId { get; set; }
}

[Route("api")]
[Route("api/v1")]
[TypeFilter(typeof(ProjectAuthorizationFilter))]
public class SprintCeremoniesController(
    PlanningDbContext planning,
    DeliveryIntelligenceDbContext delivery,
    IUserLookupService userLookup,
    ProjectAccess access) : ApiControllerBase
{
    // ==========================================
    // 1. PLANNING POKER ONLINE (FIBONACCI)
    // ==========================================

    [HttpPost("poker/sessions")]
    public async Task<IActionResult> CreateOrGetPokerSession([FromBody] CreatePokerSessionRequest request)
    {
        var issue = await delivery.Issues.AsNoTracking().SingleOrDefaultAsync(i => i.Id == request.IssueId && !i.IsDeleted);
        if (issue is null) return NotFound(new { title = "Không tìm thấy User Story." });
        if (!await access.AllowsAsync(issue.ProjectId)) return Forbid();

        var session = await planning.PlanningPokerSessions
            .FirstOrDefaultAsync(s => s.IssueId == request.IssueId && s.Status != "Finished");

        if (session is null)
        {
            session = new PlanningPokerSession
            {
                Id = Guid.NewGuid(),
                ProjectId = issue.ProjectId,
                SprintId = request.SprintId ?? issue.SprintId,
                IssueId = issue.Id,
                Status = "Voting",
                CreatedAt = DateTime.UtcNow
            };
            planning.PlanningPokerSessions.Add(session);
            await planning.SaveChangesAsync();
        }

        return await GetSessionDetails(session.Id);
    }

    [HttpGet("poker/sessions/{id:guid}")]
    public Task<IActionResult> GetSessionDetailsEndpoint(Guid id) => GetSessionDetails(id);

    private async Task<IActionResult> GetSessionDetails(Guid sessionId)
    {
        var session = await planning.PlanningPokerSessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sessionId);
        if (session is null) return NotFound();

        var votes = await planning.PlanningPokerVotes.AsNoTracking()
            .Where(v => v.SessionId == sessionId)
            .ToListAsync();

        var users = await userLookup.GetAllActiveDisplayInfoAsync();
        var userDict = users.ToDictionary(u => u.UserId, u => u.DisplayName);

        var isRevealed = session.Status is "Revealed" or "Finished";

        var voteList = votes.Select(v => new
        {
            v.UserId,
            UserName = userDict.GetValueOrDefault(v.UserId, "Thành viên"),
            VoteValue = isRevealed ? (decimal?)v.VoteValue : null,
            HasVoted = true
        }).ToList();

        var issue = await delivery.Issues.AsNoTracking().SingleOrDefaultAsync(i => i.Id == session.IssueId);

        return Ok(new
        {
            session.Id,
            session.ProjectId,
            session.SprintId,
            session.IssueId,
            IssueTitle = issue?.Title ?? "",
            session.Status,
            session.FinalStoryPoints,
            Votes = voteList,
            TotalVoted = votes.Count
        });
    }

    [HttpPost("poker/sessions/{id:guid}/vote")]
    public async Task<IActionResult> SubmitVote(Guid id, [FromBody] SubmitPokerVoteRequest request)
    {
        var session = await planning.PlanningPokerSessions.SingleOrDefaultAsync(s => s.Id == id);
        if (session is null) return NotFound();
        if (!await access.AllowsAsync(session.ProjectId)) return Forbid();

        var vote = await planning.PlanningPokerVotes.SingleOrDefaultAsync(v => v.SessionId == id && v.UserId == access.UserId);
        if (vote is null)
        {
            vote = new PlanningPokerVote
            {
                Id = Guid.NewGuid(),
                SessionId = id,
                UserId = access.UserId,
                VoteValue = request.VoteValue,
                VotedAt = DateTime.UtcNow
            };
            planning.PlanningPokerVotes.Add(vote);
        }
        else
        {
            vote.VoteValue = request.VoteValue;
            vote.VotedAt = DateTime.UtcNow;
        }

        await planning.SaveChangesAsync();
        return await GetSessionDetails(id);
    }

    [HttpPost("poker/sessions/{id:guid}/reveal")]
    public async Task<IActionResult> RevealVotes(Guid id)
    {
        var session = await planning.PlanningPokerSessions.SingleOrDefaultAsync(s => s.Id == id);
        if (session is null) return NotFound();
        if (!await access.AllowsAsync(session.ProjectId, "issue.update")) return Forbid();

        session.Status = "Revealed";
        session.UpdatedAt = DateTime.UtcNow;
        await planning.SaveChangesAsync();

        return await GetSessionDetails(id);
    }

    [HttpPost("poker/sessions/{id:guid}/finalize")]
    public async Task<IActionResult> FinalizePoker(Guid id, [FromBody] FinalizePokerRequest request)
    {
        var session = await planning.PlanningPokerSessions.SingleOrDefaultAsync(s => s.Id == id);
        if (session is null) return NotFound();
        if (!await access.AllowsAsync(session.ProjectId, "issue.update")) return Forbid();

        session.Status = "Finished";
        session.FinalStoryPoints = request.StoryPoints;
        session.UpdatedAt = DateTime.UtcNow;

        var issue = await delivery.Issues.SingleOrDefaultAsync(i => i.Id == session.IssueId);
        if (issue is not null)
        {
            issue.StoryPoints = request.StoryPoints;
            issue.UpdatedAt = DateTime.UtcNow;
        }

        await planning.SaveChangesAsync();
        await delivery.SaveChangesAsync();

        return await GetSessionDetails(id);
    }

    // ==========================================
    // 2. SPRINT REVIEW & PO SIGN-OFF
    // ==========================================

    [HttpGet("sprints/{sprintId:guid}/review-items")]
    public async Task<IActionResult> GetReviewItems(Guid sprintId)
    {
        var sprint = await planning.Sprints.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sprintId);
        if (sprint is null) return NotFound();
        if (!await access.AllowsAsync(sprint.ProjectId)) return Forbid();

        // Lấy tất cả các Story trong Sprint
        var storyTypes = await planning.IssueTypes.AsNoTracking()
            .Where(t => t.ProjectId == sprint.ProjectId && !t.IsSubtask)
            .Select(t => t.Id)
            .ToListAsync();

        var stories = await delivery.Issues.AsNoTracking()
            .Where(i => i.SprintId == sprintId && storyTypes.Contains(i.IssueTypeId) && !i.IsDeleted)
            .ToListAsync();

        var storyIds = stories.Select(s => s.Id).ToList();

        var subtasks = await delivery.Issues.AsNoTracking()
            .Where(i => i.ParentId.HasValue && storyIds.Contains(i.ParentId.Value) && !i.IsDeleted)
            .ToListAsync();

        var criteria = await delivery.AcceptanceCriteria.AsNoTracking()
            .Where(c => storyIds.Contains(c.IssueId))
            .ToListAsync();

        var statuses = await planning.WorkflowStatuses.AsNoTracking()
            .Where(s => s.ProjectId == sprint.ProjectId)
            .ToDictionaryAsync(s => s.Id);

        var links = await planning.SpecStoryLinks.AsNoTracking()
            .Where(l => storyIds.Contains(l.IssueId))
            .ToListAsync();

        var project = await planning.Projects.AsNoTracking().SingleAsync(p => p.Id == sprint.ProjectId);

        var result = stories.Select(s =>
        {
            var mySubtasks = subtasks.Where(sub => sub.ParentId == s.Id).ToList();
            var totalSubs = mySubtasks.Count;
            var doneSubs = mySubtasks.Count(sub => statuses.TryGetValue(sub.StatusId, out var st) && (st.IsDone || st.Category == "Done"));
            var myCriteria = criteria.Where(c => c.IssueId == s.Id).ToList();
            var myLink = links.FirstOrDefault(l => l.IssueId == s.Id);
            var isCurrentDone = statuses.TryGetValue(s.StatusId, out var stCurrent) && (stCurrent.IsDone || stCurrent.Category == "Done");

            return new
            {
                s.Id,
                IssueKey = $"{project.ProjectKey}-{s.IssueNumber}",
                s.Title,
                s.StoryPoints,
                StatusName = statuses.TryGetValue(s.StatusId, out var currSt) ? currSt.Name : "",
                IsDone = isCurrentDone,
                TotalSubtasks = totalSubs,
                CompletedSubtasks = doneSubs,
                IsSubtasksAllDone = totalSubs > 0 && totalSubs == doneSubs,
                Criteria = myCriteria.Select(c => new { c.Id, c.Content, c.IsMet }),
                LinkedSpecId = myLink?.DocumentId,
                LinkedExcerpt = myLink?.Excerpt
            };
        });

        return Ok(result);
    }

    [HttpPost("sprints/{sprintId:guid}/accept-story/{storyId:guid}")]
    public async Task<IActionResult> AcceptStory(Guid sprintId, Guid storyId)
    {
        var sprint = await planning.Sprints.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sprintId);
        if (sprint is null) return NotFound();
        if (!await access.AllowsAsync(sprint.ProjectId, "project.settings.manage") && !await access.AllowsAsync(sprint.ProjectId, "sprint.close"))
            return Forbid();

        var story = await delivery.Issues.SingleOrDefaultAsync(i => i.Id == storyId && i.SprintId == sprintId);
        if (story is null) return NotFound();

        // Tìm trạng thái Done của dự án
        var doneStatus = await planning.WorkflowStatuses.FirstOrDefaultAsync(s => s.ProjectId == sprint.ProjectId && s.IsDone)
            ?? await planning.WorkflowStatuses.FirstOrDefaultAsync(s => s.ProjectId == sprint.ProjectId && s.Category == "Done");
        if (doneStatus is null) return BadRequest(new { title = "Dự án chưa cấu hình trạng thái Done." });

        story.StatusId = doneStatus.Id;
        story.ResolvedAt = DateTime.UtcNow;
        story.UpdatedAt = DateTime.UtcNow;

        delivery.ActivityLogs.Add(new ActivityLog
        {
            Id = Guid.NewGuid(),
            IssueId = story.Id,
            UserId = access.UserId,
            Action = "PO Sign-off: Accepted",
            NewValue = "Done",
            Source = "User",
            CreatedAt = DateTime.UtcNow
        });

        await delivery.SaveChangesAsync();
        return Ok(new { story.Id, Status = "Accepted", doneStatus.Name });
    }

    [HttpPost("sprints/{sprintId:guid}/reject-story/{storyId:guid}")]
    public async Task<IActionResult> RejectStory(Guid sprintId, Guid storyId, [FromBody] RejectStoryRequest request)
    {
        var sprint = await planning.Sprints.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sprintId);
        if (sprint is null) return NotFound();
        if (!await access.AllowsAsync(sprint.ProjectId, "project.settings.manage") && !await access.AllowsAsync(sprint.ProjectId, "sprint.close"))
            return Forbid();

        var story = await delivery.Issues.SingleOrDefaultAsync(i => i.Id == storyId && i.SprintId == sprintId);
        if (story is null) return NotFound();

        delivery.ActivityLogs.Add(new ActivityLog
        {
            Id = Guid.NewGuid(),
            IssueId = story.Id,
            UserId = access.UserId,
            Action = "PO Sign-off: Rejected",
            NewValue = request.Reason.Trim(),
            Source = "User",
            CreatedAt = DateTime.UtcNow
        });

        await delivery.SaveChangesAsync();
        return Ok(new { story.Id, Status = "Rejected", Reason = request.Reason.Trim() });
    }

    // ==========================================
    // 3. SPRINT RETROSPECTIVE BOARD
    // ==========================================

    [HttpGet("sprints/{sprintId:guid}/retro-cards")]
    public async Task<IActionResult> GetRetroCards(Guid sprintId)
    {
        var sprint = await planning.Sprints.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sprintId);
        if (sprint is null) return NotFound();
        if (!await access.AllowsAsync(sprint.ProjectId)) return Forbid();

        var cards = await planning.SprintRetrospectiveCards.AsNoTracking()
            .Where(c => c.SprintId == sprintId)
            .OrderByDescending(c => c.VoteCount)
            .ThenBy(c => c.CreatedAt)
            .ToListAsync();

        var users = await userLookup.GetAllActiveDisplayInfoAsync();
        var userDict = users.ToDictionary(u => u.UserId, u => u.DisplayName);

        return Ok(cards.Select(c => new
        {
            c.Id,
            c.SprintId,
            c.ColumnType,
            c.Content,
            c.IsAnonymous,
            AuthorName = c.IsAnonymous ? "Thành viên ẩn danh" : userDict.GetValueOrDefault(c.AuthorUserId, "Thành viên"),
            c.VoteCount,
            c.ConvertedToIssueId,
            c.CreatedAt
        }));
    }

    [HttpPost("sprints/{sprintId:guid}/retro-cards")]
    public async Task<IActionResult> CreateRetroCard(Guid sprintId, [FromBody] CreateRetroCardRequest request)
    {
        var sprint = await planning.Sprints.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sprintId);
        if (sprint is null) return NotFound();
        if (!await access.AllowsAsync(sprint.ProjectId)) return Forbid();

        var card = new SprintRetrospectiveCard
        {
            Id = Guid.NewGuid(),
            SprintId = sprintId,
            ColumnType = request.ColumnType,
            Content = request.Content.Trim(),
            IsAnonymous = request.IsAnonymous,
            AuthorUserId = access.UserId,
            VoteCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        planning.SprintRetrospectiveCards.Add(card);
        await planning.SaveChangesAsync();
        return StatusCode(201, card);
    }

    [HttpPost("retro-cards/{id:guid}/vote")]
    public async Task<IActionResult> VoteRetroCard(Guid id)
    {
        var card = await planning.SprintRetrospectiveCards.SingleOrDefaultAsync(c => c.Id == id);
        if (card is null) return NotFound();

        card.VoteCount += 1;
        await planning.SaveChangesAsync();
        return Ok(new { card.Id, card.VoteCount });
    }

    [HttpPost("retro-cards/{id:guid}/convert-to-task")]
    public async Task<IActionResult> ConvertRetroCardToTask(Guid id)
    {
        var card = await planning.SprintRetrospectiveCards.SingleOrDefaultAsync(c => c.Id == id);
        if (card is null) return NotFound();
        if (card.ConvertedToIssueId.HasValue) return Conflict(new { title = "Thẻ này đã được chuyển thành công việc." });

        var sprint = await planning.Sprints.AsNoTracking().SingleAsync(s => s.Id == card.SprintId);
        if (!await access.AllowsAsync(sprint.ProjectId, "issue.create")) return Forbid();

        var taskType = await planning.IssueTypes.FirstOrDefaultAsync(t => t.ProjectId == sprint.ProjectId && t.Name == "Task")
            ?? await planning.IssueTypes.FirstAsync(t => t.ProjectId == sprint.ProjectId && !t.IsSubtask);

        var initStatus = await planning.WorkflowStatuses.FirstOrDefaultAsync(s => s.ProjectId == sprint.ProjectId && s.IsInitial)
            ?? await planning.WorkflowStatuses.FirstAsync(s => s.ProjectId == sprint.ProjectId);

        var priority = await planning.Priorities.FirstAsync();

        await delivery.Database.ExecuteSqlInterpolatedAsync($"UPDATE `Project` SET `IssueCounter` = `IssueCounter` + 1 WHERE `Id` = {sprint.ProjectId}");
        var issueNumber = await delivery.Database.SqlQuery<int>($"SELECT `IssueCounter` AS `Value` FROM `Project` WHERE `Id` = {sprint.ProjectId}").SingleAsync();

        var issue = new Issue
        {
            Id = Guid.NewGuid(),
            ProjectId = sprint.ProjectId,
            IssueNumber = issueNumber,
            RankOrder = issueNumber,
            Title = $"[Retro Action] {card.Content}",
            Description = $"Hành động cải tiến chuyển đổi từ buổi Retro của Sprint: {sprint.Name}",
            StatusId = initStatus.Id,
            IssueTypeId = taskType.Id,
            PriorityId = priority.Id,
            ReporterId = access.UserId,
            CreatedAt = DateTime.UtcNow
        };

        delivery.Issues.Add(issue);
        await delivery.SaveChangesAsync();

        card.ConvertedToIssueId = issue.Id;
        await planning.SaveChangesAsync();

        var project = await planning.Projects.AsNoTracking().SingleAsync(p => p.Id == sprint.ProjectId);

        return Ok(new
        {
            IssueId = issue.Id,
            IssueKey = $"{project.ProjectKey}-{issue.IssueNumber}",
            issue.Title
        });
    }

    // ==========================================
    // 4. COMPLETE SPRINT WIZARD & ROLLOVER
    // ==========================================

    [HttpGet("sprints/{sprintId:guid}/close-summary")]
    public async Task<IActionResult> GetCloseSummary(Guid sprintId)
    {
        var sprint = await planning.Sprints.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sprintId);
        if (sprint is null) return NotFound();
        if (!await access.AllowsAsync(sprint.ProjectId)) return Forbid();

        var statuses = await planning.WorkflowStatuses.AsNoTracking()
            .Where(s => s.ProjectId == sprint.ProjectId)
            .ToDictionaryAsync(s => s.Id);

        var issues = await delivery.Issues.AsNoTracking()
            .Where(i => i.SprintId == sprintId && !i.IsDeleted && !i.ParentId.HasValue)
            .ToListAsync();

        var committedPoints = issues.Sum(i => i.StoryPoints ?? 0);
        var completedIssues = issues.Where(i => statuses.TryGetValue(i.StatusId, out var st) && (st.IsDone || st.Category == "Done")).ToList();
        var completedPoints = completedIssues.Sum(i => i.StoryPoints ?? 0);
        var unfinishedIssues = issues.Where(i => !statuses.TryGetValue(i.StatusId, out var st) || (!st.IsDone && st.Category != "Done")).ToList();

        var futureSprints = await planning.Sprints.AsNoTracking()
            .Where(s => s.ProjectId == sprint.ProjectId && s.Id != sprintId && s.Status != "Completed")
            .OrderBy(s => s.OrderIndex)
            .Select(s => new { s.Id, s.Name, s.Status })
            .ToListAsync();

        return Ok(new
        {
            sprint.Id,
            sprint.Name,
            sprint.Goal,
            CommittedStoryPoints = committedPoints,
            CompletedStoryPoints = completedPoints,
            TotalStories = issues.Count,
            CompletedStories = completedIssues.Count,
            UnfinishedStories = unfinishedIssues.Count,
            AvailableNextSprints = futureSprints
        });
    }

    [HttpPost("sprints/{sprintId:guid}/complete-and-rollover")]
    public async Task<IActionResult> CompleteAndRollover(Guid sprintId, [FromBody] CompleteSprintRolloverRequest request)
    {
        var sprint = await planning.Sprints.SingleOrDefaultAsync(s => s.Id == sprintId);
        if (sprint is null) return NotFound();
        if (!await access.AllowsAsync(sprint.ProjectId, "sprint.close")) return Forbid();

        var statuses = await planning.WorkflowStatuses.AsNoTracking()
            .Where(s => s.ProjectId == sprint.ProjectId)
            .ToDictionaryAsync(s => s.Id);

        // Đóng Sprint
        sprint.Status = "Completed";
        sprint.UpdatedAt = DateTime.UtcNow;

        // Lấy tất cả việc dở dang trong Sprint
        var issues = await delivery.Issues
            .Where(i => i.SprintId == sprintId && !i.IsDeleted)
            .ToListAsync();

        var unfinished = issues.Where(i => !statuses.TryGetValue(i.StatusId, out var st) || (!st.IsDone && st.Category != "Done")).ToList();

        Guid? nextSprintId = request.TargetDestination == "NextSprint" ? request.NextSprintId : null;

        foreach (var item in unfinished)
        {
            item.SprintId = nextSprintId;
            item.UpdatedAt = DateTime.UtcNow;
        }

        await planning.SaveChangesAsync();
        await delivery.SaveChangesAsync();

        return Ok(new
        {
            sprint.Id,
            Status = "Completed",
            RolledOverCount = unfinished.Count,
            RolledOverTo = request.TargetDestination
        });
    }
}
