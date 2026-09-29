using IdentityExperience.Application.IServices;
using DeliveryIntelligence.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ProjectMgmt.Solution.Services;

public class AiIssueSourceReader : IAiIssueSourceReader
{
    private readonly DeliveryIntelligenceDbContext _deliveryContext;

    public AiIssueSourceReader(DeliveryIntelligenceDbContext deliveryContext)
    {
        _deliveryContext = deliveryContext;
    }

    public async Task<AiIssueSource?> GetIssueSourceAsync(Guid issueId)
    {
        return await _deliveryContext.Issues
            .AsNoTracking()
            .Where(issue => issue.Id == issueId)
            .Select(issue => new AiIssueSource
            {
                IssueId = issue.Id,
                ProjectId = issue.ProjectId,
                Title = issue.Title,
                Description = issue.Description,
                IsDeleted = issue.IsDeleted
            })
            .FirstOrDefaultAsync();
    }
}
