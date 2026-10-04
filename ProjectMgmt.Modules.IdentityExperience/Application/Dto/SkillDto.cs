namespace IdentityExperience.Application.Dto;

public class SkillDto : Result
{
    public Guid? UserId { get; set; }
    public Guid? SkillId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Category { get; set; }
    public bool? IsActive { get; set; }
    public sbyte? ProficiencyLevel { get; set; }
    public sbyte? Level { get; set; }
    public decimal? YearsOfExperience { get; set; }
    public bool? IsSelfDeclared { get; set; }
    public bool? Verified { get; set; }
    public int? UpdatedCount { get; set; }
    public List<SkillDto>? Items { get; set; }
    public List<SkillDto>? Skills { get; set; }
}
