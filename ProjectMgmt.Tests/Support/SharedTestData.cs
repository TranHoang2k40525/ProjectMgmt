using System.Globalization;
using DeliveryIntelligence.Domain.Entities;
using DeliveryIntelligence.Domain.UseCases;
using ProjectMgmt.IdentityAccess.Contracts;
using ProjectMgmt.ProjectManagement.Contracts;

namespace ProjectMgmt.Tests.Support;

/// <summary>
/// Bộ seed dùng chung cho A/B/C: Id cố định, cấu hình workflow giống cấu hình mặc định Planning tạo cho Project mới.
/// Unit test dùng trực tiếp các danh sách này; integration test ghi chúng vào MySQL cô lập qua <see cref="MySqlTestDatabase"/>.
/// </summary>
public static class SharedTestData
{
    public static readonly DateTime SeededAtUtc = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    public static readonly Guid OrganizationId = Id(1, 1);
    public static readonly Guid ProjectId = Id(1, 2);
    public static readonly Guid OtherProjectId = Id(1, 3);
    public const string ProjectKey = "DEMO";
    public const string OtherProjectKey = "OTHER";

    public static readonly Guid OwnerUserId = Id(2, 1);
    public static readonly Guid DeveloperUserId = Id(2, 2);
    public static readonly Guid ViewerUserId = Id(2, 3);
    public static readonly Guid OutsiderUserId = Id(2, 4);

    public static readonly Guid EpicTypeId = Id(3, 1);
    public static readonly Guid StoryTypeId = Id(3, 2);
    public static readonly Guid TaskTypeId = Id(3, 3);
    public static readonly Guid BugTypeId = Id(3, 4);
    public static readonly Guid SubtaskTypeId = Id(3, 5);
    public static readonly Guid OtherTaskTypeId = Id(3, 6);

    public static readonly Guid BacklogStatusId = Id(4, 1);
    public static readonly Guid ToDoStatusId = Id(4, 2);
    public static readonly Guid InProgressStatusId = Id(4, 3);
    public static readonly Guid InReviewStatusId = Id(4, 4);
    public static readonly Guid DoneStatusId = Id(4, 5);
    public static readonly Guid OtherInitialStatusId = Id(4, 6);

    public static readonly Guid ActiveSprintId = Id(6, 1);

    /// <summary>Priority Medium do migration SeedPlanningReferenceData tạo sẵn.</summary>
    public static readonly Guid MediumPriorityId = Guid.Parse("33333333-0000-0000-0000-000000000003");

    public static readonly Guid EpicIssueId = Id(7, 1);
    public static readonly Guid StoryIssueId = Id(7, 2);
    public static readonly Guid SubtaskIssueId = Id(7, 3);
    public static readonly Guid BacklogTaskIssueId = Id(7, 4);
    public static readonly Guid BugIssueId = Id(7, 5);
    public static readonly Guid OtherProjectIssueId = Id(7, 6);

    /// <summary>Kỹ năng do migration SeedIdentityReferenceData tạo sẵn.</summary>
    public static readonly Guid DotnetSkillId = Guid.Parse("44444444-0000-0000-0000-000000000001");
    public static readonly Guid AngularSkillId = Guid.Parse("44444444-0000-0000-0000-000000000002");
    public static readonly Guid SqlSkillId = Guid.Parse("44444444-0000-0000-0000-000000000003");

    public static readonly Guid ProjectManagerRoleId = Guid.Parse("11111111-0000-0000-0000-000000000003");
    public static readonly Guid DeveloperRoleId = Guid.Parse("11111111-0000-0000-0000-000000000006");
    public static readonly Guid ViewerRoleId = Guid.Parse("11111111-0000-0000-0000-000000000007");

    public static IReadOnlyList<UserDisplayInfo> Users { get; } =
    [
        new() { UserId = OwnerUserId, DisplayName = "Demo Owner" },
        new() { UserId = DeveloperUserId, DisplayName = "Demo Developer" },
        new() { UserId = ViewerUserId, DisplayName = "Demo Viewer" },
        new() { UserId = OutsiderUserId, DisplayName = "Outside User" }
    ];

    /// <summary>Thành viên Project DEMO và vai trò; OutsiderUserId không thuộc Project nào.</summary>
    public static IReadOnlyList<(Guid UserId, Guid RoleId)> DemoMembers { get; } =
    [
        (OwnerUserId, ProjectManagerRoleId),
        (DeveloperUserId, DeveloperRoleId),
        (ViewerUserId, ViewerRoleId)
    ];

    public static IReadOnlyList<ProjectIssueTypeDto> IssueTypes { get; } =
    [
        new(EpicTypeId, ProjectId, "Epic", "#904EE2", 3, false, 1),
        new(StoryTypeId, ProjectId, "Story", "#65BA43", 2, false, 2),
        new(TaskTypeId, ProjectId, "Task", "#4BADE8", 2, false, 3),
        new(BugTypeId, ProjectId, "Bug", "#E5493A", 2, false, 4),
        new(SubtaskTypeId, ProjectId, "Subtask", "#4BADE8", 1, true, 5),
        new(OtherTaskTypeId, OtherProjectId, "Task", "#4BADE8", 2, false, 1)
    ];

