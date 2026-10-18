namespace DeliveryIntelligence.Domain.Entities;

public class AcceptanceCriteria
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsMet { get; set; }
    public Guid? MetBy { get; set; }
    public DateTime? MetAt { get; set; }
    public int OrderIndex { get; set; }
    public string Source { get; set; } = string.Empty;
    public Guid? AiGenerationLogId { get; set; }
    public bool WasEditedAfterAi { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
