#pragma warning disable CA1848
using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using IdentityExperience.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Planning.Domain.Entities;
using Planning.Infrastructure;
using DeliveryIntelligence.Infrastructure;
using ProjectMgmt.Solution.Realtime;
using ProjectMgmt.Solution.Security;

namespace ProjectMgmt.Solution.Controllers;

[Authorize]
[ApiController]
[Route("api/sprints/{sprintId:guid}/messages")]
[Route("api/v1/sprints/{sprintId:guid}/messages")]
public sealed class SprintMessagesController(
    PlanningDbContext planning,
    DeliveryIntelligenceDbContext delivery,
    IdentityExperienceDbContext identity,
    ProjectAccess access,
    IHubContext<NotificationHub> hub,
    INotificationServices notifications,
    ILogger<SprintMessagesController> logger) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMessages(Guid sprintId)
    {
        var sprint = await planning.Sprints.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sprintId);
        if (sprint == null)
        {
            return NotFound(new { message = "Không tìm thấy chu kỳ Sprint." });
        }

        if (!await access.AllowsAsync(sprint.ProjectId))
        {
            return StatusCode(403, new { message = "Bạn không có quyền truy cập vào Sprint này." });
        }

        var messages = await planning.SprintMessages
            .AsNoTracking()
            .Where(m => m.SprintId == sprintId && !m.IsDeleted)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        var userIds = messages.Select(m => m.UserId).Distinct().ToList();
        var profiles = await identity.UserProfiles.AsNoTracking().Where(p => userIds.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId);
        var users = await identity.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);

        var result = messages.Select(m => new SprintMessageDto
        {
            Id = m.Id,
            SprintId = m.SprintId,
            UserId = m.UserId,
            SenderName = profiles.TryGetValue(m.UserId, out var p) && !string.IsNullOrWhiteSpace(p.DisplayName) 
                ? p.DisplayName 
                : (users.TryGetValue(m.UserId, out var u) ? u.Email : "Thành viên"),
            SenderEmail = users.TryGetValue(m.UserId, out var usr) ? usr.Email : null,
            SenderAvatar = profiles.TryGetValue(m.UserId, out var prof) ? prof.AvatarUrl : null,
            Content = m.Content,
            CreatedAt = m.CreatedAt,
            IsCurrentUser = m.UserId == access.UserId
        }).ToList();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage(Guid sprintId, [FromBody] CreateSprintMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Content))
        {
            return BadRequest(new { message = "Nội dung tin nhắn không được để trống." });
        }

        var sprint = await planning.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId);
        if (sprint == null)
        {
            return NotFound(new { message = "Không tìm thấy chu kỳ Sprint." });
        }

        if (!await access.AllowsAsync(sprint.ProjectId))
        {
            return StatusCode(403, new { message = "Bạn không có quyền tham gia hội thoại trong Sprint này." });
        }

        var message = new SprintMessage
        {
            Id = Guid.NewGuid(),
            SprintId = sprintId,
            UserId = access.UserId,
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        planning.SprintMessages.Add(message);
        await planning.SaveChangesAsync();

        var senderProfile = await identity.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == access.UserId);
        var senderUser = await identity.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == access.UserId);
        var senderName = !string.IsNullOrWhiteSpace(senderProfile?.DisplayName) 
            ? senderProfile.DisplayName 
            : (senderUser?.Email ?? "Thành viên");

        var broadcastDto = new SprintMessageDto
        {
            Id = message.Id,
            SprintId = sprintId,
            UserId = message.UserId,
            SenderName = senderName,
            SenderEmail = senderUser?.Email,
            SenderAvatar = senderProfile?.AvatarUrl,
            Content = message.Content,
            CreatedAt = message.CreatedAt,
            IsCurrentUser = false
        };

        // 1. Phát sự kiện realtime tới phòng chat của Sprint qua SignalR
        try
        {
            await hub.Clients.Group($"sprint:{sprintId}").SendAsync("ReceiveSprintMessage", broadcastDto);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không thể phát sự kiện ReceiveSprintMessage tới group sprint:{SprintId}", sprintId);
        }

        // 2. Gửi thông báo tức thì (Event-driven notification) tới tất cả những người liên quan đến Sprint
        var sprintAssignees = await delivery.Issues
            .AsNoTracking()
            .Where(i => i.SprintId == sprintId && !i.IsDeleted && i.AssigneeId.HasValue)
            .Select(i => i.AssigneeId!.Value)
            .Distinct()
            .ToListAsync();

        var project = await planning.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == sprint.ProjectId);
        var projectMembers = await identity.UserRoles
            .AsNoTracking()
            .Where(ur => ur.ScopeType == "Project" && ur.ScopeId == sprint.ProjectId)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync();

        var recipients = new HashSet<Guid>(sprintAssignees);
        if (project?.LeadUserId != null && project.LeadUserId != Guid.Empty)
        {
            recipients.Add(project.LeadUserId);
        }
        foreach (var memberId in projectMembers)
        {
            recipients.Add(memberId);
        }
        // Loại bỏ người vừa gửi tin nhắn
        recipients.Remove(access.UserId);

        var snippet = message.Content.Length > 60 ? string.Concat(message.Content.AsSpan(0, 57), "...") : message.Content;
        foreach (var recipientId in recipients)
        {
            try
            {
                await notifications.PublishAsync(new NotificationDto
                {
                    UserId = recipientId,
                    Type = "SprintChat",
                    Title = $"[{sprint.Name}] {senderName}: {snippet}",
                    Content = message.Content,
                    EntityType = "Sprint",
                    EntityId = sprintId,
                    ProjectId = sprint.ProjectId,
                    ActorId = access.UserId
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không thể gửi thông báo SprintChat tới người dùng {RecipientId}", recipientId);
            }
        }

        var callerDto = new SprintMessageDto
        {
            Id = message.Id,
            SprintId = sprintId,
            UserId = message.UserId,
            SenderName = senderName,
            SenderEmail = senderUser?.Email,
            SenderAvatar = senderProfile?.AvatarUrl,
            Content = message.Content,
            CreatedAt = message.CreatedAt,
            IsCurrentUser = true
        };

        return Ok(callerDto);
    }

    [HttpDelete("{messageId:guid}")]
    public async Task<IActionResult> DeleteMessage(Guid sprintId, Guid messageId)
    {
        var sprint = await planning.Sprints.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sprintId);
        if (sprint == null) return NotFound(new { message = "Không tìm thấy chu kỳ Sprint." });

        if (!await access.AllowsAsync(sprint.ProjectId)) return StatusCode(403);

        var message = await planning.SprintMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.SprintId == sprintId);
        if (message == null || message.IsDeleted) return NotFound(new { message = "Tin nhắn không tồn tại hoặc đã bị xóa." });

        var canModerate = await access.AllowsAsync(sprint.ProjectId, "project.update");
        if (message.UserId != access.UserId && !canModerate)
        {
            return StatusCode(403, new { message = "Bạn chỉ có thể xóa tin nhắn của chính mình." });
        }

        message.IsDeleted = true;
        message.UpdatedAt = DateTime.UtcNow;
        await planning.SaveChangesAsync();

        try
        {
            await hub.Clients.Group($"sprint:{sprintId}").SendAsync("SprintMessageDeleted", messageId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không thể phát sự kiện SprintMessageDeleted tới group sprint:{SprintId}", sprintId);
        }

        return NoContent();
    }
}

public sealed class SprintMessageDto
{
    public Guid Id { get; set; }
    public Guid SprintId { get; set; }
    public Guid UserId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? SenderEmail { get; set; }
    public string? SenderAvatar { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsCurrentUser { get; set; }
}

public sealed class CreateSprintMessageRequest
{
    public string Content { get; set; } = string.Empty;
}
