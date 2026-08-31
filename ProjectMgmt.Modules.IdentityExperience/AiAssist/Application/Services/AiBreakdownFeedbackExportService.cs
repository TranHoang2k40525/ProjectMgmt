using System.Text.Json;
using ProjectMgmt.AiAssist.Contracts;
using ProjectMgmt.Modules.IdentityExperience.AiAssist.Domain.Repositories;

namespace ProjectMgmt.Modules.IdentityExperience.AiAssist.Application.Services;

internal sealed class AiBreakdownFeedbackExportService(IAiAssistRepository repository)
    : IAiBreakdownFeedbackExportService
{
    public async Task<IReadOnlyList<AiBreakdownFeedbackSampleDto>> ExportBreakdownSamplesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var from = fromUtc.UtcDateTime;
        var to = toUtc.UtcDateTime;

        var rows = await repository.GetReviewedTasksAsync(from, to, cancellationToken);

        return rows.Select(x => new AiBreakdownFeedbackSampleDto(
                x.Id,
                x.AiGenerationLogId,
                x.OriginalSummary,
                x.OriginalDescription,
                ParseStringArray(x.OriginalAcceptanceCriteria),
                x.FinalSummary,
                x.FinalDescription,
                x.FinalAcceptanceCriteria is null ? null : ParseStringArray(x.FinalAcceptanceCriteria),
                x.UserAction,
                x.EditDistanceRatio,
                new DateTimeOffset(DateTime.SpecifyKind(x.ReviewedAt!.Value, DateTimeKind.Utc))))
            .ToList();
    }

    private static string[] ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
