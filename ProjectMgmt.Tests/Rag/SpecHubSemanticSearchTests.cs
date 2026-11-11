using System.Text.Json;
using DeliveryIntelligence.Infrastructure.Services.Rag;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Planning.Domain.Entities;
using Planning.Infrastructure;
using ProjectMgmt.Core.Security;
using ProjectMgmt.Core.VectorSearch;
using ProjectMgmt.Solution.McpServers;
using ProjectMgmt.Solution.Services.Ai.Rag;
using Xunit;

namespace ProjectMgmt.Tests.Rag;

public class SpecHubSemanticSearchTests
{
    private sealed class FixedVectorSearch(IReadOnlyList<VectorSearchResult> hits) : IVectorSearchService
    {
        public Guid? SearchedProject { get; private set; }

        public Task<IReadOnlyList<VectorSearchResult>> SearchRelevantSpecsAsync(Guid projectId, string query, int topK = 5, CancellationToken ct = default)
        {
            SearchedProject = projectId;
            return Task.FromResult<IReadOnlyList<VectorSearchResult>>(hits.Take(topK).ToList());
        }

        public Task<bool> UpsertSpecChunkAsync(Guid projectId, string chunkId, string title, string content, string sourcePath,
            IReadOnlyDictionary<string, string>? metadata = null, CancellationToken ct = default) => Task.FromResult(true);
    }

    private static async Task<(PlanningDbContext Db, ProjectScopeContext Scope)> ProjectAsync()
    {
        var db = new PlanningDbContext(new DbContextOptionsBuilder<PlanningDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        db.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "Đặt phòng hội thảo", ProjectKey = "DPHT" });
        await db.SaveChangesAsync();
        return (db, new ProjectScopeContext(orgId, projectId, Guid.NewGuid()));
    }

    [Fact]
    public async Task Spec_hub_search_returns_semantic_chunks_with_citations()
    {
        var (db, scope) = await ProjectAsync();
        var documentId = Guid.NewGuid();
        var hit = MySqlVectorSearchProvider.ToResult(new RagSearchHit(Guid.NewGuid(), documentId, "SRS.pdf", "2. Bảo mật", 2, "Chỉ quản trị viên được duyệt.", 0.81));
        var vector = new FixedVectorSearch([hit]);
        var tools = new SpecHubTools(db, null!, NullLogger<SpecHubTools>.Instance, vector);

        var json = JsonDocument.Parse(await tools.SearchAsync(scope.ProjectId, "ai được duyệt yêu cầu", scope, topK: 3));

        Assert.Equal(scope.ProjectId, vector.SearchedProject);
        Assert.Equal("semantic", json.RootElement.GetProperty("mode").GetString());
        var result = Assert.Single(json.RootElement.GetProperty("results").EnumerateArray());
        Assert.Equal(documentId.ToString(), result.GetProperty("documentId").GetString());
        Assert.Equal(("SRS.pdf", "2. Bảo mật", "2"), (result.GetProperty("documentName").GetString(), result.GetProperty("heading").GetString(), result.GetProperty("page").GetString()));
    }

    [Fact]
    public async Task Spec_hub_search_still_rejects_another_project()
    {
        var (db, scope) = await ProjectAsync();
        var vector = new FixedVectorSearch([]);
        var tools = new SpecHubTools(db, null!, NullLogger<SpecHubTools>.Instance, vector);

        await Assert.ThrowsAnyAsync<Exception>(() => tools.SearchAsync(Guid.NewGuid(), "bất kỳ", scope, topK: 3));
        Assert.Null(vector.SearchedProject);
    }
}
