using Microsoft.EntityFrameworkCore;
using ProjectMgmt.Modules.Planning.AiAssignment.Domain.Entities;
using ProjectMgmt.Modules.Planning.AiAssignment.Domain.Repositories;
using ProjectMgmt.Modules.Planning.Infrastructure.Persistence;

namespace ProjectMgmt.Modules.Planning.AiAssignment.Infrastructure.Persistence;

internal sealed class EfAiAssignmentRepository(PlanningDbContext dbContext) : IAiAssignmentRepository
{
    public Task<AiAssignmentRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default) =>
        dbContext.AiAssignmentRuns.SingleOrDefaultAsync(x => x.Id == runId, cancellationToken);

    public async Task<IReadOnlyList<AiAssignmentFeedbackRecord>> GetFeedbackAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default) =>
        await (from decision in dbContext.AiAssignmentDecisions.AsNoTracking()
               join candidate in dbContext.AiAssignmentCandidates.AsNoTracking()
                   on decision.SuggestedCandidateId equals candidate.Id into candidates
               from candidate in candidates.DefaultIfEmpty()
               where decision.DecidedAt != null
                   && decision.DecidedAt >= fromUtc
                   && decision.DecidedAt < toUtc
               orderby decision.DecidedAt
               select new AiAssignmentFeedbackRecord(
                   decision,
                   candidate == null ? null : candidate.FeatureSnapshot))
            .ToListAsync(cancellationToken);

    public async Task AddRunAsync(AiAssignmentRun run, CancellationToken cancellationToken = default) =>
        await dbContext.AiAssignmentRuns.AddAsync(run, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
