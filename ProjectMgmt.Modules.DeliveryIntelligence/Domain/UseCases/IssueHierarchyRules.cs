namespace DeliveryIntelligence.Domain.UseCases;

/// <summary>
/// Thông tin cấp bậc của IssueType do Planning sở hữu (nhận qua contract, không đọc DbContext của Planning).
/// Quy ước seed hiện tại: Epic = 3, Story/Task/Bug = 2, Sub-task = 1.
/// </summary>
public record IssueTypeLevel(Guid Id, string Name, int HierarchyLevel, bool IsSubtask)
{
    public const int EpicLevel = 3;

    public bool IsEpic => !IsSubtask && HierarchyLevel >= EpicLevel;
}

public record IssueHierarchyNode(Guid Id, Guid ProjectId, IssueTypeLevel Type, Guid? ParentId, Guid? EpicId);

public record IssueRuleViolation(string Code, string Message);

/// <summary>
/// Luật cây Epic → Story/Task/Bug → Sub-task. Trigger TRG_Issue_NoSelfParent_* chỉ chặn tự làm cha ở DB;
/// các luật cấp bậc, vòng lặp và cùng Project được kiểm ở đây trước khi ghi.
/// </summary>
public static class IssueHierarchyRules
{
    public static IssueRuleViolation? ValidateParent(
        Guid issueId,
        Guid projectId,
        IssueTypeLevel type,
        IssueHierarchyNode? parent,
        IReadOnlyCollection<Guid> parentAncestorIds)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(parentAncestorIds);

        if (parent is null)
        {
            return type.IsSubtask
                ? new IssueRuleViolation("ISSUE_SUBTASK_PARENT_REQUIRED", "Sub-task phải có công việc cha.")
                : null;
        }

        if (parent.Id == issueId)
        {
            return new IssueRuleViolation("ISSUE_PARENT_SELF", "Issue không thể là cha của chính nó.");
        }

        if (parent.ProjectId != projectId)
        {
            return new IssueRuleViolation("ISSUE_PARENT_PROJECT_MISMATCH", "Công việc cha phải thuộc cùng Project.");
        }

        if (type.IsEpic)
        {
            return new IssueRuleViolation("ISSUE_EPIC_PARENT_NOT_ALLOWED", "Epic không được có công việc cha.");
        }

        if (parent.Type.IsSubtask || parent.Type.HierarchyLevel <= type.HierarchyLevel)
        {
            return new IssueRuleViolation("ISSUE_PARENT_LEVEL_INVALID", "Công việc cha phải có cấp cao hơn loại Issue hiện tại.");
        }

        if (parentAncestorIds.Contains(issueId))
        {
            return new IssueRuleViolation("ISSUE_PARENT_CYCLE", "Không thể tạo vòng lặp cha–con.");
        }

        return null;
    }

    public static IssueRuleViolation? ValidateEpic(Guid issueId, Guid projectId, IssueTypeLevel type, IssueHierarchyNode? epic)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (epic is null)
        {
            return null;
        }

        if (type.IsEpic || epic.Id == issueId)
        {
            return new IssueRuleViolation("ISSUE_EPIC_LINK_INVALID", "Epic không thể thuộc một Epic khác.");
        }

        if (epic.ProjectId != projectId || !epic.Type.IsEpic)
        {
            return new IssueRuleViolation("ISSUE_EPIC_LINK_INVALID", "Epic không hợp lệ hoặc không thuộc cùng Project.");
        }

        return null;
    }

    /// <summary>EpicId được denormalize: con trực tiếp của Epic nhận Id của Epic, cháu kế thừa EpicId của cha.</summary>
    public static Guid? ResolveEpicId(IssueHierarchyNode? parent) =>
        parent is null ? null : parent.Type.IsEpic ? parent.Id : parent.EpicId;
}
