namespace DeliveryIntelligence.Domain.Entities;

public class IssueRequiredSkill
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid SkillId { get; set; }
    public sbyte MinLevel { get; set; }
    public decimal Weight { get; set; }
    public string Source { get; set; } = string.Empty;
}
