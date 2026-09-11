namespace DeliveryIntelligence.Domain.Entities;

public class IssueVersionLink
{
    public Guid IssueId { get; set; }
    public Guid VersionId { get; set; }
    public string LinkType { get; set; } = string.Empty;
}
