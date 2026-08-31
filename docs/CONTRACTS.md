# Cross-module contracts

`ProjectMgmt.Contracts` là project duy nhất chứa interface và DTO dùng giữa ba project nghiệp vụ. Project này chỉ reference `ProjectMgmt.Core` và không chứa EF type/entity.

## IdentityExperience cung cấp

- `IUserLookupService`: kiểm tra user và lấy display info.
- `IUserSkillService`: lấy skill/profile feature cho phân công AI.
- `INotificationSender`: ghi thông báo mà consumer không cần biết bảng `Notification`.
- `IAiBreakdownFeedbackExportService`: xuất feedback AI breakdown cho DataOps.

## Planning cung cấp

- `IProjectLookupService`: project key, issue type và workflow status.
- `IWorkflowValidationService`: transition, initial status và WIP limit.
- `IIssueNumberGenerator`: cấp số issue trong transaction có row lock.
- `ISprintLookupService`: kiểm tra sprint/project mà không lộ entity.
- `IAiAssignmentFeedbackExportService`: xuất nhãn quyết định phân công.

## DeliveryIntelligence cung cấp

- `IIssueService`, `IIssueReadService`: tạo/đọc issue.
- `IIssueSprintService`: chuyển issue giữa backlog và sprint.
- `IIssueSkillService`: skill yêu cầu của issue.
- `IIssueWorkInProgressCounter`: số issue theo status để Planning kiểm tra WIP.
- `IEvaluationSetCatalog`: metadata evaluation set đã đóng băng.

Tất cả API async nhận `CancellationToken`. DTO dùng GUID/string/value type; không đưa navigation hoặc persistence concern qua boundary.
