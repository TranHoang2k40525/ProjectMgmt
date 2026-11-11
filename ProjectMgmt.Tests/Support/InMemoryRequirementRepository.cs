using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;

namespace ProjectMgmt.Tests.Support;

public class InMemoryRequirementRepository : IRequirementRepository
{
    public List<AcceptanceCriteria> Criteria { get; } = [];
    public List<IssueRequiredSkill> Skills { get; } = [];
    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<AcceptanceCriteria>> GetCriteriaAsync(Guid issueId)
    {
        IReadOnlyList<AcceptanceCriteria> result = Criteria.Where(c => c.IssueId == issueId).OrderBy(c => c.OrderIndex).ThenBy(c => c.CreatedAt).ToList();
        return Task.FromResult(result);
    }

    public Task<AcceptanceCriteria?> GetCriterionAsync(Guid criterionId) =>
        Task.FromResult(Criteria.SingleOrDefault(c => c.Id == criterionId));

    public Task AddCriterionAsync(AcceptanceCriteria criterion)
    {
        Criteria.Add(criterion);
        return Task.CompletedTask;
    }

    public void RemoveCriterion(AcceptanceCriteria criterion) => Criteria.Remove(criterion);

    public Task<IReadOnlyList<IssueRequiredSkill>> GetRequiredSkillsAsync(IEnumerable<Guid> issueIds)
    {
        var ids = issueIds.ToHashSet();
        IReadOnlyList<IssueRequiredSkill> result = Skills.Where(s => ids.Contains(s.IssueId)).OrderBy(s => s.IssueId).ThenByDescending(s => s.Weight).ToList();
        return Task.FromResult(result);
    }

    public Task<IssueRequiredSkill?> GetRequiredSkillAsync(Guid requiredSkillId) =>
        Task.FromResult(Skills.SingleOrDefault(s => s.Id == requiredSkillId));

    public Task AddRequiredSkillAsync(IssueRequiredSkill skill)
    {
        Skills.Add(skill);
        return Task.CompletedTask;
    }

    public void RemoveRequiredSkill(IssueRequiredSkill skill) => Skills.Remove(skill);

    public Task SaveChangesAsync()
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

/// <summary>Danh mục kỹ năng giống seed của IdentityExperience, kèm một kỹ năng đã ngừng dùng.</summary>
public class SharedSkillCatalogReader : ISkillCatalogReader
{
    public static readonly Guid RetiredSkillId = Guid.Parse("44444444-0000-0000-0000-0000000000ff");

    public Task<IReadOnlyList<SkillCatalogItem>> GetSkillsAsync() =>
        Task.FromResult<IReadOnlyList<SkillCatalogItem>>(
        [
            new(SharedTestData.DotnetSkillId, ".NET / C#", "Backend", true),
            new(SharedTestData.AngularSkillId, "Angular / TypeScript", "Frontend", true),
            new(SharedTestData.SqlSkillId, "SQL / Database", "Database", true),
            new(RetiredSkillId, "Flash", "Legacy", false)
        ]);
}
