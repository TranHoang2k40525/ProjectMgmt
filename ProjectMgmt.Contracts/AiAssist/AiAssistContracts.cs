namespace ProjectMgmt.AiAssist.Contracts;

public class AiBreakdownFeedbackSampleDto
{
    public AiBreakdownFeedbackSampleDto(
        Guid suggestedTaskId,
        Guid generationLogId,
        string originalSummary,
        string? originalDescription,
        IReadOnlyList<string> originalAcceptanceCriteria,
        string? finalSummary,
        string? finalDescription,
        IReadOnlyList<string>? finalAcceptanceCriteria,
        string userAction,
        decimal? editDistanceRatio,
        DateTimeOffset reviewedAtUtc)
    {
        SuggestedTaskId = suggestedTaskId;
        GenerationLogId = generationLogId;
        OriginalSummary = originalSummary;
        OriginalDescription = originalDescription;
        OriginalAcceptanceCriteria = originalAcceptanceCriteria;
        FinalSummary = finalSummary;
        FinalDescription = finalDescription;
        FinalAcceptanceCriteria = finalAcceptanceCriteria;
        UserAction = userAction;
        EditDistanceRatio = editDistanceRatio;
        ReviewedAtUtc = reviewedAtUtc;
    }

    public Guid SuggestedTaskId { get; set; }
    public Guid GenerationLogId { get; set; }
    public string OriginalSummary { get; set; }
    public string? OriginalDescription { get; set; }
    public IReadOnlyList<string> OriginalAcceptanceCriteria { get; set; }
    public string? FinalSummary { get; set; }
    public string? FinalDescription { get; set; }
    public IReadOnlyList<string>? FinalAcceptanceCriteria { get; set; }
    public string UserAction { get; set; }
    public decimal? EditDistanceRatio { get; set; }
    public DateTimeOffset ReviewedAtUtc { get; set; }
}

public interface IAiBreakdownFeedbackExportService
{
    Task<IReadOnlyList<AiBreakdownFeedbackSampleDto>> ExportBreakdownSamplesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);
}
