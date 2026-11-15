namespace DeliveryIntelligence.Domain.Entities;

/// <summary>
/// Mục chỉ mục trí nhớ có gắn mã trỏ (#mem-1, #mem-2...) của một phiên Trợ Lý AI,
/// cho phép Agent trỏ thẳng vào nút ngữ cảnh liên quan thay vì nạp lại toàn bộ hội thoại cũ.
/// </summary>
public class AiConversationMemoryEntry
{
    public Guid Id { get; set; }
    public Guid ConversationSessionId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public string AnchorKey { get; set; } = string.Empty;
    public int TurnIndex { get; set; }
    public string Category { get; set; } = "qa_fact";
    public string Title { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string ContentJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public AiConversationSession? Session { get; set; }
}
