using DeliveryIntelligence.Application.Dto;
using DeliveryIntelligence.Application.IServices;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using ProjectMgmt.BuildingBlocks.Results;
using ProjectMgmt.ProjectManagement.Contracts;

namespace DeliveryIntelligence.Application.Services;

public class IssueReleaseService : IIssueReleaseService
{
    public const string FixVersion = "FixVersion";
    public const string AffectsVersion = "AffectsVersion";
    public const int MaxLinksPerKind = 20;

    private readonly IIssueRepository _issues;
    private readonly IReleaseLinkRepository _links;
    private readonly IProjectReleaseLookupService _releases;
    private readonly TimeProvider _clock;

    public IssueReleaseService(
        IIssueRepository issues,
        IReleaseLinkRepository links,
        IProjectReleaseLookupService releases,
        TimeProvider clock)
    {
        _issues = issues;
        _links = links;
        _releases = releases;
        _clock = clock;
    }

    public async Task<Result<IssueReleaseLinksDto>> GetLinksAsync(Guid issueId)
    {
        var issue = await _issues.GetByIdAsync(issueId);
        return issue is null
            ? Result<IssueReleaseLinksDto>.Failure(IssueErrors.NotFound)
            : Result.Success(await BuildAsync(issue));
    }

    public async Task<Result<IssueReleaseLinksDto>> AddComponentAsync(
        Guid issueId,
        Guid componentId,
        Guid actorUserId)
    {
        var issue = await _issues.GetByIdAsync(issueId);
        if (issue is null)
        {
            return Result<IssueReleaseLinksDto>.Failure(IssueErrors.NotFound);
        }

        var components = await _releases.GetComponentsAsync(issue.ProjectId);
        var component = components.FirstOrDefault(c => c.Id == componentId);
        if (component is null)
        {
            return Result<IssueReleaseLinksDto>.Failure(ReleaseErrors.ComponentNotInProject);
        }

        var existing = await _links.GetComponentLinksAsync(issueId);
        if (existing.All(link => link.ComponentId != componentId))
        {
            if (existing.Count >= MaxLinksPerKind)
            {
                return Result<IssueReleaseLinksDto>.Failure(ReleaseErrors.TooManyLinks);
            }

            await _links.AddComponentLinkAsync(new IssueComponentLink { IssueId = issueId, ComponentId = componentId });
            await _issues.AddActivityAsync(
                IssueActivities.Create(issueId, actorUserId, IssueActivities.ComponentAdded, Now(), "Component", newValue: component.Name));
            await _links.SaveChangesAsync();
        }

        return Result.Success(await BuildAsync(issue, components));
    }

    public async Task<Result<IssueReleaseLinksDto>> RemoveComponentAsync(
        Guid issueId,
        Guid componentId,
        Guid actorUserId)
    {
        var issue = await _issues.GetByIdAsync(issueId);
        if (issue is null)
        {
            return Result<IssueReleaseLinksDto>.Failure(IssueErrors.NotFound);
        }

        var link = (await _links.GetComponentLinksAsync(issueId)).FirstOrDefault(l => l.ComponentId == componentId);
        if (link is not null)
        {
            var name = (await _releases.GetComponentsAsync(issue.ProjectId)).FirstOrDefault(c => c.Id == componentId)?.Name;
            _links.RemoveComponentLink(link);
            await _issues.AddActivityAsync(
                IssueActivities.Create(issueId, actorUserId, IssueActivities.ComponentRemoved, Now(), "Component", oldValue: name ?? componentId.ToString()));
            await _links.SaveChangesAsync();
        }

        return Result.Success(await BuildAsync(issue));
    }

    public async Task<Result<IssueReleaseLinksDto>> AddVersionAsync(
        Guid issueId,
        AddIssueVersionDto request,
        Guid actorUserId)
    {
        ArgumentNullException.ThrowIfNull(request);
        var linkType = NormalizeLinkType(request.LinkType);
        if (linkType is null)
        {
            return Result<IssueReleaseLinksDto>.Failure(ReleaseErrors.LinkTypeInvalid);
        }

        var issue = await _issues.GetByIdAsync(issueId);
        if (issue is null)
        {
            return Result<IssueReleaseLinksDto>.Failure(IssueErrors.NotFound);
        }

        var versions = await _releases.GetVersionsAsync(issue.ProjectId);
        var version = versions.FirstOrDefault(v => v.Id == request.VersionId);
        if (version is null)
        {
            return Result<IssueReleaseLinksDto>.Failure(ReleaseErrors.VersionNotInProject);
        }

        var existing = (await _links.GetVersionLinksAsync(issueId)).Where(link => link.LinkType == linkType).ToList();
        if (existing.All(link => link.VersionId != version.Id))
        {
            if (existing.Count >= MaxLinksPerKind)
            {
                return Result<IssueReleaseLinksDto>.Failure(ReleaseErrors.TooManyLinks);
            }

            await _links.AddVersionLinkAsync(new IssueVersionLink { IssueId = issueId, VersionId = version.Id, LinkType = linkType });
            await _issues.AddActivityAsync(
                IssueActivities.Create(issueId, actorUserId, IssueActivities.VersionAdded, Now(), linkType, newValue: version.Name));
            await _links.SaveChangesAsync();
        }

        return Result.Success(await BuildAsync(issue, versions: versions));
    }

