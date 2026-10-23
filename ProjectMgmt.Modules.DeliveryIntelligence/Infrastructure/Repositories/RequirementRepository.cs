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
    public async Task<IReadOnlyList<AcceptanceCriteria>> GetCriteriaAsync(Guid issueId, CancellationToken cancellationToken = default) =>
        await _context.AcceptanceCriteria
            .Where(criterion => criterion.IssueId == issueId)
            .OrderBy(criterion => criterion.OrderIndex)
            .ThenBy(criterion => criterion.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<AcceptanceCriteria?> GetCriterionAsync(Guid criterionId, CancellationToken cancellationToken = default) =>
        _context.AcceptanceCriteria.SingleOrDefaultAsync(criterion => criterion.Id == criterionId, cancellationToken);

    public async Task AddCriterionAsync(AcceptanceCriteria criterion, CancellationToken cancellationToken = default) =>
        await _context.AcceptanceCriteria.AddAsync(criterion, cancellationToken);

    public void RemoveCriterion(AcceptanceCriteria criterion) => _context.AcceptanceCriteria.Remove(criterion);

    public async Task<IReadOnlyList<IssueRequiredSkill>> GetRequiredSkillsAsync(IEnumerable<Guid> issueIds, CancellationToken cancellationToken = default)
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
            .ToListAsync(cancellationToken);
    }

    public Task<IssueRequiredSkill?> GetRequiredSkillAsync(Guid requiredSkillId, CancellationToken cancellationToken = default) =>
        _context.IssueRequiredSkills.SingleOrDefaultAsync(skill => skill.Id == requiredSkillId, cancellationToken);

    public async Task AddRequiredSkillAsync(IssueRequiredSkill skill, CancellationToken cancellationToken = default) =>
        await _context.IssueRequiredSkills.AddAsync(skill, cancellationToken);

    public void RemoveRequiredSkill(IssueRequiredSkill skill) => _context.IssueRequiredSkills.Remove(skill);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
