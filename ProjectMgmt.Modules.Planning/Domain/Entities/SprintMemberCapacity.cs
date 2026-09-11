namespace Planning.Domain.Entities;

public class SprintMemberCapacity
{
    public Guid Id { get; set; }
    public Guid SprintId { get; set; }
    public Guid UserId { get; set; }
    public decimal? CapacityPoints { get; set; }
    public decimal? AvailableHours { get; set; }
    public string? Note { get; set; }
}
