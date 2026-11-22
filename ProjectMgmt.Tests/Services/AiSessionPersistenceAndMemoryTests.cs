namespace ProjectMgmt.Tests.Services;

using DeliveryIntelligence.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Solution.Dtos.Ai;
using ProjectMgmt.Solution.Services.Ai;
using Xunit;

public class AiSessionPersistenceAndMemoryTests
{
    private sealed class TestDeliveryDbContext(DbContextOptions<DeliveryIntelligenceDbContext> options)
        : DeliveryIntelligenceDbContext(options) { }

    private static TestDeliveryDbContext CreateDeliveryDb()
        => new(new DbContextOptionsBuilder<DeliveryIntelligenceDbContext>()
            .UseInMemoryDatabase($"AiSessStore_{Guid.NewGuid():N}")
            .Options);

    private static AiSprintProposalDto BuildSampleProposal(string sessionId, Guid projectId, string goal) => new()
    {
        SessionId = sessionId,
        ProjectId = projectId,
        SprintGoal = goal,
        Intent = "sprint_plan",
        RecommendedTarget = "sprint",
        TotalStoryPoints = 8,
        TotalEstimatedHours = 20,
        Tasks =
        [
            new ProposedTaskDto
            {
                Id = "t1",
                Title = "Xây dựng API đồng bộ",
                Type = "Task",
                Priority = "High",
                Status = "To Do",
                StoryPoints = 8,
                EstimatedHours = 20
            }
        ],
        ConversationHistory =
        [
            new AiConversationTurnDto
            {
                Id = "u1",
                Role = "user",
                Content = "Lập kế hoạch đồng bộ dữ liệu"
            },
            new AiConversationTurnDto
            {
                Id = "a1",
                Role = "assistant",
                Content = "Đã lập 1 hạng mục công việc (8 SP).",
                Intent = "sprint_plan",
                ThinkingSummary = "Đã suy nghĩ xong trong 1.5s · 3 thao tác",
                DurationMs = 1500,
                ReferencedMemoryAnchors = ["#mem-1"],
                TraceItems =
                [
                    new AiTurnTraceItemDto
                    {
                        Kind = "mcp",
                        ActionVerb = "Truy vấn MCP",
                        Target = "Cấu hình dự án",
                        Summary = "Đọc workflow áp dụng",
                        Detail = "Trạng thái áp dụng của dự án",
                        Status = "done",
                        DurationMs = 32
                    }
                ]
            }
        ],
        IndexedMemories =
        [
            new AiMemoryAnchorDto
            {
                AnchorKey = "#mem-1",
                TurnIndex = 1,
                Category = "sprint_plan",
                Title = "Kế hoạch đồng bộ dữ liệu",
                Summary = "Đề xuất 1 Task (8 SP) cho API đồng bộ.",
                DetailContent = "Đề xuất 1 Task (8 SP) cho API đồng bộ.",
                Keywords = "dong, bo, api"
            }
        ]
    };

    [Fact]
    public async Task SaveAndListSessions_ShouldEnforceStrictOrgProjectUserScope()
    {
        await using var db = CreateDeliveryDb();
        var store = new AiConversationSessionStore(db, NullLogger<AiConversationSessionStore>.Instance);

        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var proj1 = Guid.NewGuid();
        var proj2 = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        var scopeOwner = new ProjectScopeContext(orgA, proj1, user1);
        await store.SaveSessionStateAsync(scopeOwner, "User Một", "Gemini 3.6 Flash", BuildSampleProposal("sprint_ai_owner", proj1, "Sprint Owner"));
        await store.SaveSessionStateAsync(new ProjectScopeContext(orgA, proj1, user2), "User Hai", "Gemini 3.6 Flash", BuildSampleProposal("sprint_ai_other_user", proj1, "Sprint User 2"));
        await store.SaveSessionStateAsync(new ProjectScopeContext(orgA, proj2, user1), "User Một", "Gemini 3.6 Flash", BuildSampleProposal("sprint_ai_other_proj", proj2, "Sprint Proj 2"));
        await store.SaveSessionStateAsync(new ProjectScopeContext(orgB, proj1, user1), "User Một", "Gemini 3.6 Flash", BuildSampleProposal("sprint_ai_other_org", proj1, "Sprint Org B"));

        var list = await store.ListUserProjectSessionsAsync(scopeOwner);

        var only = Assert.Single(list);
        Assert.Equal("sprint_ai_owner", only.SessionId);
        Assert.Equal(2, only.TurnCount);
        Assert.Equal(1, only.MemoryCount);
        Assert.Equal("sprint_plan", only.LastIntent);
        Assert.False(string.IsNullOrWhiteSpace(only.Title));
    }

