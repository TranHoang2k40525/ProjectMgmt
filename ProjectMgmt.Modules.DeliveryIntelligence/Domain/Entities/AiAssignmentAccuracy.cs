namespace DeliveryIntelligence.Domain.Entities;

public class AiAssignmentAccuracy
{
    public Guid ProjectId { get; set; }
    public string Strategy { get; set; } = string.Empty;
    public DateOnly Day { get; set; }
    public long DecisionCount { get; set; }
    public decimal AcceptedCount { get; set; }
    public decimal OverriddenCount { get; set; }
    public decimal ReassignedLaterCount { get; set; }
    public decimal? AvgCycleTimeHours { get; set; }
}
