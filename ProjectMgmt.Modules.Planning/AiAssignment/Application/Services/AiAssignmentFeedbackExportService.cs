using System.Text.Json;
using ProjectMgmt.AiAssignment.Contracts;
using ProjectMgmt.Modules.Planning.AiAssignment.Domain.IRepositories;

namespace ProjectMgmt.Modules.Planning.AiAssignment.Application.Services;

internal class AiAssignmentFeedbackExportService : IAiAssignmentFeedbackExportService
{
    private readonly IAiAssignmentRepository _repository;

    public AiAssignmentFeedbackExportService(IAiAssignmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<AiAssignmentFeedbackSampleDto>> ExportAssignmentSamplesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var rangeStart = fromUtc.UtcDateTime;
        var rangeEnd = toUtc.UtcDateTime;

        var rows = await _repository.GetFeedbackAsync(rangeStart, rangeEnd, cancellationToken);

        return rows.Select(x => new AiAssignmentFeedbackSampleDto(
                x.Decision.Id,
                x.Decision.IssueId,
                x.Decision.SuggestedUserId,
                x.Decision.FinalUserId,
                x.Decision.Outcome,
                x.Decision.OverrideReason,
                ParseFeatures(x.FeatureSnapshot),
                new DateTimeOffset(DateTime.SpecifyKind(x.Decision.DecidedAt!.Value, DateTimeKind.Utc))))
            .ToList();
    }

    private static Dictionary<string, decimal> ParseFeatures(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, decimal>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, decimal>>(json)
                ?? new Dictionary<string, decimal>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, decimal>();
        }
    }
}
