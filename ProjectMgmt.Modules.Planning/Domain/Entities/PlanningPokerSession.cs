namespace Planning.Domain.Entities;

public class PlanningPokerSession
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? SprintId { get; set; }
    public Guid IssueId { get; set; }
    public string Status { get; set; } = "Voting"; // Voting, Revealed, Finished
    public decimal? FinalStoryPoints { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
