namespace ProjectMgmt.Solution.Services.Ai;

using System.Text.Encodings.Web;
using System.Text.Json;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Solution.Dtos.Ai;

public interface IAiConversationSessionStore
{
    /// <summary>
    /// Nạp phiên làm việc từ DB khi khóa cứng khớp tuyệt đối (OrganizationId, ProjectId, UserId).
    /// Trả về null nếu không tồn tại hoặc khác quyền sở hữu.
    /// </summary>
    Task<AiSprintProposalDto?> GetScopedSessionAsync(
        string sessionId,
        IProjectScopeContext scope,
        CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra quyền sở hữu khi người dùng mở trực tiếp proposal/{sessionId}.
    /// </summary>
    Task<(AiSprintProposalDto? Proposal, bool Forbidden)> GetProposalWithOwnershipCheckAsync(
        string sessionId,
        Guid currentUserId,
        Guid expectedOrgId,
        CancellationToken ct = default);

    /// <summary>
    /// Lưu bền vững toàn bộ hội thoại đa lượt, bản đề xuất và chỉ mục trí nhớ (#mem-X) xuống MySQL theo biên (Org, Project, User).
    /// </summary>
    Task SaveSessionStateAsync(
        IProjectScopeContext scope,
        string actorName,
        string model,
        AiSprintProposalDto proposal,
        CancellationToken ct = default);

    /// <summary>
    /// Liệt kê các phiên hội thoại thực tế của đúng (OrganizationId, ProjectId, UserId).
    /// </summary>
    Task<List<AiSessionSummary>> ListUserProjectSessionsAsync(
        IProjectScopeContext scope,
        int limit = 25,
        CancellationToken ct = default);

    /// <summary>
    /// Xóa một phiên làm việc cũ sau khi xác nhận khóa sở hữu (OrganizationId, ProjectId, UserId).
    /// </summary>
    Task<(bool Found, bool Forbidden)> DeleteSessionAsync(
        string sessionId,
        IProjectScopeContext scope,
        CancellationToken ct = default);
}

public sealed class AiConversationSessionStore : IAiConversationSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly DeliveryIntelligenceDbContext _deliveryDb;
    private readonly ILogger<AiConversationSessionStore> _logger;

    public AiConversationSessionStore(
        DeliveryIntelligenceDbContext deliveryDb,
        ILogger<AiConversationSessionStore> logger)
    {
        _deliveryDb = deliveryDb;
        _logger = logger;
    }

