using ProjectMgmt.AiAssignment.Contracts;
using ProjectMgmt.AiAssist.Contracts;

namespace ProjectMgmt.Tests.Fakes;

public sealed class FakeAiBreakdownFeedbackExportService : IAiBreakdownFeedbackExportService
{
    public List<AiBreakdownFeedbackSampleDto> Samples { get; } = [];

    public Task<IReadOnlyList<AiBreakdownFeedbackSampleDto>> ExportBreakdownSamplesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AiBreakdownFeedbackSampleDto>>(Samples
            .Where(sample => sample.ReviewedAtUtc >= fromUtc && sample.ReviewedAtUtc <= toUtc)
            .OrderBy(sample => sample.ReviewedAtUtc)
            .ToArray());
}

public sealed class FakeAiAssignmentFeedbackExportService : IAiAssignmentFeedbackExportService
{
    public List<AiAssignmentFeedbackSampleDto> Samples { get; } = [];

    public Task<IReadOnlyList<AiAssignmentFeedbackSampleDto>> ExportAssignmentSamplesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AiAssignmentFeedbackSampleDto>>(Samples
            .Where(sample => sample.DecidedAtUtc >= fromUtc && sample.DecidedAtUtc <= toUtc)
            .OrderBy(sample => sample.DecidedAtUtc)
            .ToArray());
}
