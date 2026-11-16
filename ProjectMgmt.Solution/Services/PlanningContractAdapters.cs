using Microsoft.EntityFrameworkCore;
using Planning.Domain.Entities;
using Planning.Infrastructure;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Solution.Services;

public class PlanningWorkflowValidationService(PlanningDbContext db) : IWorkflowValidationService
{
    public async Task<WorkflowTransitionCheck> CheckTransitionAsync(
        Guid projectId,
        Guid fromStatusId,
        Guid toStatusId)
    {
        var from = await db.WorkflowStatuses
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == fromStatusId && s.ProjectId == projectId);
        var to = await db.WorkflowStatuses
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == toStatusId && s.ProjectId == projectId);

        if (from is null || to is null)
        {
            return new WorkflowTransitionCheck(
                false,
                "WORKFLOW_TRANSITION_NOT_ALLOWED",
                $"Không có đường chuyển {from?.Name} → {to?.Name}.",
                null,
                from?.Category,
                to?.Category ?? string.Empty);
        }

        if (fromStatusId == toStatusId)
        {
            return new WorkflowTransitionCheck(
                true,
                null,
                null,
                null,
                from.Category,
                to.Category);
        }

        var transition = await db.WorkflowTransitions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.ProjectId == projectId && t.FromStatusId == fromStatusId && t.ToStatusId == toStatusId);

        var hasAnyTransitions = await db.WorkflowTransitions
            .AsNoTracking()
            .AnyAsync(t => t.ProjectId == projectId);

        var allowed = transition is not null || !hasAnyTransitions;

        return new WorkflowTransitionCheck(
            allowed,
            allowed ? null : "WORKFLOW_TRANSITION_NOT_ALLOWED",
            allowed ? null : $"Không có đường chuyển {from.Name} → {to.Name}.",
            transition?.RequiredPermissionCode,
            from.Category,
            to.Category);
    }
}

public class PlanningReleaseLookupService(PlanningDbContext db) : IProjectReleaseLookupService
{
    public async Task<IReadOnlyList<ProjectComponentDto>> GetComponentsAsync(Guid projectId)
    {
        var components = await db.ProjectComponents
            .AsNoTracking()
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.Name)
            .ToListAsync();

        if (components.Count == 0 && await db.Projects.AsNoTracking().AnyAsync(p => p.Id == projectId))
        {
            var seeded = new List<ProjectComponent>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Name = "Backend API",
                    Description = "API & Services",
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Name = "Web UI",
                    Description = "Frontend Application",
                    CreatedAt = DateTime.UtcNow
                }
            };
            db.ProjectComponents.AddRange(seeded);
            await db.SaveChangesAsync();
            components = seeded;
        }

        return components
            .Select(c => new ProjectComponentDto(c.Id, c.ProjectId, c.Name, c.DefaultSkillId))
            .ToList();
    }

    public async Task<IReadOnlyList<ProjectVersionDto>> GetVersionsAsync(Guid projectId)
    {
        var versions = await db.ProjectVersions
            .AsNoTracking()
            .Where(v => v.ProjectId == projectId)
            .OrderBy(v => v.Name)
            .ToListAsync();

        if (versions.Count == 0 && await db.Projects.AsNoTracking().AnyAsync(p => p.Id == projectId))
        {
            var seeded = new List<ProjectVersion>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Name = "v1.0.0",
                    Description = "Initial Release",
                    ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                    IsReleased = false,
                    CreatedAt = DateTime.UtcNow
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Name = "v0.9.0",
                    Description = "Beta Release",
                    ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)),
                    IsReleased = true,
                    CreatedAt = DateTime.UtcNow
                }
            };
            db.ProjectVersions.AddRange(seeded);
            await db.SaveChangesAsync();
            versions = seeded;
        }

        return versions
            .Select(v => new ProjectVersionDto(v.Id, v.ProjectId, v.Name, v.ReleaseDate, v.IsReleased))
            .ToList();
    }
}