    [Fact]
    public async Task GetScopedSessionAndOwnershipCheck_ShouldBlockCrossScopeAndHydrateMemories()
    {
        await using var db = CreateDeliveryDb();
        var store = new AiConversationSessionStore(db, NullLogger<AiConversationSessionStore>.Instance);

        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var projA = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var ownerScope = new ProjectScopeContext(orgA, projA, owner);

        await store.SaveSessionStateAsync(ownerScope, "Hoàng", "Gemini 3.6 Flash", BuildSampleProposal("sprint_ai_restore_01", projA, "Mục tiêu khôi phục"));

        // Chủ sở hữu hợp lệ -> khôi phục nguyên vẹn lịch sử, trace và #mem-1
        var restored = await store.GetScopedSessionAsync("sprint_ai_restore_01", ownerScope);
        Assert.NotNull(restored);
        Assert.Equal(orgA, restored!.OrganizationId);
        Assert.Equal(owner, restored.UserId);
        Assert.Equal(2, restored.ConversationHistory.Count);
        Assert.Single(restored.IndexedMemories);
        Assert.Equal("#mem-1", restored.IndexedMemories[0].AnchorKey);
        var assistantTurn = restored.ConversationHistory.Last(t => t.Role == "assistant");
        Assert.Single(assistantTurn.TraceItems);
        Assert.Equal("Đã suy nghĩ xong trong 1.5s · 3 thao tác", assistantTurn.ThinkingSummary);

        // Sai user / sai org / sai project -> bị khóa cứng
        Assert.Null(await store.GetScopedSessionAsync("sprint_ai_restore_01", new ProjectScopeContext(orgA, projA, intruder)));
        Assert.Null(await store.GetScopedSessionAsync("sprint_ai_restore_01", new ProjectScopeContext(orgB, projA, owner)));
        Assert.Null(await store.GetScopedSessionAsync("sprint_ai_restore_01", new ProjectScopeContext(orgA, Guid.NewGuid(), owner)));

        var (_, intruderForbidden) = await store.GetProposalWithOwnershipCheckAsync("sprint_ai_restore_01", intruder, orgA);
        Assert.True(intruderForbidden);

        var (_, orgForbidden) = await store.GetProposalWithOwnershipCheckAsync("sprint_ai_restore_01", owner, orgB);
        Assert.True(orgForbidden);
    }

    [Fact]
    public async Task DeleteSessionAsync_ShouldBlockForeignUserAndDeleteOwnedSessionCleanly()
    {
        await using var db = CreateDeliveryDb();
        var store = new AiConversationSessionStore(db, NullLogger<AiConversationSessionStore>.Instance);

        var org = Guid.NewGuid();
        var proj = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var ownerScope = new ProjectScopeContext(org, proj, owner);

        await store.SaveSessionStateAsync(ownerScope, "Hoàng", "Gemini 3.6 Flash", BuildSampleProposal("sprint_ai_del_01", proj, "Phiên cần xóa"));

        // Người dùng khác không được phép xóa
        var (intruderFound, intruderForbidden) = await store.DeleteSessionAsync("sprint_ai_del_01", new ProjectScopeContext(org, proj, intruder));
        Assert.True(intruderFound);
        Assert.True(intruderForbidden);
        Assert.NotNull(await store.GetScopedSessionAsync("sprint_ai_del_01", ownerScope));

        // Chính chủ xóa thành công và biến mất khỏi danh sách phiên
        var (ownerFound, ownerForbidden) = await store.DeleteSessionAsync("sprint_ai_del_01", ownerScope);
        Assert.True(ownerFound);
        Assert.False(ownerForbidden);
        Assert.Empty(await store.ListUserProjectSessionsAsync(ownerScope));
        Assert.Null(await store.GetScopedSessionAsync("sprint_ai_del_01", ownerScope));
    }
}
