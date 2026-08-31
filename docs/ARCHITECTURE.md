# Kiến trúc backend

## Quyết định chính

`ProjectMgmt.Solution` là ASP.NET Core Web API và composition root duy nhất. Project này giữ đúng tên `.Solution`, dùng thư mục `Controllers` và chứa endpoint, middleware, OpenAPI, health endpoint cùng đăng ký các module. `.Host` không phải Web API và không nằm trong solution hiện tại.

Backend có đúng 7 project. Ba project nghiệp vụ tương ứng ba nhóm phát triển. Mỗi logical module theo cấu trúc:

```text
<LogicalModule>/
├─ Domain/
│  ├─ Entities/
│  └─ IRepositories/
├─ Application/
│  ├─ IServices/
│  └─ Services/
└─ Infrastructure/
   ├─ Repositories/
   └─ Persistence/
      └─ Configurations/
```

`ProjectMgmt.Contracts` là cổng giao tiếp công khai giữa các project nghiệp vụ. Interface service dùng xuyên module ở `Contracts`; `Application/IServices` dành cho interface chỉ dùng nội bộ module. Không module nghiệp vụ nào reference implementation của module khác.

## Persistence thuộc từng module

`ProjectMgmt.Core` không reference EF Core/Pomelo và không chứa connection helper. Mỗi project nghiệp vụ tự có:

- `<Module>AppDbContext.cs`: constructor thường, `DbSet<T>`, nạp entity configuration và convention kiểu cột;
- `<Module>DatabaseConfiguration.cs`: đọc connection string, cấu hình MySQL/retry/migration history và database health check;
- `Infrastructure/Persistence/Configurations`: các `IEntityTypeConfiguration<T>` cấu hình table, khóa, relationship và index;
- `Infrastructure/Repositories`: implementation của interface trong `Domain/IRepositories`.

| DbContext | Logical module | Số bảng |
|---|---|---:|
| `IdentityExperienceAppDbContext` | IdentityAccess 11 + Notification 1 + AiAssist 2 | 14 |
| `PlanningAppDbContext` | ProjectManagement 10 + SprintBacklog 3 + AiAssignment 5 | 18 |
| `DeliveryIntelligenceAppDbContext` | IssueTracking 14 + AiCore 2 + AiDataOps 7 | 23 |

Cả ba context dùng connection-string key `ProjectMgmt` và migration-history table riêng. GUID liên module là scalar; không có navigation xuyên context.

Runtime không tự tạo, xóa hay migrate schema. `/health/live` chỉ kiểm tra process; `/health` gọi health check kết nối của ba module và trả 503 nếu một module không thể kết nối DB.

## Quy ước code

Mã nguồn dùng class và constructor truyền thống. Không dùng `record`, `sealed` hoặc primary constructor. Rule analyzer đề nghị đóng class bằng `sealed` được tắt có chủ đích để giữ đúng quy ước này.
