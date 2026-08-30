namespace ProjectMgmt.AiAssist.Contracts;

public sealed record AiBreakdownFeedbackSampleDto(
    Guid SuggestedTaskId,
    Guid GenerationLogId,
    string OriginalSummary,
    string? OriginalDescription,
    IReadOnlyList<string> OriginalAcceptanceCriteria,
    string? FinalSummary,
    string? FinalDescription,
    IReadOnlyList<string>? FinalAcceptanceCriteria,
    string UserAction,
    decimal? EditDistanceRatio,
    DateTimeOffset ReviewedAtUtc);

public interface IAiBreakdownFeedbackExportService
{
    Task<IReadOnlyList<AiBreakdownFeedbackSampleDto>> ExportBreakdownSamplesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);
}