    public async Task<Result<IssueReleaseLinksDto>> RemoveVersionAsync(
        Guid issueId,
        Guid versionId,
        string linkType,
        Guid actorUserId)
    {
        var normalized = NormalizeLinkType(linkType);
        if (normalized is null)
        {
            return Result<IssueReleaseLinksDto>.Failure(ReleaseErrors.LinkTypeInvalid);
        }

        var issue = await _issues.GetByIdAsync(issueId);
        if (issue is null)
        {
            return Result<IssueReleaseLinksDto>.Failure(IssueErrors.NotFound);
        }

        var link = (await _links.GetVersionLinksAsync(issueId))
            .FirstOrDefault(l => l.VersionId == versionId && l.LinkType == normalized);
        if (link is not null)
        {
            var name = (await _releases.GetVersionsAsync(issue.ProjectId)).FirstOrDefault(v => v.Id == versionId)?.Name;
            _links.RemoveVersionLink(link);
            await _issues.AddActivityAsync(
                IssueActivities.Create(issueId, actorUserId, IssueActivities.VersionRemoved, Now(), normalized, oldValue: name ?? versionId.ToString()));
            await _links.SaveChangesAsync();
        }

        return Result.Success(await BuildAsync(issue));
    }

    private async Task<IssueReleaseLinksDto> BuildAsync(
        Issue issue,
        IReadOnlyList<ProjectComponentDto>? components = null,
        IReadOnlyList<ProjectVersionDto>? versions = null)
    {
        components ??= await _releases.GetComponentsAsync(issue.ProjectId);
        versions ??= await _releases.GetVersionsAsync(issue.ProjectId);
        var componentNames = components.ToDictionary(c => c.Id);
        var versionInfo = versions.ToDictionary(v => v.Id);
        var componentLinks = await _links.GetComponentLinksAsync(issue.Id);
        var versionLinks = await _links.GetVersionLinksAsync(issue.Id);

        IssueVersionDto ToVersion(IssueVersionLink link) => versionInfo.TryGetValue(link.VersionId, out var v)
            ? new IssueVersionDto { VersionId = v.Id, Name = v.Name, LinkType = link.LinkType, ReleaseDate = v.ReleaseDate, IsReleased = v.IsReleased }
            : new IssueVersionDto { VersionId = link.VersionId, Name = string.Empty, LinkType = link.LinkType };

        return new IssueReleaseLinksDto
        {
            Components = componentLinks
                .Select(link => new IssueComponentDto { ComponentId = link.ComponentId, Name = componentNames.GetValueOrDefault(link.ComponentId)?.Name ?? string.Empty })
                .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            FixVersions = versionLinks.Where(l => l.LinkType == FixVersion).Select(ToVersion).OrderBy(v => v.ReleaseDate ?? DateOnly.MaxValue).ToList(),
            AffectsVersions = versionLinks.Where(l => l.LinkType == AffectsVersion).Select(ToVersion).OrderBy(v => v.ReleaseDate ?? DateOnly.MaxValue).ToList(),
            AvailableComponents = components.Select(c => new ReleaseOptionDto { Id = c.Id, Name = c.Name }).ToList(),
            AvailableVersions = versions.Select(v => new ReleaseOptionDto { Id = v.Id, Name = v.Name, ReleaseDate = v.ReleaseDate, IsReleased = v.IsReleased }).ToList()
        };
    }

    private static string? NormalizeLinkType(string? linkType) =>
        string.Equals(linkType, FixVersion, StringComparison.OrdinalIgnoreCase) ? FixVersion
        : string.Equals(linkType, AffectsVersion, StringComparison.OrdinalIgnoreCase) ? AffectsVersion
        : null;

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;
}

public static class ReleaseErrors
{
    public static readonly Error ComponentNotInProject = Error.Validation("ISSUE_COMPONENT_INVALID", "Component không tồn tại hoặc không thuộc Project của Issue.");
    public static readonly Error VersionNotInProject = Error.Validation("ISSUE_VERSION_INVALID", "Version không tồn tại hoặc không thuộc Project của Issue.");
    public static readonly Error LinkTypeInvalid = Error.Validation("ISSUE_VERSION_LINK_TYPE_INVALID", "Loại liên kết phải là FixVersion hoặc AffectsVersion.");
    public static readonly Error TooManyLinks = Error.Validation("ISSUE_RELEASE_LINK_LIMIT", $"Tối đa {IssueReleaseService.MaxLinksPerKind} liên kết mỗi loại.");
}
