using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using Microsoft.EntityFrameworkCore;

namespace DeliveryIntelligence.Infrastructure.Repositories;

public class RequirementRepository : IRequirementRepository
{
    private readonly DeliveryIntelligenceDbContext _context;

    public RequirementRepository(DeliveryIntelligenceDbContext context)
    {
        _context = context;
    }

    // Có tracking: danh sách này còn được dùng để đánh lại OrderIndex khi sắp xếp.
    public async Task<IReadOnlyList<AcceptanceCriteria>> GetCriteriaAsync(Guid issueId) =>
        await _context.AcceptanceCriteria
            .Where(criterion => criterion.IssueId == issueId)
            .OrderBy(criterion => criterion.OrderIndex)
            .ThenBy(criterion => criterion.CreatedAt)
            .ToListAsync();

    public Task<AcceptanceCriteria?> GetCriterionAsync(Guid criterionId) =>
        _context.AcceptanceCriteria.SingleOrDefaultAsync(criterion => criterion.Id == criterionId);

    public async Task AddCriterionAsync(AcceptanceCriteria criterion) =>
        await _context.AcceptanceCriteria.AddAsync(criterion);

    public void RemoveCriterion(AcceptanceCriteria criterion) => _context.AcceptanceCriteria.Remove(criterion);

    public async Task<IReadOnlyList<IssueRequiredSkill>> GetRequiredSkillsAsync(IEnumerable<Guid> issueIds)
    {
        var ids = issueIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await _context.IssueRequiredSkills
            .AsNoTracking()
            .Where(skill => ids.Contains(skill.IssueId))
            .OrderBy(skill => skill.IssueId)
            .ThenByDescending(skill => skill.Weight)
            .ToListAsync();
    }

    public Task<IssueRequiredSkill?> GetRequiredSkillAsync(Guid requiredSkillId) =>
        _context.IssueRequiredSkills.SingleOrDefaultAsync(skill => skill.Id == requiredSkillId);

    public async Task AddRequiredSkillAsync(IssueRequiredSkill skill) =>
        await _context.IssueRequiredSkills.AddAsync(skill);

    public void RemoveRequiredSkill(IssueRequiredSkill skill) => _context.IssueRequiredSkills.Remove(skill);

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
