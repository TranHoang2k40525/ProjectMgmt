namespace DeliveryIntelligence.Domain.Entities;

public class IssueAssignmentHistory
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public Guid? FromAssigneeId { get; set; }
    public Guid? ToAssigneeId { get; set; }
    public Guid? AssignedBy { get; set; }
    public string AssignmentSource { get; set; } = string.Empty;
    public Guid? AiCandidateId { get; set; }
    public decimal? StoryPointsAtTime { get; set; }
    public string? Reason { get; set; }
    public DateTime AssignedAt { get; set; }
}
