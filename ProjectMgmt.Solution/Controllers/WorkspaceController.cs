using System.Security.Cryptography;
using System.Text.Json;
using IdentityExperience.Application.Dto;
using IdentityExperience.Application.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using ProjectMgmt.IdentityAccess.Contracts;

namespace ProjectMgmt.Solution.Controllers;

#region DTOs

public sealed class RecentItemDto
{
    public string Kind { get; set; } = "";
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string OrgId { get; set; } = "";
    public string VisitedAt { get; set; } = "";
}

public sealed class RecordVisitRequestDto
{
    public string Kind { get; set; } = "";
    public string Id { get; set; } = "";
}

public sealed class OrgMembershipDto
{
    public string OrgId { get; set; } = "";
    public string OrgName { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public string Role { get; set; } = "Member";
    public int MemberCount { get; set; } = 1;
    public string JoinedAt { get; set; } = "";
}

public sealed class OrgMemberDto
{
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "Member";
    public string JoinedAt { get; set; } = "";
}

public sealed class OrgRoleOptionDto
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Assignable { get; set; }
}

public sealed class UpdateOrgMemberRoleRequestDto
{
    public string Role { get; set; } = "Member";
}

public sealed class InviteOrgMembersRequestDto
{
    public List<string> Emails { get; set; } = [];
    public string Role { get; set; } = "Member";
}

public sealed class InviteEmailResultDto
{
    public string Email { get; set; } = "";
    public string Outcome { get; set; } = "Sent";
}

public sealed class OrgInvitationDto
{
    public string Id { get; set; } = "";
    public string OrgId { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "Member";
    public string InvitedByName { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}

public sealed class MyInvitationDto
{
    public string Id { get; set; } = "";
    public string OrgId { get; set; } = "";
    public string OrgName { get; set; } = "";
    public string Role { get; set; } = "Member";
    public string InvitedByName { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}

public sealed class RespondInvitationRequestDto
{
    public bool Accept { get; set; }
}

public sealed class InviteLinkDto
{
    public string Token { get; set; } = "";
    public string OrgId { get; set; } = "";
    public string Role { get; set; } = "Member";
    public string CreatedAt { get; set; } = "";
}

public sealed class InviteLinkPreviewDto
{
    public string Token { get; set; } = "";
    public string OrgId { get; set; } = "";
    public string OrgName { get; set; } = "";
    public string InvitedByName { get; set; } = "";
    public string Role { get; set; } = "Member";
    public bool AlreadyMember { get; set; }
}

public sealed class WorkflowStatusDefDto
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "ToDo";
}

public sealed class WorkflowTemplateDto
{
    public string Id { get; set; } = "";
    public string? OrgId { get; set; }
    public string Name { get; set; } = "";
    public string ManagementType { get; set; } = "Scrum";
    public List<WorkflowStatusDefDto> Statuses { get; set; } = [];
    public bool IsPreset { get; set; }
    public bool IsDefault { get; set; }
}

public sealed class SaveWorkflowTemplateRequestDto
{
    public string Name { get; set; } = "";
    public string ManagementType { get; set; } = "Scrum";
    public List<WorkflowStatusDefDto> Statuses { get; set; } = [];
}

#endregion

[ApiController]
[Authorize]
public sealed class WorkspaceController : ControllerBase
{
    private readonly MySqlConnection _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly INotificationServices _notificationServices;
    private readonly IEmailService _emailService;
    private readonly ILogger<WorkspaceController> _logger;

    public WorkspaceController(
        MySqlConnection db,
        ICurrentUserContext currentUser,
        INotificationServices notificationServices,
        IEmailService emailService,
        ILogger<WorkspaceController> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _notificationServices = notificationServices;
        _emailService = emailService;
        _logger = logger;
    }

    private Guid CurrentUserId => _currentUser.UserId ?? Guid.Empty;

    private static string AsString(object? val) => val?.ToString() ?? "";

    private async Task EnsureConnectionOpenAsync()
    {
        if (_db.State != System.Data.ConnectionState.Open)
        {
            await _db.OpenAsync();
        }
    }

    #region 1. Gần đây (Recent Visits)

    [HttpGet("api/users/me/recent")]
    public async Task<IActionResult> GetRecentVisits([FromQuery] Guid? orgId, [FromQuery] int limit = 100)
    {
        if (CurrentUserId == Guid.Empty) return Unauthorized();
        await EnsureConnectionOpenAsync();

        var safeLimit = Math.Clamp(limit > 0 ? limit : 100, 1, 500);

        var list = new List<RecentItemDto>();
        await using var cmd = _db.CreateCommand();
        cmd.CommandText = $@"
            SELECT Kind, ItemId, Title, Subtitle, OrgId, VisitedAt
            FROM UserRecentVisit
            WHERE UserId = @userId
              AND (@orgId IS NULL OR OrgId = @orgId)
            ORDER BY VisitedAt DESC
            LIMIT {safeLimit};";
        cmd.Parameters.AddWithValue("@userId", CurrentUserId.ToString());
        cmd.Parameters.AddWithValue("@orgId", orgId.HasValue ? orgId.Value.ToString() : (object)DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new RecentItemDto
            {
                Kind = AsString(reader.GetValue(0)),
                Id = AsString(reader.GetValue(1)),
                Title = AsString(reader.GetValue(2)),
                Subtitle = AsString(reader.GetValue(3)),
                OrgId = AsString(reader.GetValue(4)),
                VisitedAt = reader.GetDateTime(5).ToString("O")
            });
        }

        return Ok(list);
    }

    [HttpPost("api/users/me/recent")]
    public async Task<IActionResult> RecordVisit([FromBody] RecordVisitRequestDto request)
    {
        if (CurrentUserId == Guid.Empty) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Kind))
            return BadRequest();

        await EnsureConnectionOpenAsync();

        string title = "";
        string subtitle = "";
        string targetOrgId = "";

