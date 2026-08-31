# Phân công module cho ba người

| Nhóm | Project lớn | Logical module | Bảng |
|---|---|---|---:|
| A | `ProjectMgmt.Modules.IdentityExperience` | IdentityAccess, Notification, AiAssist | 14 |
| B | `ProjectMgmt.Modules.Planning` | ProjectManagement, SprintBacklog, AiAssignment | 18 |
| C | `ProjectMgmt.Modules.DeliveryIntelligence` | IssueTracking, AiCore, AiDataOps | 23 |

Quy tắc làm việc:

1. Mỗi entity/table/configuration chỉ có một project sở hữu.
2. Mỗi nhóm quản lý `AppDbContext`, database configuration, repository và service của project mình.
3. Giao tiếp liên nhóm chỉ qua interface/DTO trong `ProjectMgmt.Contracts`.
4. Không truyền EF entity hoặc `DbContext` qua boundary.
5. Controller và composition root nằm trong `ProjectMgmt.Solution`.
6. Repository/service dùng class và constructor truyền thống; không dùng `record`, `sealed`, primary constructor, CQRS hoặc MediatR.
7. Thay đổi contract phải ưu tiên additive và cập nhật `CONTRACTS-CHANGELOG.md`.
