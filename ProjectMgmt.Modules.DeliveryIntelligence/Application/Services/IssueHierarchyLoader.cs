using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.IRepositories;
using DeliveryIntelligence.Domain.UseCases;
using ProjectMgmt.ProjectManagement.Contracts;

namespace DeliveryIntelligence.Application.Services;

public record IssuePlacement(IssueRuleViolation? Violation, Guid? ParentId, Guid? EpicId);

/// <summary>
/// Nạp dữ liệu cho luật cây Issue: cấp IssueType lấy qua IProjectLookupService (Planning),
/// Issue cha/tổ tiên đọc từ repository của module.
/// </summary>
public class IssueHierarchyLoader
{
    private const int MaxDepth = 16;
    private readonly IIssueRepository _issues;
    private readonly IProjectLookupService _projects;

    public IssueHierarchyLoader(IIssueRepository issues, IProjectLookupService projects)
    {
        _issues = issues;
        _projects = projects;
    }

    public async Task<IReadOnlyDictionary<Guid, IssueTypeLevel>> GetTypesAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var types = await _projects.GetIssueTypesAsync(projectId, cancellationToken);
        return types.ToDictionary(
            type => type.Id,
            type => new IssueTypeLevel(type.Id, type.Name, type.HierarchyLevel, type.IsSubtask));
    }

    public async Task<IssuePlacement> PlaceAsync(
        Guid issueId,
        Guid projectId,
        IssueTypeLevel type,
        Guid? parentId,
        Guid? epicId,
        IReadOnlyDictionary<Guid, IssueTypeLevel> types,
        CancellationToken cancellationToken = default)
    {
        IssueHierarchyNode? parent = null;
        if (parentId.HasValue)
        {
            parent = await GetNodeAsync(parentId.Value, types, cancellationToken);
            if (parent is null)
            {
                return Rejected("ISSUE_PARENT_NOT_FOUND", "Không tìm thấy công việc cha.");
            }
        }

        List<Guid> ancestors = parent is null ? [] : await GetAncestorIdsAsync(parent, cancellationToken);
        var violation = IssueHierarchyRules.ValidateParent(issueId, projectId, type, parent, ancestors);
        if (violation is not null)
        {
            return new IssuePlacement(violation, null, null);
        }

        if (parent is not null)
        {
            return new IssuePlacement(null, parent.Id, IssueHierarchyRules.ResolveEpicId(parent));
        }

        if (!epicId.HasValue)
        {
            return new IssuePlacement(null, null, null);
        }

        var epic = await GetNodeAsync(epicId.Value, types, cancellationToken);
        violation = epic is null
            ? new IssueRuleViolation("ISSUE_EPIC_LINK_INVALID", "Không tìm thấy Epic.")
            : IssueHierarchyRules.ValidateEpic(issueId, projectId, type, epic);
        return new IssuePlacement(violation, null, violation is null ? epicId : null);
    }

    public async Task<IssueHierarchyNode?> GetNodeAsync(
        Guid issueId,
        IReadOnlyDictionary<Guid, IssueTypeLevel> types,
        CancellationToken cancellationToken = default)
    {
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken: cancellationToken);
        return issue is null ? null : ToNode(issue, types);
    }

    public static IssueHierarchyNode? ToNode(Issue issue, IReadOnlyDictionary<Guid, IssueTypeLevel> types) =>
        types.TryGetValue(issue.IssueTypeId, out var type)
            ? new IssueHierarchyNode(issue.Id, issue.ProjectId, type, issue.ParentId, issue.EpicId)
            : null;

    /// <summary>Id các tổ tiên phía trên <paramref name="node"/> (không gồm chính nó), dừng khi gặp vòng lặp dữ liệu cũ.</summary>
    public async Task<List<Guid>> GetAncestorIdsAsync(IssueHierarchyNode node, CancellationToken cancellationToken = default)
    {
        var ancestors = new List<Guid>();
        var nextId = node.ParentId;
        while (nextId.HasValue && ancestors.Count < MaxDepth && !ancestors.Contains(nextId.Value))
        {
            ancestors.Add(nextId.Value);
            var next = await _issues.GetByIdAsync(nextId.Value, includeDeleted: true, cancellationToken);
            nextId = next?.ParentId;
        }

        return ancestors;
    }

    private static IssuePlacement Rejected(string code, string message) =>
        new(new IssueRuleViolation(code, message), null, null);
}
