namespace DeliveryIntelligence.Domain.Entities;

public class AiBreakdownQuality
{
    public Guid ProjectId { get; set; }
    public Guid? ModelId { get; set; }
    public Guid? PromptTemplateId { get; set; }
    public DateOnly Day { get; set; }
    public long RequestCount { get; set; }
    public long SuggestedTaskCount { get; set; }
    public decimal KeptCount { get; set; }
    public decimal EditedCount { get; set; }
    public decimal RejectedCount { get; set; }
    public decimal? AvgEditDistance { get; set; }
    public decimal? AvgLatencyMs { get; set; }
}
