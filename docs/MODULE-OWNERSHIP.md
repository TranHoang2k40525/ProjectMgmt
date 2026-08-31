# Phân công module cho ba người

| Nhóm | Project lớn | Logical module | Bảng |
|---|---|---|---:|
| A | `ProjectMgmt.Modules.IdentityExperience` | IdentityAccess, Notification, AiAssist | 14 |
| B | `ProjectMgmt.Modules.Planning` | ProjectManagement, SprintBacklog, AiAssignment | 18 |
| C | `ProjectMgmt.Modules.DeliveryIntelligence` | IssueTracking, AiCore, AiDataOps | 23 |

Quy tắc làm việc:

1. Mỗi entity/table/configuration chỉ có một project sở hữu.
2. Không thêm project nghiệp vụ mới nếu logical module có thể nằm trong một trong ba khối trên.
3. Giao tiếp liên nhóm chỉ qua interface/DTO trong `ProjectMgmt.Contracts`.
4. Không truyền EF entity hoặc `DbContext` qua boundary.
5. API và DI nằm trong `ProjectMgmt.Solution`; module tự cung cấp extension đăng ký dịch vụ của mình.
6. Repository là kiểu truyền thống; không đưa CQRS/MediatR vào nếu chưa có quyết định kiến trúc mới.
7. Thay đổi contract phải ưu tiên additive và cập nhật `CONTRACTS-CHANGELOG.md`.
