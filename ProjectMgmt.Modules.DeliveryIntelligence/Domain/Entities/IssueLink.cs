namespace DeliveryIntelligence.Domain.Entities;

public class IssueLink
{
    public Guid Id { get; set; }
    public Guid SourceIssueId { get; set; }
    public Guid TargetIssueId { get; set; }
    public string LinkType { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
