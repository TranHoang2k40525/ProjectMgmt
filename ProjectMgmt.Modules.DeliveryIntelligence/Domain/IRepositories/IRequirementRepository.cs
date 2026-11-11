using DeliveryIntelligence.Domain.Entities;

namespace DeliveryIntelligence.Domain.IRepositories;

/// <summary>Acceptance Criteria và kỹ năng yêu cầu của Issue — đầu vào trực tiếp của hai AI.</summary>
public interface IRequirementRepository
{
    Task<IReadOnlyList<AcceptanceCriteria>> GetCriteriaAsync(Guid issueId);

    Task<AcceptanceCriteria?> GetCriterionAsync(Guid criterionId);

    Task AddCriterionAsync(AcceptanceCriteria criterion);

    void RemoveCriterion(AcceptanceCriteria criterion);

    Task<IReadOnlyList<IssueRequiredSkill>> GetRequiredSkillsAsync(IEnumerable<Guid> issueIds);

    Task<IssueRequiredSkill?> GetRequiredSkillAsync(Guid requiredSkillId);

    Task AddRequiredSkillAsync(IssueRequiredSkill skill);

    void RemoveRequiredSkill(IssueRequiredSkill skill);

    Task SaveChangesAsync();
}