    public async Task<AiSprintProposalDto?> GetScopedSessionAsync(
        string sessionId,
        IProjectScopeContext scope,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId) ||
            scope.OrganizationId == Guid.Empty ||
            scope.ProjectId == Guid.Empty ||
            scope.UserId == Guid.Empty)
        {
            return null;
        }

        await AiConversationSchemaBootstrapper.EnsureTablesExistAsync(_deliveryDb, ct);

        var entity = await _deliveryDb.AiConversationSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.SessionId == sessionId &&
                s.OrganizationId == scope.OrganizationId &&
                s.ProjectId == scope.ProjectId &&
                s.UserId == scope.UserId &&
                !s.IsDeleted,
                ct);

        if (entity == null)
        {
            return null;
        }

        var memories = await _deliveryDb.AiConversationMemoryEntries
            .AsNoTracking()
            .Where(m =>
                m.ConversationSessionId == entity.Id &&
                m.OrganizationId == scope.OrganizationId &&
                m.ProjectId == scope.ProjectId &&
                m.UserId == scope.UserId)
            .OrderBy(m => m.TurnIndex)
            .ThenBy(m => m.CreatedAtUtc)
            .ToListAsync(ct);

        return HydrateProposal(entity, memories);
    }

    public async Task<(AiSprintProposalDto? Proposal, bool Forbidden)> GetProposalWithOwnershipCheckAsync(
        string sessionId,
        Guid currentUserId,
        Guid expectedOrgId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return (null, false);
        }

        await AiConversationSchemaBootstrapper.EnsureTablesExistAsync(_deliveryDb, ct);

        var entity = await _deliveryDb.AiConversationSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && !s.IsDeleted, ct);

        if (entity == null)
        {
            return (null, false);
        }

        if (entity.UserId != currentUserId ||
            (expectedOrgId != Guid.Empty && entity.OrganizationId != expectedOrgId))
        {
            return (null, true);
        }

        var memories = await _deliveryDb.AiConversationMemoryEntries
            .AsNoTracking()
            .Where(m =>
                m.ConversationSessionId == entity.Id &&
                m.OrganizationId == entity.OrganizationId &&
                m.ProjectId == entity.ProjectId &&
                m.UserId == entity.UserId)
            .OrderBy(m => m.TurnIndex)
            .ThenBy(m => m.CreatedAtUtc)
            .ToListAsync(ct);

        return (HydrateProposal(entity, memories), false);
    }

    public async Task SaveSessionStateAsync(
        IProjectScopeContext scope,
        string actorName,
        string model,
        AiSprintProposalDto proposal,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(proposal.SessionId) ||
            scope.OrganizationId == Guid.Empty ||
            scope.ProjectId == Guid.Empty ||
            scope.UserId == Guid.Empty)
        {
            return;
        }

        await AiConversationSchemaBootstrapper.EnsureTablesExistAsync(_deliveryDb, ct);

        proposal.OrganizationId = scope.OrganizationId;
        proposal.ProjectId = scope.ProjectId;
        proposal.UserId = scope.UserId;

        var existing = await _deliveryDb.AiConversationSessions
            .Include(s => s.Memories)
            .FirstOrDefaultAsync(s => s.SessionId == proposal.SessionId, ct);

        if (existing != null)
        {
            if (existing.OrganizationId != scope.OrganizationId ||
                existing.ProjectId != scope.ProjectId ||
                existing.UserId != scope.UserId)
            {
                _logger.LogWarning(
                    "[SessionStore] Từ chối ghi đè chéo phiên {SessionId}. Chủ sở hữu ({OwnerOrg}, {OwnerProj}, {OwnerUser}) != Người gọi ({CallOrg}, {CallProj}, {CallUser})",
                    proposal.SessionId, existing.OrganizationId, existing.ProjectId, existing.UserId,
                    scope.OrganizationId, scope.ProjectId, scope.UserId);
                throw new UnauthorizedAccessException("Không có quyền ghi đè phiên làm việc AI của dự án, tổ chức hoặc người dùng khác.");
            }
        }

        var now = DateTime.UtcNow;
        var title = Cap(AiIndexedMemoryEngine.BuildSessionTitle(proposal), 290);
        var historyJson = JsonSerializer.Serialize(proposal.ConversationHistory ?? [], JsonOptions);
        var proposalJson = JsonSerializer.Serialize(proposal, JsonOptions);
        var effectiveModel = Cap(string.IsNullOrWhiteSpace(model) ? "Gemini 3.6 Flash" : model, 95);
        var safeActorName = Cap(string.IsNullOrWhiteSpace(actorName) ? "Thành viên" : actorName, 145);

        if (existing == null)
        {
            existing = new AiConversationSession
            {
                Id = Guid.NewGuid(),
                SessionId = Cap(proposal.SessionId, 115),
                OrganizationId = scope.OrganizationId,
                ProjectId = scope.ProjectId,
                UserId = scope.UserId,
                CreatedByName = safeActorName,
                Title = title,
                Model = effectiveModel,
                LastIntent = Cap(proposal.Intent ?? "sprint_plan", 48),
                RecommendedTarget = Cap(proposal.RecommendedTarget ?? "sprint", 28),
                TargetSprintId = proposal.TargetSprintId,
                TurnCount = proposal.ConversationHistory?.Count ?? 0,
                TaskCount = proposal.Tasks?.Count ?? 0,
                TotalStoryPoints = proposal.TotalStoryPoints,
                MemoryCount = proposal.IndexedMemories?.Count ?? 0,
                ConversationHistoryJson = historyJson,
                ProposalJson = proposalJson,
                IsDeleted = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            _deliveryDb.AiConversationSessions.Add(existing);
        }
        else
        {
            existing.IsDeleted = false;
            existing.DeletedAtUtc = null;
            existing.Title = title;
            if (!string.IsNullOrWhiteSpace(model))
            {
                existing.Model = effectiveModel;
            }
            existing.LastIntent = Cap(proposal.Intent ?? existing.LastIntent, 48);
            existing.RecommendedTarget = Cap(proposal.RecommendedTarget ?? existing.RecommendedTarget, 28);
            existing.TargetSprintId = proposal.TargetSprintId;
            existing.TurnCount = proposal.ConversationHistory?.Count ?? existing.TurnCount;
            existing.TaskCount = proposal.Tasks?.Count ?? 0;
            existing.TotalStoryPoints = proposal.TotalStoryPoints;
            existing.MemoryCount = proposal.IndexedMemories?.Count ?? 0;
            existing.ConversationHistoryJson = historyJson;
            existing.ProposalJson = proposalJson;
            existing.UpdatedAtUtc = now;

            if (existing.Memories.Count > 0)
            {
                _deliveryDb.AiConversationMemoryEntries.RemoveRange(existing.Memories);
                existing.Memories.Clear();
            }
        }

        if (proposal.IndexedMemories != null && proposal.IndexedMemories.Count > 0)
        {
            foreach (var mem in proposal.IndexedMemories)
            {
                var entry = new AiConversationMemoryEntry
                {
                    Id = Guid.NewGuid(),
                    ConversationSessionId = existing.Id,
                    SessionId = Cap(proposal.SessionId, 115),
                    OrganizationId = scope.OrganizationId,
                    ProjectId = scope.ProjectId,
                    UserId = scope.UserId,
                    AnchorKey = Cap(mem.AnchorKey, 30),
                    TurnIndex = mem.TurnIndex,
                    Category = Cap(mem.Category, 48),
                    Title = Cap(mem.Title, 210),
                    Keywords = Cap(mem.Keywords, 490),
                    Summary = Cap(mem.Summary, 590),
                    ContentJson = JsonSerializer.Serialize(new { detail = mem.DetailContent }, JsonOptions),
                    CreatedAtUtc = mem.CreatedAtUtc == default ? now : mem.CreatedAtUtc
                };
                _deliveryDb.AiConversationMemoryEntries.Add(entry);
            }
        }

        await _deliveryDb.SaveChangesAsync(ct);
    }

    private static string Cap(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public async Task<List<AiSessionSummary>> ListUserProjectSessionsAsync(
        IProjectScopeContext scope,
        int limit = 25,
        CancellationToken ct = default)
    {
        if (scope.OrganizationId == Guid.Empty || scope.ProjectId == Guid.Empty || scope.UserId == Guid.Empty)
        {
            return [];
        }

        await AiConversationSchemaBootstrapper.EnsureTablesExistAsync(_deliveryDb, ct);

        var rows = await _deliveryDb.AiConversationSessions
            .AsNoTracking()
            .Where(s =>
                s.OrganizationId == scope.OrganizationId &&
                s.ProjectId == scope.ProjectId &&
                s.UserId == scope.UserId &&
                !s.IsDeleted)
            .OrderByDescending(s => s.UpdatedAtUtc)
            .Take(Math.Clamp(limit, 1, 50))
            .Select(s => new AiSessionSummary(
                s.SessionId,
                s.ProjectId,
                s.UserId,
                s.CreatedByName,
                s.Title,
                s.TaskCount,
                s.TotalStoryPoints,
                s.Model,
                s.CreatedAtUtc,
                s.Title,
                s.TurnCount,
                s.MemoryCount,
                s.UpdatedAtUtc,
                s.LastIntent))
            .ToListAsync(ct);

        return rows;
    }

    public async Task<(bool Found, bool Forbidden)> DeleteSessionAsync(
        string sessionId,
        IProjectScopeContext scope,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId) ||
            scope.OrganizationId == Guid.Empty ||
            scope.ProjectId == Guid.Empty ||
            scope.UserId == Guid.Empty)
        {
            return (false, false);
        }

        await AiConversationSchemaBootstrapper.EnsureTablesExistAsync(_deliveryDb, ct);

        var entity = await _deliveryDb.AiConversationSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && !s.IsDeleted, ct);

        if (entity == null)
        {
            return (false, false);
        }

        if (entity.OrganizationId != scope.OrganizationId ||
            entity.ProjectId != scope.ProjectId ||
            entity.UserId != scope.UserId)
        {
            return (true, true);
        }

        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _deliveryDb.SaveChangesAsync(ct);

        return (true, false);
    }

    private AiSprintProposalDto HydrateProposal(
        AiConversationSession entity,
        IReadOnlyList<AiConversationMemoryEntry> memories)
    {
        AiSprintProposalDto? proposal = null;
        try
        {
            proposal = JsonSerializer.Deserialize<AiSprintProposalDto>(entity.ProposalJson, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SessionStore] Không thể parse ProposalJson cho session {SessionId}", entity.SessionId);
        }

        proposal ??= new AiSprintProposalDto
        {
            SessionId = entity.SessionId,
            ProjectId = entity.ProjectId,
            TargetSprintId = entity.TargetSprintId,
            SprintGoal = entity.Title,
            RecommendedTarget = entity.RecommendedTarget,
            Intent = entity.LastIntent,
            CreatedAtUtc = entity.CreatedAtUtc
        };

        proposal.OrganizationId = entity.OrganizationId;
        proposal.ProjectId = entity.ProjectId;
        proposal.UserId = entity.UserId;
        proposal.SessionId = entity.SessionId;

        if ((proposal.ConversationHistory == null || proposal.ConversationHistory.Count == 0) &&
            !string.IsNullOrWhiteSpace(entity.ConversationHistoryJson))
        {
            try
            {
                proposal.ConversationHistory = JsonSerializer.Deserialize<List<AiConversationTurnDto>>(entity.ConversationHistoryJson, JsonOptions) ?? [];
            }
            catch
            {
                proposal.ConversationHistory = [];
            }
        }

        if (memories.Count > 0)
        {
            proposal.IndexedMemories = memories.Select(m => new AiMemoryAnchorDto
            {
                AnchorKey = m.AnchorKey,
                TurnIndex = m.TurnIndex,
                Category = m.Category,
                Title = m.Title,
                Keywords = m.Keywords,
                Summary = m.Summary,
                DetailContent = ExtractDetailContent(m.ContentJson),
                CreatedAtUtc = m.CreatedAtUtc
            }).ToList();
        }

        return proposal;
    }

    private static string ExtractDetailContent(string contentJson)
    {
        if (string.IsNullOrWhiteSpace(contentJson)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(contentJson);
            if (doc.RootElement.TryGetProperty("detail", out var detailProp))
            {
                return detailProp.GetString() ?? string.Empty;
            }
        }
        catch
        {
            // Fallback raw string
        }
        return contentJson;
    }
}