    public static IReadOnlyList<ProjectStatusDto> Statuses { get; } =
    [
        new(BacklogStatusId, ProjectId, "Backlog", "ToDo", "#5E6C84", 1, true),
        new(ToDoStatusId, ProjectId, "To Do", "ToDo", "#42526E", 2, false),
        new(InProgressStatusId, ProjectId, "In Progress", "InProgress", "#0052CC", 3, false),
        new(InReviewStatusId, ProjectId, "In Review", "InProgress", "#FFAB00", 4, false),
        new(DoneStatusId, ProjectId, "Done", "Done", "#36B37E", 5, false),
        new(OtherInitialStatusId, OtherProjectId, "To Do", "ToDo", "#42526E", 1, true)
    ];

    /// <summary>Đường chuyển trạng thái mặc định (giống seed workflow của Planning).</summary>
    public static IReadOnlyList<(Guid From, Guid To, string Name)> Transitions { get; } =
    [
        (BacklogStatusId, ToDoStatusId, "Ready for Sprint"),
        (ToDoStatusId, InProgressStatusId, "Start Work"),
        (InProgressStatusId, InReviewStatusId, "Submit Review"),
        (InReviewStatusId, DoneStatusId, "Accept & Done"),
        (InReviewStatusId, InProgressStatusId, "Needs Rework"),
        (InProgressStatusId, ToDoStatusId, "Stop Work"),
        (DoneStatusId, InProgressStatusId, "Reopen")
    ];

    public static IssueTypeLevel TypeLevel(Guid issueTypeId)
    {
        var type = IssueTypes.Single(t => t.Id == issueTypeId);
        return new IssueTypeLevel(type.Id, type.Name, type.HierarchyLevel, type.IsSubtask);
    }

    public static int IssueCounter(Guid projectId) => CreateIssues().Count(issue => issue.ProjectId == projectId);

    /// <summary>
    /// Cây mẫu của DEMO: DEMO-1 Epic → DEMO-2 Story (Sprint đang chạy) → DEMO-3 Sub-task;
    /// DEMO-4 Task ở Backlog; DEMO-5 Bug đã Done. OTHER-1 dùng cho kiểm tra chặn truy cập chéo Project.
    /// Mỗi lần gọi trả về instance mới để test không ảnh hưởng nhau.
    /// </summary>
    public static List<Issue> CreateIssues() =>
    [
        NewIssue(EpicIssueId, ProjectId, 1, EpicTypeId, "Quản lý tài khoản người dùng", ToDoStatusId, rank: 1),
        NewIssue(StoryIssueId, ProjectId, 2, StoryTypeId, "Người dùng có thể đặt lại mật khẩu", InProgressStatusId, rank: 2,
            parentId: EpicIssueId, epicId: EpicIssueId, sprintId: ActiveSprintId, assigneeId: DeveloperUserId, storyPoints: 5),
        NewIssue(SubtaskIssueId, ProjectId, 3, SubtaskTypeId, "Tạo endpoint gửi OTP", ToDoStatusId, rank: 3,
            parentId: StoryIssueId, epicId: EpicIssueId, sprintId: ActiveSprintId, assigneeId: DeveloperUserId, storyPoints: 2),
        NewIssue(BacklogTaskIssueId, ProjectId, 4, TaskTypeId, "Viết tài liệu hướng dẫn đăng nhập", BacklogStatusId, rank: 4, storyPoints: 1),
        NewIssue(BugIssueId, ProjectId, 5, BugTypeId, "Sửa lỗi hiển thị avatar", DoneStatusId, rank: 5,
            sprintId: ActiveSprintId, assigneeId: OwnerUserId, storyPoints: 0.5m),
        NewIssue(OtherProjectIssueId, OtherProjectId, 1, OtherTaskTypeId, "Issue của Project khác", OtherInitialStatusId, rank: 1)
    ];

    private static Issue NewIssue(
        Guid id,
        Guid projectId,
        int number,
        Guid typeId,
        string title,
        Guid statusId,
        decimal rank,
        Guid? parentId = null,
        Guid? epicId = null,
        Guid? sprintId = null,
        Guid? assigneeId = null,
        decimal? storyPoints = null) => new()
    {
        Id = id,
        ProjectId = projectId,
        IssueNumber = number,
        IssueTypeId = typeId,
        Title = title,
        StatusId = statusId,
        PriorityId = MediumPriorityId,
        ReporterId = OwnerUserId,
        ParentId = parentId,
        EpicId = epicId,
        SprintId = sprintId,
        AssigneeId = assigneeId,
        StoryPoints = storyPoints,
        RankOrder = rank,
        CreatedAt = SeededAtUtc.AddMinutes(number)
    };

    private static Guid Id(int group, int number) =>
        Guid.Parse(string.Create(CultureInfo.InvariantCulture, $"c0de0000-0000-0000-{group:D4}-{number:D12}"));
}
