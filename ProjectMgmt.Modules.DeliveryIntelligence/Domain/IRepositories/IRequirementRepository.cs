using DeliveryIntelligence.Domain.Entities;

namespace DeliveryIntelligence.Domain.IRepositories;

/// <summary>Acceptance Criteria và kỹ năng yêu cầu của Issue — đầu vào trực tiếp của hai AI.</summary>
public interface IRequirementRepository
{
    Task<IReadOnlyList<AcceptanceCriteria>> GetCriteriaAsync(Guid issueId, CancellationToken cancellationToken = default);

    Task<AcceptanceCriteria?> GetCriterionAsync(Guid criterionId, CancellationToken cancellationToken = default);

    Task AddCriterionAsync(AcceptanceCriteria criterion, CancellationToken cancellationToken = default);

    void RemoveCriterion(AcceptanceCriteria criterion);

    Task<IReadOnlyList<IssueRequiredSkill>> GetRequiredSkillsAsync(IEnumerable<Guid> issueIds, CancellationToken cancellationToken = default);

    Task<IssueRequiredSkill?> GetRequiredSkillAsync(Guid requiredSkillId, CancellationToken cancellationToken = default);

    Task AddRequiredSkillAsync(IssueRequiredSkill skill, CancellationToken cancellationToken = default);

    void RemoveRequiredSkill(IssueRequiredSkill skill);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
