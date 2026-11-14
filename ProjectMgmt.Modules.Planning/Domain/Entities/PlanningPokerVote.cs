namespace Planning.Domain.Entities;

public class PlanningPokerVote
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public decimal VoteValue { get; set; }
    public DateTime VotedAt { get; set; } = DateTime.UtcNow;
}