        if (request.Kind == "organization")
        {
            await using var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT Name FROM Organization WHERE Id = @id LIMIT 1;";
            cmd.Parameters.AddWithValue("@id", request.Id);
            var name = await cmd.ExecuteScalarAsync();
            if (name == null) return NotFound();
            title = name.ToString()!;
            subtitle = "Tổ chức";
            targetOrgId = request.Id;
        }
        else if (request.Kind == "project")
        {
            await using var cmd = _db.CreateCommand();
            cmd.CommandText = @"
                SELECT p.Name, p.ProjectKey, p.OrgId, COALESCE(o.Name, '') AS OrgName
                FROM Project p
                LEFT JOIN Organization o ON p.OrgId = o.Id
                WHERE p.Id = @id LIMIT 1;";
            cmd.Parameters.AddWithValue("@id", request.Id);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return NotFound();
            title = AsString(reader.GetValue(0));
            var key = AsString(reader.GetValue(1));
            targetOrgId = AsString(reader.GetValue(2));
            var orgName = AsString(reader.GetValue(3));
            subtitle = $"{key} · {orgName}";
        }
        else if (request.Kind == "issue")
        {
            await using var cmd = _db.CreateCommand();
            cmd.CommandText = @"
                SELECT i.Title, i.IssueNumber, p.ProjectKey, p.Name AS ProjectName, p.OrgId
                FROM Issue i
                JOIN Project p ON i.ProjectId = p.Id
                WHERE i.Id = @id LIMIT 1;";
            cmd.Parameters.AddWithValue("@id", request.Id);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return NotFound();
            title = AsString(reader.GetValue(0));
            var num = reader.GetInt32(1);
            var key = AsString(reader.GetValue(2));
            var projName = AsString(reader.GetValue(3));
            targetOrgId = AsString(reader.GetValue(4));
            subtitle = $"{key}-{num} · {projName}";
        }
        else
        {
            return BadRequest(new { title = "Loại mục gần đây không hợp lệ." });
        }

        await using var upsertCmd = _db.CreateCommand();
        upsertCmd.CommandText = @"
            INSERT INTO UserRecentVisit (Id, UserId, Kind, ItemId, Title, Subtitle, OrgId, VisitedAt)
            VALUES (@id, @userId, @kind, @itemId, @title, @subtitle, @orgId, UTC_TIMESTAMP(6))
            ON DUPLICATE KEY UPDATE Title = VALUES(Title), Subtitle = VALUES(Subtitle), OrgId = VALUES(OrgId), VisitedAt = UTC_TIMESTAMP(6);";
        upsertCmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
        upsertCmd.Parameters.AddWithValue("@userId", CurrentUserId.ToString());
        upsertCmd.Parameters.AddWithValue("@kind", request.Kind);
        upsertCmd.Parameters.AddWithValue("@itemId", request.Id);
        upsertCmd.Parameters.AddWithValue("@title", title);
        upsertCmd.Parameters.AddWithValue("@subtitle", subtitle);
        upsertCmd.Parameters.AddWithValue("@orgId", targetOrgId);

