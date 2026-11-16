namespace DeliveryIntelligence.Application.Dto;

public class ActivityDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public Guid? ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class StatusChangeDto
{
    public Guid Id { get; set; }
    public Guid? FromStatusId { get; set; }
    public string? FromStatusName { get; set; }
    public Guid ToStatusId { get; set; }
    public string ToStatusName { get; set; } = string.Empty;
    public string? FromCategory { get; set; }
    public string ToCategory { get; set; } = string.Empty;
    public Guid? ChangedBy { get; set; }
    public string ChangedByName { get; set; } = string.Empty;
    public long? DurationSeconds { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class AssignmentChangeDto
{
    public Guid Id { get; set; }
    public Guid? FromAssigneeId { get; set; }
    public string? FromAssigneeName { get; set; }
    public Guid? ToAssigneeId { get; set; }
    public string? ToAssigneeName { get; set; }
    public Guid? AssignedBy { get; set; }
    public string AssignedByName { get; set; } = string.Empty;
    public string AssignmentSource { get; set; } = string.Empty;
    public decimal? StoryPointsAtTime { get; set; }
    public string? Reason { get; set; }
    public DateTime AssignedAt { get; set; }
}
