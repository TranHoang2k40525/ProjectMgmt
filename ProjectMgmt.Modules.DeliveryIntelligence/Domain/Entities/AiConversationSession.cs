namespace DeliveryIntelligence.Domain.Entities;

/// <summary>
/// Lưu trữ bền vững từng phiên hội thoại đa lượt của Trợ Lý AI theo đúng biên khóa cứng (OrganizationId, ProjectId, UserId).
/// </summary>
public class AiConversationSession
{
    public Guid Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Model { get; set; } = "Gemini 3.6 Flash";
    public string LastIntent { get; set; } = "sprint_plan";
    public string RecommendedTarget { get; set; } = "sprint";
    public Guid? TargetSprintId { get; set; }
    public int TurnCount { get; set; }
    public int TaskCount { get; set; }
    public decimal TotalStoryPoints { get; set; }
    public int MemoryCount { get; set; }
    public string ConversationHistoryJson { get; set; } = "[]";
    public string ProposalJson { get; set; } = "{}";
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAtUtc { get; set; }

    public ICollection<AiConversationMemoryEntry> Memories { get; set; } = new List<AiConversationMemoryEntry>();
}
