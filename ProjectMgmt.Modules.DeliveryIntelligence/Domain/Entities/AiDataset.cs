namespace DeliveryIntelligence.Domain.Entities;

public class AiDataset
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Languages { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