        await upsertCmd.ExecuteNonQueryAsync();
        return NoContent();
    }

    #endregion

    #region 2. Tư cách thành viên tổ chức (Organization Memberships & Roles)

    [HttpGet("api/users/me/organization-memberships")]
    public async Task<IActionResult> GetMyMemberships()
    {
        if (CurrentUserId == Guid.Empty) return Unauthorized();
        await EnsureConnectionOpenAsync();

        var memberships = new List<OrgMembershipDto>();
        await using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT o.Id, o.Name, o.OwnerId,
                   CASE 
                     WHEN o.OwnerId = @userId THEN 'Owner'
                     WHEN om.Role IS NOT NULL THEN om.Role
                     ELSE 'Member'
                   END AS Role,
                   COALESCE(om.JoinedAt, o.CreatedAt) AS JoinedAt,
                   (
                     SELECT COUNT(DISTINCT m.UserId)
                     FROM (
                       SELECT o.OwnerId AS UserId
                       UNION
                       SELECT om2.UserId FROM OrganizationMember om2 WHERE om2.OrgId = o.Id
                       UNION
                       SELECT ur.UserId FROM UserRole ur
                       JOIN Project p ON ur.ScopeId = p.Id
                       WHERE ur.ScopeType = 'Project' AND p.OrgId = o.Id
                     ) m
                   ) AS MemberCount
            FROM Organization o
            LEFT JOIN OrganizationMember om ON o.Id = om.OrgId AND om.UserId = @userId
            WHERE o.IsActive = 1
              AND (
                o.OwnerId = @userId
                OR om.UserId IS NOT NULL
                OR EXISTS (
                  SELECT 1 FROM UserRole ur
                  JOIN Project p ON ur.ScopeId = p.Id
                  WHERE ur.UserId = @userId AND ur.ScopeType = 'Project' AND p.OrgId = o.Id
                )
              )
            ORDER BY o.Name;";
        cmd.Parameters.AddWithValue("@userId", CurrentUserId.ToString());

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            memberships.Add(new OrgMembershipDto
            {
                OrgId = AsString(reader.GetValue(0)),
                OrgName = AsString(reader.GetValue(1)),
                OwnerId = AsString(reader.GetValue(2)),
                Role = AsString(reader.GetValue(3)),
                JoinedAt = reader.GetDateTime(4).ToString("O"),
                MemberCount = Convert.ToInt32(reader.GetValue(5))
            });
        }

        return Ok(memberships);
    }

    [HttpGet("api/organizations/roles")]
    public IActionResult GetOrganizationRoles()
    {
        var roles = new List<OrgRoleOptionDto>
        {
            new() { Code = "Owner", Name = "Chủ tổ chức", Description = "Lãnh đạo tối cao của toàn tổ chức, toàn quyền kiểm soát.", Assignable = false },
            new() { Code = "Admin", Name = "Quản trị viên", Description = "Quản lý thành viên, dự án và thiết lập quy trình của tổ chức.", Assignable = true },
            new() { Code = "Member", Name = "Thành viên", Description = "Tham gia vào các dự án được phân công.", Assignable = true }
        };
        return Ok(roles);
    }

    #endregion

    #region 3. Thành viên tổ chức (Organization Members)

    [HttpGet("api/organizations/{orgId:guid}/members")]
    public async Task<IActionResult> GetOrganizationMembers(Guid orgId)
    {
        await EnsureConnectionOpenAsync();

        var members = new Dictionary<string, OrgMemberDto>();
        await using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT u.Id, COALESCE(up.DisplayName, u.Email) AS DisplayName, u.Email, up.AvatarUrl,
                   CASE WHEN o.OwnerId = u.Id THEN 'Owner' ELSE COALESCE(om.Role, 'Member') END AS Role,
                   COALESCE(om.JoinedAt, o.CreatedAt) AS JoinedAt
            FROM Organization o
            JOIN User u ON (
                u.Id = o.OwnerId
                OR EXISTS (SELECT 1 FROM OrganizationMember om2 WHERE om2.OrgId = o.Id AND om2.UserId = u.Id)
                OR EXISTS (SELECT 1 FROM UserRole ur JOIN Project p ON ur.ScopeId = p.Id WHERE p.OrgId = o.Id AND ur.ScopeType = 'Project' AND ur.UserId = u.Id)
            )
            LEFT JOIN UserProfile up ON u.Id = up.UserId
            LEFT JOIN OrganizationMember om ON o.Id = om.OrgId AND om.UserId = u.Id
            WHERE o.Id = @orgId AND u.IsActive = 1
            ORDER BY Role = 'Owner' DESC, DisplayName ASC;";
        cmd.Parameters.AddWithValue("@orgId", orgId.ToString());

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var userId = AsString(reader.GetValue(0));
            if (!members.ContainsKey(userId))
            {
                members[userId] = new OrgMemberDto
                {
                    UserId = userId,
                    DisplayName = AsString(reader.GetValue(1)),
                    Email = AsString(reader.GetValue(2)),
                    AvatarUrl = reader.IsDBNull(3) ? null : AsString(reader.GetValue(3)),
                    Role = AsString(reader.GetValue(4)),
                    JoinedAt = reader.GetDateTime(5).ToString("O")
                };
            }
        }

        return Ok(members.Values.ToList());
    }

    [HttpPut("api/organizations/{orgId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> UpdateMemberRole(Guid orgId, Guid userId, [FromBody] UpdateOrgMemberRoleRequestDto request)
    {
        await EnsureConnectionOpenAsync();

        // Check if target is owner
        await using (var checkCmd = _db.CreateCommand())
        {
            checkCmd.CommandText = "SELECT OwnerId FROM Organization WHERE Id = @orgId LIMIT 1;";
            checkCmd.Parameters.AddWithValue("@orgId", orgId.ToString());
            var owner = await checkCmd.ExecuteScalarAsync();
            if (owner == null) return NotFound(new { title = "Không tìm thấy tổ chức." });
            if (owner.ToString() == userId.ToString())
            {
                return BadRequest(new { title = "Không thể thay đổi vai trò của Chủ tổ chức." });
            }
        }

        await using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO OrganizationMember (Id, OrgId, UserId, Role, JoinedAt)
            VALUES (@id, @orgId, @userId, @role, UTC_TIMESTAMP(6))
            ON DUPLICATE KEY UPDATE Role = VALUES(Role);";
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("@orgId", orgId.ToString());
        cmd.Parameters.AddWithValue("@userId", userId.ToString());
        cmd.Parameters.AddWithValue("@role", request.Role);
        await cmd.ExecuteNonQueryAsync();

        // Return updated member
        await using var fetchCmd = _db.CreateCommand();
        fetchCmd.CommandText = @"
            SELECT u.Id, COALESCE(up.DisplayName, u.Email), u.Email, up.AvatarUrl, om.Role, om.JoinedAt
            FROM OrganizationMember om
            JOIN User u ON om.UserId = u.Id
            LEFT JOIN UserProfile up ON u.Id = up.UserId
            WHERE om.OrgId = @orgId AND om.UserId = @userId LIMIT 1;";
        fetchCmd.Parameters.AddWithValue("@orgId", orgId.ToString());
        fetchCmd.Parameters.AddWithValue("@userId", userId.ToString());

        await using var reader = await fetchCmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return Ok(new OrgMemberDto
            {
                UserId = AsString(reader.GetValue(0)),
                DisplayName = AsString(reader.GetValue(1)),
                Email = AsString(reader.GetValue(2)),
                AvatarUrl = reader.IsDBNull(3) ? null : AsString(reader.GetValue(3)),
                Role = AsString(reader.GetValue(4)),
                JoinedAt = reader.GetDateTime(5).ToString("O")
            });
        }

        return Ok(new OrgMemberDto { UserId = userId.ToString(), Role = request.Role });
    }

    [HttpDelete("api/organizations/{orgId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid orgId, Guid userId)
    {
        await EnsureConnectionOpenAsync();

        // Check if target is owner
        await using (var checkCmd = _db.CreateCommand())
        {
            checkCmd.CommandText = "SELECT OwnerId FROM Organization WHERE Id = @orgId LIMIT 1;";
            checkCmd.Parameters.AddWithValue("@orgId", orgId.ToString());
            var owner = await checkCmd.ExecuteScalarAsync();
            if (owner != null && owner.ToString() == userId.ToString())
            {
                return BadRequest(new { title = "Không thể xóa Chủ tổ chức." });
            }
        }

        await using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM OrganizationMember WHERE OrgId = @orgId AND UserId = @userId;
            DELETE FROM UserRole WHERE UserId = @userId AND ScopeType = 'Project' AND ScopeId IN (SELECT Id FROM Project WHERE OrgId = @orgId);";
        cmd.Parameters.AddWithValue("@orgId", orgId.ToString());
        cmd.Parameters.AddWithValue("@userId", userId.ToString());
        await cmd.ExecuteNonQueryAsync();

        return NoContent();
    }

    #endregion

    #region 4. Lời mời (Invitations)

    [HttpPost("api/organizations/{orgId:guid}/invitations")]
    public async Task<IActionResult> InviteByEmail(Guid orgId, [FromBody] InviteOrgMembersRequestDto request)
    {
        await EnsureConnectionOpenAsync();
        var results = new List<InviteEmailResultDto>();

        string orgName = "Tổ chức";
        await using (var orgCmd = _db.CreateCommand())
        {
            orgCmd.CommandText = "SELECT Name FROM Organization WHERE Id = @orgId LIMIT 1;";
            orgCmd.Parameters.AddWithValue("@orgId", orgId.ToString());
            var orgNameObj = await orgCmd.ExecuteScalarAsync();
            if (orgNameObj != null) orgName = orgNameObj.ToString()!;
        }

        string inviterName = "Quản trị viên tổ chức";
        if (CurrentUserId != Guid.Empty)
        {
            await using var inviterCmd = _db.CreateCommand();
            inviterCmd.CommandText = @"
                SELECT COALESCE(up.DisplayName, u.Email, 'Quản trị viên tổ chức')
                FROM User u
                LEFT JOIN UserProfile up ON u.Id = up.UserId
                WHERE u.Id = @userId LIMIT 1;";
            inviterCmd.Parameters.AddWithValue("@userId", CurrentUserId.ToString());
            var inviterObj = await inviterCmd.ExecuteScalarAsync();
            if (inviterObj != null) inviterName = inviterObj.ToString()!;
        }

        // Ensure an active invite link token exists for this organization so the email has a direct 1-click join URL
        string inviteToken;
        await using (var linkCmd = _db.CreateCommand())
        {
            linkCmd.CommandText = "SELECT Token FROM OrganizationInviteLink WHERE OrgId = @orgId AND IsActive = 1 ORDER BY CreatedAt DESC LIMIT 1;";
            linkCmd.Parameters.AddWithValue("@orgId", orgId.ToString());
            var tokenObj = await linkCmd.ExecuteScalarAsync();
            if (tokenObj != null && !string.IsNullOrWhiteSpace(tokenObj.ToString()))
            {
                inviteToken = tokenObj.ToString()!;
            }
            else
            {
                inviteToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
                await using var createLinkCmd = _db.CreateCommand();
                createLinkCmd.CommandText = @"
                    INSERT INTO OrganizationInviteLink (Token, OrgId, Role, CreatedByUserId, CreatedAt, IsActive)
                    VALUES (@token, @orgId, @role, @userId, UTC_TIMESTAMP(6), 1);";
                createLinkCmd.Parameters.AddWithValue("@token", inviteToken);
                createLinkCmd.Parameters.AddWithValue("@orgId", orgId.ToString());
                createLinkCmd.Parameters.AddWithValue("@role", string.IsNullOrWhiteSpace(request.Role) ? "Member" : request.Role);
                createLinkCmd.Parameters.AddWithValue("@userId", CurrentUserId.ToString());
                await createLinkCmd.ExecuteNonQueryAsync();
            }
        }

        var originHeader = Request.Headers.Origin.FirstOrDefault();
        var baseUrl = !string.IsNullOrWhiteSpace(originHeader)
            ? originHeader.TrimEnd('/')
            : $"{Request.Scheme}://{Request.Host}";
        var inviteAcceptUrl = $"{baseUrl}/invite/{inviteToken}";
        var roleLabel = string.Equals(request.Role, "Admin", StringComparison.OrdinalIgnoreCase)
            ? "Quản trị viên"
            : "Thành viên";

        foreach (var email in request.Emails)
        {
            var trimmedEmail = email.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(trimmedEmail)) continue;

            // Check if user exists and get display name
            string? targetUserId = null;
            string targetDisplayName = trimmedEmail;
            await using (var checkUserCmd = _db.CreateCommand())
            {
                checkUserCmd.CommandText = @"
                    SELECT u.Id, COALESCE(up.DisplayName, u.Email)
                    FROM User u
                    LEFT JOIN UserProfile up ON u.Id = up.UserId
                    WHERE LOWER(u.Email) = @email LIMIT 1;";
                checkUserCmd.Parameters.AddWithValue("@email", trimmedEmail);
                await using var userReader = await checkUserCmd.ExecuteReaderAsync();
                if (await userReader.ReadAsync())
                {
                    targetUserId = AsString(userReader.GetValue(0));
                    targetDisplayName = AsString(userReader.GetValue(1));
                }
            }

            if (string.IsNullOrWhiteSpace(targetUserId))
            {
                results.Add(new InviteEmailResultDto { Email = trimmedEmail, Outcome = "NoAccount" });
                continue;
            }

            // Check if already member
            await using var checkMemberCmd = _db.CreateCommand();
            checkMemberCmd.CommandText = @"
                SELECT 1 FROM Organization WHERE Id = @orgId AND OwnerId = @userId
                UNION
                SELECT 1 FROM OrganizationMember WHERE OrgId = @orgId AND UserId = @userId
                LIMIT 1;";
            checkMemberCmd.Parameters.AddWithValue("@orgId", orgId.ToString());
            checkMemberCmd.Parameters.AddWithValue("@userId", targetUserId);
            var isMember = await checkMemberCmd.ExecuteScalarAsync() != null;

            if (isMember)
            {
                results.Add(new InviteEmailResultDto { Email = trimmedEmail, Outcome = "AlreadyMember" });
                continue;
            }

            // Check if already invited pending -> if so, update timestamp/role and resend email + notification!
            Guid invitationId;
            await using var checkInvCmd = _db.CreateCommand();
            checkInvCmd.CommandText = @"
                SELECT Id FROM OrganizationInvitation
                WHERE OrgId = @orgId AND LOWER(Email) = @email AND Status = 'Pending' LIMIT 1;";
            checkInvCmd.Parameters.AddWithValue("@orgId", orgId.ToString());
            checkInvCmd.Parameters.AddWithValue("@email", trimmedEmail);
            var existingInvIdObj = await checkInvCmd.ExecuteScalarAsync();

            if (existingInvIdObj != null && Guid.TryParse(existingInvIdObj.ToString(), out var parsedExistingId))
            {
                invitationId = parsedExistingId;
                await using var updateCmd = _db.CreateCommand();
                updateCmd.CommandText = @"
                    UPDATE OrganizationInvitation
                    SET Role = @role, InvitedByUserId = @invitedBy, CreatedAt = UTC_TIMESTAMP(6)
                    WHERE Id = @id;";
                updateCmd.Parameters.AddWithValue("@id", invitationId.ToString());
                updateCmd.Parameters.AddWithValue("@role", request.Role);
                updateCmd.Parameters.AddWithValue("@invitedBy", CurrentUserId.ToString());
                await updateCmd.ExecuteNonQueryAsync();
            }
            else
            {
                invitationId = Guid.NewGuid();
                await using var insertCmd = _db.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO OrganizationInvitation (Id, OrgId, Email, Role, InvitedByUserId, Status, CreatedAt)
                    VALUES (@id, @orgId, @email, @role, @invitedBy, 'Pending', UTC_TIMESTAMP(6));";
                insertCmd.Parameters.AddWithValue("@id", invitationId.ToString());
                insertCmd.Parameters.AddWithValue("@orgId", orgId.ToString());
                insertCmd.Parameters.AddWithValue("@email", trimmedEmail);
                insertCmd.Parameters.AddWithValue("@role", request.Role);
                insertCmd.Parameters.AddWithValue("@invitedBy", CurrentUserId.ToString());
                await insertCmd.ExecuteNonQueryAsync();
            }

            // 1. Publish in-app + real-time SignalR notification
            if (Guid.TryParse(targetUserId, out var targetUserGuid))
            {
                try
                {
                    await _notificationServices.PublishAsync(new NotificationDto
                    {
                        UserId = targetUserGuid,
                        Type = "OrganizationInvitation",
                        Title = $"Lời mời tham gia tổ chức {orgName}",
                        Content = $"{inviterName} đã mời bạn tham gia tổ chức \"{orgName}\" với vai trò {roleLabel}.",
                        EntityType = "OrganizationInvitation",
                        EntityId = invitationId,
                        ActorId = CurrentUserId != Guid.Empty ? CurrentUserId : null,
                        RoleName = request.Role,
                        SendEmail = false
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể tạo thông báo lời mời tổ chức cho {Email}", trimmedEmail);
                }
            }

            // 2. Send invitation email via SMTP
            try
            {
                await _emailService.SendOrganizationInvitationAsync(
                    trimmedEmail,
                    targetDisplayName,
                    orgName,
                    inviterName,
                    roleLabel,
                    inviteAcceptUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi email lời mời tổ chức tới {Email}", trimmedEmail);
            }

            results.Add(new InviteEmailResultDto { Email = trimmedEmail, Outcome = "Sent" });
        }

        return Ok(results);
    }

    [HttpGet("api/organizations/{orgId:guid}/invitations")]
    public async Task<IActionResult> GetOrgInvitations(Guid orgId, [FromQuery] string? status = "Pending")
    {
        await EnsureConnectionOpenAsync();

        var list = new List<OrgInvitationDto>();
        await using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT oi.Id, oi.OrgId, oi.Email, oi.Role, COALESCE(up.DisplayName, u.Email, 'Hệ thống') AS InvitedByName, oi.CreatedAt
            FROM OrganizationInvitation oi
            LEFT JOIN User u ON oi.InvitedByUserId = u.Id
            LEFT JOIN UserProfile up ON u.Id = up.UserId
            WHERE oi.OrgId = @orgId
              AND (@status IS NULL OR oi.Status = @status)
            ORDER BY oi.CreatedAt DESC;";
        cmd.Parameters.AddWithValue("@orgId", orgId.ToString());
        cmd.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(status) ? (object)DBNull.Value : status);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new OrgInvitationDto
            {
                Id = AsString(reader.GetValue(0)),
                OrgId = AsString(reader.GetValue(1)),
                Email = AsString(reader.GetValue(2)),
                Role = AsString(reader.GetValue(3)),
                InvitedByName = AsString(reader.GetValue(4)),
                CreatedAt = reader.GetDateTime(5).ToString("O")
            });
        }

        return Ok(list);
    }

    [HttpDelete("api/organizations/{orgId:guid}/invitations/{invitationId:guid}")]
    public async Task<IActionResult> CancelInvitation(Guid orgId, Guid invitationId)
    {
        await EnsureConnectionOpenAsync();

        await using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            UPDATE OrganizationInvitation
            SET Status = 'Canceled', RespondedAt = UTC_TIMESTAMP(6)
            WHERE Id = @id AND OrgId = @orgId;";
        cmd.Parameters.AddWithValue("@id", invitationId.ToString());
        cmd.Parameters.AddWithValue("@orgId", orgId.ToString());
        await cmd.ExecuteNonQueryAsync();

        return NoContent();
    }

    [HttpGet("api/users/me/organization-invitations")]
    public async Task<IActionResult> GetMyInvitations()
    {
        if (CurrentUserId == Guid.Empty) return Unauthorized();
        await EnsureConnectionOpenAsync();

        // Get user email
        string email;
        await using (var emailCmd = _db.CreateCommand())
        {
            emailCmd.CommandText = "SELECT Email FROM User WHERE Id = @userId LIMIT 1;";
            emailCmd.Parameters.AddWithValue("@userId", CurrentUserId.ToString());
            var emailObj = await emailCmd.ExecuteScalarAsync();
            if (emailObj == null) return Unauthorized();
            email = emailObj.ToString()!;
        }

        var list = new List<MyInvitationDto>();
        await using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT oi.Id, oi.OrgId, o.Name AS OrgName, oi.Role,
                       COALESCE(up.DisplayName, u.Email, 'Hệ thống') AS InvitedByName, oi.CreatedAt
                FROM OrganizationInvitation oi
                JOIN Organization o ON oi.OrgId = o.Id
                LEFT JOIN User u ON oi.InvitedByUserId = u.Id
                LEFT JOIN UserProfile up ON u.Id = up.UserId
                WHERE LOWER(oi.Email) = LOWER(@email) AND oi.Status = 'Pending' AND o.IsActive = 1
                ORDER BY oi.CreatedAt DESC;";
            cmd.Parameters.AddWithValue("@email", email);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new MyInvitationDto
                {
                    Id = AsString(reader.GetValue(0)),
                    OrgId = AsString(reader.GetValue(1)),
                    OrgName = AsString(reader.GetValue(2)),
                    Role = AsString(reader.GetValue(3)),
                    InvitedByName = AsString(reader.GetValue(4)),
                    CreatedAt = reader.GetDateTime(5).ToString("O")
                });
            }
        }

        // Ensure every pending OrganizationInvitation has a Notification row so the user sees it in /notifications
        foreach (var inv in list)
        {
            if (!Guid.TryParse(inv.Id, out var invGuid)) continue;
            await using var checkNotifCmd = _db.CreateCommand();
            checkNotifCmd.CommandText = @"
                SELECT 1 FROM Notification
                WHERE UserId = @userId AND EntityType = 'OrganizationInvitation' AND EntityId = @entityId
                LIMIT 1;";
            checkNotifCmd.Parameters.AddWithValue("@userId", CurrentUserId.ToString());
            checkNotifCmd.Parameters.AddWithValue("@entityId", invGuid.ToString());
            var exists = await checkNotifCmd.ExecuteScalarAsync() != null;
            if (!exists)
            {
                var roleLabel = string.Equals(inv.Role, "Admin", StringComparison.OrdinalIgnoreCase)
                    ? "Quản trị viên"
                    : "Thành viên";
                try
                {
                    await _notificationServices.PublishAsync(new NotificationDto
                    {
                        UserId = CurrentUserId,
                        Type = "OrganizationInvitation",
                        Title = $"Lời mời tham gia tổ chức {inv.OrgName}",
                        Content = $"{inv.InvitedByName} đã mời bạn tham gia tổ chức \"{inv.OrgName}\" với vai trò {roleLabel}.",
                        EntityType = "OrganizationInvitation",
                        EntityId = invGuid,
                        RoleName = inv.Role,
                        SendEmail = false
                    });
                }
                catch
                {
                    // Ignore backfill error
                }
            }
        }

        return Ok(list);
    }

    [HttpPost("api/users/me/organization-invitations/{invitationId:guid}/respond")]
    public async Task<IActionResult> RespondInvitation(Guid invitationId, [FromBody] RespondInvitationRequestDto request)
    {
        if (CurrentUserId == Guid.Empty) return Unauthorized();
        await EnsureConnectionOpenAsync();

        string orgId;
        string role;
        string invitedByUserId;
        await using (var fetchCmd = _db.CreateCommand())
        {
            fetchCmd.CommandText = "SELECT OrgId, Role, InvitedByUserId FROM OrganizationInvitation WHERE Id = @id AND Status = 'Pending' LIMIT 1;";
            fetchCmd.Parameters.AddWithValue("@id", invitationId.ToString());
            await using var reader = await fetchCmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return NotFound(new { title = "Lời mời không tồn tại hoặc đã xử lý." });
            orgId = AsString(reader.GetValue(0));
            role = AsString(reader.GetValue(1));
            invitedByUserId = AsString(reader.GetValue(2));
        }

        // Mark corresponding Notification as read
        await using (var markNotifCmd = _db.CreateCommand())
        {
            markNotifCmd.CommandText = @"
                UPDATE Notification
                SET IsRead = 1, ReadAt = UTC_TIMESTAMP(6)
                WHERE UserId = @userId AND EntityType = 'OrganizationInvitation' AND EntityId = @entityId;";
            markNotifCmd.Parameters.AddWithValue("@userId", CurrentUserId.ToString());
            markNotifCmd.Parameters.AddWithValue("@entityId", invitationId.ToString());
            await markNotifCmd.ExecuteNonQueryAsync();
        }

        string orgName = "Tổ chức";
        string ownerId = "";
        await using (var orgCmd = _db.CreateCommand())
        {
            orgCmd.CommandText = "SELECT Name, OwnerId FROM Organization WHERE Id = @orgId LIMIT 1;";
            orgCmd.Parameters.AddWithValue("@orgId", orgId);
            await using var reader = await orgCmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                orgName = AsString(reader.GetValue(0));
                ownerId = AsString(reader.GetValue(1));
            }
        }

        string responderName = "Thành viên";
        await using (var userCmd = _db.CreateCommand())
        {
            userCmd.CommandText = @"
                SELECT COALESCE(up.DisplayName, u.Email, 'Thành viên')
                FROM User u
                LEFT JOIN UserProfile up ON u.Id = up.UserId
                WHERE u.Id = @userId LIMIT 1;";
            userCmd.Parameters.AddWithValue("@userId", CurrentUserId.ToString());
            var rName = await userCmd.ExecuteScalarAsync();
            if (rName != null) responderName = rName.ToString()!;
        }

        if (request.Accept)
        {
            await using var acceptCmd = _db.CreateCommand();
            acceptCmd.CommandText = @"
                UPDATE OrganizationInvitation SET Status = 'Accepted', RespondedAt = UTC_TIMESTAMP(6) WHERE Id = @id;
                INSERT INTO OrganizationMember (Id, OrgId, UserId, Role, JoinedAt)
                VALUES (@memberId, @orgId, @userId, @role, UTC_TIMESTAMP(6))
                ON DUPLICATE KEY UPDATE Role = VALUES(Role);";
            acceptCmd.Parameters.AddWithValue("@id", invitationId.ToString());
            acceptCmd.Parameters.AddWithValue("@memberId", Guid.NewGuid().ToString());
            acceptCmd.Parameters.AddWithValue("@orgId", orgId);
            acceptCmd.Parameters.AddWithValue("@userId", CurrentUserId.ToString());
            acceptCmd.Parameters.AddWithValue("@role", role);
            await acceptCmd.ExecuteNonQueryAsync();

            if (Guid.TryParse(invitedByUserId, out var inviterGuid) && inviterGuid != CurrentUserId)
            {
                try
                {
                    await _notificationServices.PublishAsync(new NotificationDto
                    {
                        UserId = inviterGuid,
                        Type = "ProjectInvitationAccepted",
                        Title = $"Lời mời tham gia tổ chức {orgName} đã được chấp nhận",
                        Content = $"{responderName} đã chấp nhận lời mời tham gia tổ chức \"{orgName}\".",
                        EntityType = "Organization",
                        EntityId = Guid.TryParse(orgId, out var parsedOrgId) ? parsedOrgId : null,
                        ActorId = CurrentUserId,
                        SendEmail = false
                    });
                }
                catch { }
            }

            return Ok(new OrgMembershipDto
            {
                OrgId = orgId,
                OrgName = orgName,
                OwnerId = ownerId,
                Role = role,
                JoinedAt = DateTime.UtcNow.ToString("O"),
                MemberCount = 1
            });
        }
        else
        {
            await using var declineCmd = _db.CreateCommand();
            declineCmd.CommandText = "UPDATE OrganizationInvitation SET Status = 'Declined', RespondedAt = UTC_TIMESTAMP(6) WHERE Id = @id;";
            declineCmd.Parameters.AddWithValue("@id", invitationId.ToString());
            await declineCmd.ExecuteNonQueryAsync();

            if (Guid.TryParse(invitedByUserId, out var inviterGuid) && inviterGuid != CurrentUserId)
            {
                try
                {
                    await _notificationServices.PublishAsync(new NotificationDto
                    {
                        UserId = inviterGuid,
                        Type = "ProjectInvitationDeclined",
                        Title = $"Lời mời tham gia tổ chức {orgName} đã bị từ chối",
                        Content = $"{responderName} đã từ chối lời mời tham gia tổ chức \"{orgName}\".",
                        EntityType = "Organization",
                        EntityId = Guid.TryParse(orgId, out var parsedOrgId) ? parsedOrgId : null,
                        ActorId = CurrentUserId,
                        SendEmail = false
                    });
                }
                catch { }
            }
        }

        return NoContent();
    }

    #endregion

    #region 5. Link mời (Invite Links)

    [HttpGet("api/organizations/{orgId:guid}/invite-link")]
    public async Task<IActionResult> GetInviteLink(Guid orgId)
    {
        await EnsureConnectionOpenAsync();

        await using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT Token, OrgId, Role, CreatedAt
            FROM OrganizationInviteLink
            WHERE OrgId = @orgId AND IsActive = 1
            ORDER BY CreatedAt DESC
            LIMIT 1;";
        cmd.Parameters.AddWithValue("@orgId", orgId.ToString());

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return Ok(new InviteLinkDto
            {
                Token = AsString(reader.GetValue(0)),
                OrgId = AsString(reader.GetValue(1)),
                Role = AsString(reader.GetValue(2)),
                CreatedAt = reader.GetDateTime(3).ToString("O")
            });
        }

        return Ok(null);
    }

    [HttpPost("api/organizations/{orgId:guid}/invite-link")]
    public async Task<IActionResult> RegenerateInviteLink(Guid orgId, [FromBody] UpdateOrgMemberRoleRequestDto request)
    {
        await EnsureConnectionOpenAsync();

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        await using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            UPDATE OrganizationInviteLink SET IsActive = 0 WHERE OrgId = @orgId;
            INSERT INTO OrganizationInviteLink (Token, OrgId, Role, CreatedByUserId, CreatedAt, IsActive)
            VALUES (@token, @orgId, @role, @userId, UTC_TIMESTAMP(6), 1);";
        cmd.Parameters.AddWithValue("@token", token);
        cmd.Parameters.AddWithValue("@orgId", orgId.ToString());
        cmd.Parameters.AddWithValue("@role", string.IsNullOrWhiteSpace(request.Role) ? "Member" : request.Role);
        cmd.Parameters.AddWithValue("@userId", CurrentUserId.ToString());
        await cmd.ExecuteNonQueryAsync();

        return Ok(new InviteLinkDto
        {
            Token = token,
            OrgId = orgId.ToString(),
            Role = string.IsNullOrWhiteSpace(request.Role) ? "Member" : request.Role,
            CreatedAt = DateTime.UtcNow.ToString("O")
        });
    }

    [HttpDelete("api/organizations/{orgId:guid}/invite-link")]
    public async Task<IActionResult> DisableInviteLink(Guid orgId)
    {
        await EnsureConnectionOpenAsync();

        await using var cmd = _db.CreateCommand();
        cmd.CommandText = "UPDATE OrganizationInviteLink SET IsActive = 0 WHERE OrgId = @orgId;";
        cmd.Parameters.AddWithValue("@orgId", orgId.ToString());
        await cmd.ExecuteNonQueryAsync();

        return NoContent();
    }

    [HttpGet("api/invite-links/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> PreviewInviteLink(string token)
    {
        await EnsureConnectionOpenAsync();

        string resolvedToken;
        string orgId;
        string orgName;
        string role;
        string invitedByName;

        await using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT oil.Token, oil.OrgId, o.Name AS OrgName, oil.Role,
                       COALESCE(up.DisplayName, u.Email, 'Hệ thống') AS InvitedByName
                FROM OrganizationInviteLink oil
                JOIN Organization o ON oil.OrgId = o.Id
                LEFT JOIN User u ON oil.CreatedByUserId = u.Id
                LEFT JOIN UserProfile up ON u.Id = up.UserId
                WHERE oil.Token = @token AND oil.IsActive = 1 AND o.IsActive = 1 LIMIT 1;";
            cmd.Parameters.AddWithValue("@token", token);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return NotFound(new { title = "Link mời không hợp lệ hoặc đã hết hạn." });
            }

            resolvedToken = AsString(reader.GetValue(0));
            orgId = AsString(reader.GetValue(1));
            orgName = AsString(reader.GetValue(2));
            role = AsString(reader.GetValue(3));
            invitedByName = AsString(reader.GetValue(4));
        }

        var alreadyMember = false;
        if (CurrentUserId != Guid.Empty)
        {
            await using var checkCmd = _db.CreateCommand();
            checkCmd.CommandText = @"
                SELECT 1 FROM Organization WHERE Id = @orgId AND OwnerId = @userId
                UNION
                SELECT 1 FROM OrganizationMember WHERE OrgId = @orgId AND UserId = @userId
                LIMIT 1;";
            checkCmd.Parameters.AddWithValue("@orgId", orgId);
            checkCmd.Parameters.AddWithValue("@userId", CurrentUserId.ToString());
            alreadyMember = await checkCmd.ExecuteScalarAsync() != null;
        }

        return Ok(new InviteLinkPreviewDto
        {
            Token = resolvedToken,
            OrgId = orgId,
            OrgName = orgName,
            Role = role,
            InvitedByName = invitedByName,
            AlreadyMember = alreadyMember
        });
    }

    [HttpPost("api/invite-links/{token}/accept")]
    public async Task<IActionResult> AcceptInviteLink(string token)
    {
        if (CurrentUserId == Guid.Empty) return Unauthorized();
        await EnsureConnectionOpenAsync();

        string orgId;
        string role;
        string orgName;
        string ownerId;

        await using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT oil.OrgId, oil.Role, o.Name, o.OwnerId
                FROM OrganizationInviteLink oil
                JOIN Organization o ON oil.OrgId = o.Id
                WHERE oil.Token = @token AND oil.IsActive = 1 AND o.IsActive = 1 LIMIT 1;";
            cmd.Parameters.AddWithValue("@token", token);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return NotFound(new { title = "Link mời không hợp lệ hoặc đã hết hạn." });
            }
            orgId = AsString(reader.GetValue(0));
            role = AsString(reader.GetValue(1));
            orgName = AsString(reader.GetValue(2));
            ownerId = AsString(reader.GetValue(3));
        }

        await using (var insertCmd = _db.CreateCommand())
        {
            insertCmd.CommandText = @"
                INSERT INTO OrganizationMember (Id, OrgId, UserId, Role, JoinedAt)
                VALUES (@id, @orgId, @userId, @role, UTC_TIMESTAMP(6))
                ON DUPLICATE KEY UPDATE Role = VALUES(Role);

                UPDATE OrganizationInvitation oi
                JOIN User u ON LOWER(oi.Email) = LOWER(u.Email)
                SET oi.Status = 'Accepted', oi.RespondedAt = UTC_TIMESTAMP(6)
                WHERE oi.OrgId = @orgId AND u.Id = @userId AND oi.Status = 'Pending';";
            insertCmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
            insertCmd.Parameters.AddWithValue("@orgId", orgId);
            insertCmd.Parameters.AddWithValue("@userId", CurrentUserId.ToString());
            insertCmd.Parameters.AddWithValue("@role", role);
            await insertCmd.ExecuteNonQueryAsync();
        }

        if (Guid.TryParse(ownerId, out var ownerGuid) && ownerGuid != CurrentUserId)
        {
            try
            {
                string responderName = "Thành viên mới";
                await using (var uCmd = _db.CreateCommand())
                {
                    uCmd.CommandText = "SELECT COALESCE(up.DisplayName, u.Email, 'Thành viên mới') FROM User u LEFT JOIN UserProfile up ON u.Id = up.UserId WHERE u.Id = @userId LIMIT 1;";
                    uCmd.Parameters.AddWithValue("@userId", CurrentUserId.ToString());
                    var rObj = await uCmd.ExecuteScalarAsync();
                    if (rObj != null) responderName = rObj.ToString()!;
                }

                await _notificationServices.PublishAsync(new NotificationDto
                {
                    UserId = ownerGuid,
                    Type = "ProjectInvitationAccepted",
                    Title = $"Thành viên mới tham gia tổ chức {orgName}",
                    Content = $"{responderName} đã tham gia tổ chức \"{orgName}\" qua liên kết mời.",
                    EntityType = "Organization",
                    EntityId = Guid.TryParse(orgId, out var parsedOrgId) ? parsedOrgId : null,
                    ActorId = CurrentUserId,
                    SendEmail = false
                });
            }
            catch { }
        }

        return Ok(new OrgMembershipDto
        {
            OrgId = orgId,
            OrgName = orgName,
            OwnerId = ownerId,
            Role = role,
            JoinedAt = DateTime.UtcNow.ToString("O"),
            MemberCount = 1
        });
    }

    #endregion

    #region 6. Mẫu quy trình (Workflow Templates)

    private static List<WorkflowTemplateDto> GetSystemPresets()
    {
        return
        [
            new()
            {
                Id = "preset-scrum-std",
                OrgId = null,
                Name = "Scrum chuẩn",
                ManagementType = "Scrum",
                IsPreset = true,
                IsDefault = true,
                Statuses =
                [
                    new() { Name = "To Do", Category = "ToDo" },
                    new() { Name = "In Progress", Category = "InProgress" },
                    new() { Name = "Code Review", Category = "InProgress" },
                    new() { Name = "Done", Category = "Done" }
                ]
            },
            new()
            {
                Id = "preset-agile-qa",
                OrgId = null,
                Name = "Agile + QA",
                ManagementType = "Scrum",
                IsPreset = true,
                IsDefault = false,
                Statuses =
                [
                    new() { Name = "To Do", Category = "ToDo" },
                    new() { Name = "In Progress", Category = "InProgress" },
                    new() { Name = "Code Review", Category = "InProgress" },
                    new() { Name = "QA Testing", Category = "InProgress" },
                    new() { Name = "Done", Category = "Done" }
                ]
            },
            new()
            {
                Id = "preset-kanban-simple",
                OrgId = null,
                Name = "Kanban đơn giản",
                ManagementType = "Kanban",
                IsPreset = true,
                IsDefault = false,
                Statuses =
                [
                    new() { Name = "To Do", Category = "ToDo" },
                    new() { Name = "In Progress", Category = "InProgress" },
                    new() { Name = "Done", Category = "Done" }
                ]
            },
            new()
            {
                Id = "preset-enterprise",
                OrgId = null,
                Name = "Enterprise",
                ManagementType = "Scrum",
                IsPreset = true,
                IsDefault = false,
                Statuses =
                [
                    new() { Name = "Backlog", Category = "ToDo" },
                    new() { Name = "Spec Review", Category = "InProgress" },
                    new() { Name = "In Progress", Category = "InProgress" },
                    new() { Name = "Code Review", Category = "InProgress" },
                    new() { Name = "QA Verification", Category = "InProgress" },
                    new() { Name = "Done", Category = "Done" }
                ]
            }
        ];
    }

    [HttpGet("api/organizations/{orgId:guid}/workflow-templates")]
    public async Task<IActionResult> GetWorkflowTemplates(Guid orgId)
    {
        await EnsureConnectionOpenAsync();

        var templates = new List<WorkflowTemplateDto>();

        await using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT CAST(Id AS CHAR(64)) AS Id, CAST(OrgId AS CHAR(64)) AS OrgId, Name, ManagementType, StatusesJson, IsDefault, IsPreset
            FROM organizationworkflowtemplate
            WHERE IsActive = 1 AND (OrgId IS NULL OR OrgId = @orgId)
            ORDER BY IsPreset DESC, CreatedAt ASC;";
        cmd.Parameters.AddWithValue("@orgId", orgId.ToString());

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var statusesJson = AsString(reader.GetValue(4));
            List<WorkflowStatusDefDto>? statuses = null;
            try
            {
                statuses = JsonSerializer.Deserialize<List<WorkflowStatusDefDto>>(statusesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { }

            var isPreset = !reader.IsDBNull(6) && Convert.ToBoolean(reader.GetValue(6));

            templates.Add(new WorkflowTemplateDto
            {
                Id = AsString(reader.GetValue(0)),
                OrgId = reader.IsDBNull(1) ? null : AsString(reader.GetValue(1)),
                Name = AsString(reader.GetValue(2)),
                ManagementType = AsString(reader.GetValue(3)),
                Statuses = statuses ?? [],
                IsPreset = isPreset,
                IsDefault = reader.GetBoolean(5)
            });
        }

        var presets = GetSystemPresets();
        foreach (var p in presets)
        {
            if (!templates.Any(t => t.Id == p.Id || t.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase)))
            {
                templates.Add(p);
            }
        }

        if (!templates.Any(t => t.IsDefault) && templates.Count > 0)
        {
            templates[0].IsDefault = true;
        }

        return Ok(templates);
    }

    [HttpPost("api/organizations/{orgId:guid}/workflow-templates")]
    public async Task<IActionResult> CreateWorkflowTemplate(Guid orgId, [FromBody] SaveWorkflowTemplateRequestDto request)
    {
        await EnsureConnectionOpenAsync();

        var id = Guid.NewGuid().ToString();
        var statusesJson = JsonSerializer.Serialize(request.Statuses);

        await using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO OrganizationWorkflowTemplate (Id, OrgId, Name, ManagementType, StatusesJson, IsDefault, CreatedAt)
            VALUES (@id, @orgId, @name, @type, @json, 0, UTC_TIMESTAMP(6));";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@orgId", orgId.ToString());
        cmd.Parameters.AddWithValue("@name", request.Name);
        cmd.Parameters.AddWithValue("@type", request.ManagementType);
        cmd.Parameters.AddWithValue("@json", statusesJson);
        await cmd.ExecuteNonQueryAsync();

        return StatusCode(StatusCodes.Status201Created, new WorkflowTemplateDto
        {
            Id = id,
            OrgId = orgId.ToString(),
            Name = request.Name,
            ManagementType = request.ManagementType,
            Statuses = request.Statuses,
            IsPreset = false,
            IsDefault = false
        });
    }

    [HttpPut("api/organizations/{orgId:guid}/workflow-templates/{templateId}")]
    public async Task<IActionResult> UpdateWorkflowTemplate(Guid orgId, string templateId, [FromBody] SaveWorkflowTemplateRequestDto request)
    {
        await EnsureConnectionOpenAsync();

        var statusesJson = JsonSerializer.Serialize(request.Statuses);
        await using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            UPDATE OrganizationWorkflowTemplate
            SET Name = @name, ManagementType = @type, StatusesJson = @json, UpdatedAt = UTC_TIMESTAMP(6)
            WHERE Id = @id AND OrgId = @orgId;";
        cmd.Parameters.AddWithValue("@id", templateId);
        cmd.Parameters.AddWithValue("@orgId", orgId.ToString());
        cmd.Parameters.AddWithValue("@name", request.Name);
        cmd.Parameters.AddWithValue("@type", request.ManagementType);
        cmd.Parameters.AddWithValue("@json", statusesJson);
        await cmd.ExecuteNonQueryAsync();

        return Ok(new WorkflowTemplateDto
        {
            Id = templateId,
            OrgId = orgId.ToString(),
            Name = request.Name,
            ManagementType = request.ManagementType,
            Statuses = request.Statuses,
            IsPreset = false,
            IsDefault = false
        });
    }

    [HttpDelete("api/organizations/{orgId:guid}/workflow-templates/{templateId}")]
    public async Task<IActionResult> DeleteWorkflowTemplate(Guid orgId, string templateId)
    {
        await EnsureConnectionOpenAsync();

        await using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM OrganizationWorkflowTemplate WHERE Id = @id AND OrgId = @orgId;";
        cmd.Parameters.AddWithValue("@id", templateId);
        cmd.Parameters.AddWithValue("@orgId", orgId.ToString());
        await cmd.ExecuteNonQueryAsync();

        return NoContent();
    }

    [HttpPut("api/organizations/{orgId:guid}/workflow-templates/{templateId}/default")]
    public async Task<IActionResult> SetDefaultWorkflowTemplate(Guid orgId, string templateId)
    {
        await EnsureConnectionOpenAsync();

        await using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            UPDATE OrganizationWorkflowTemplate
            SET IsDefault = CASE WHEN Id = @id THEN 1 ELSE 0 END
            WHERE OrgId = @orgId;";
        cmd.Parameters.AddWithValue("@id", templateId);
        cmd.Parameters.AddWithValue("@orgId", orgId.ToString());
        await cmd.ExecuteNonQueryAsync();

        return Ok(new WorkflowTemplateDto { Id = templateId, IsDefault = true });
    }

    #endregion
}
