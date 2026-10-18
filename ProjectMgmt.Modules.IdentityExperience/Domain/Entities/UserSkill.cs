namespace IdentityExperience.Domain.Entities;

public class UserSkill
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SkillId { get; set; }
    public sbyte ProficiencyLevel { get; set; }
    public decimal? YearsOfExperience { get; set; }
    public bool IsSelfDeclared { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
