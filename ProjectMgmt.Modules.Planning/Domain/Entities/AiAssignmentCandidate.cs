namespace Planning.Domain.Entities;

public class AiAssignmentCandidate
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid IssueId { get; set; }
    public Guid CandidateUserId { get; set; }
    public int Rank { get; set; }
    public decimal TotalScore { get; set; }
    public decimal? LoadBalanceScore { get; set; }
    public decimal? SkillMatchScore { get; set; }
    public decimal? HistoryScore { get; set; }
    public decimal? CapacityScore { get; set; }
    public bool IsColdStart { get; set; }
    public string FeatureSnapshot { get; set; } = string.Empty;
    public string? Explanation { get; set; }
    public DateTime CreatedAt { get; set; }
}
