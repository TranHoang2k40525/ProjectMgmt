namespace IdentityExperience.Application.Dto;

public class NotificationDto : Result
{
    public Guid? NotificationId { get; set; }
    public Guid? UserId { get; set; }
    public string? Type { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ActorId { get; set; }
    public bool? IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? RoleName { get; set; }
    public bool SendEmail { get; set; }
    public List<NotificationDto>? Items { get; set; }
    public int? TotalCount { get; set; }
    public int? UnreadCount { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
