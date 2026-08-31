# Kiến trúc backend

## Quyết định chính

`ProjectMgmt.Solution` là ASP.NET Core Web API và composition root duy nhất. Không có Web project `.Host`; tên `.Solution` được giữ theo yêu cầu và chứa controller, middleware, cấu hình, OpenAPI, health checks và đăng ký DI.

Backend gồm đúng 7 project. Ba project nghiệp vụ là ba khối lớn tương ứng ba người. Bên trong mỗi khối, các logical module vẫn có thư mục riêng và được chia theo:

```text
<LogicalModule>/
├─ Domain/
│  ├─ Entities/
│  └─ Repositories/
├─ Application/
│  └─ Services/
└─ Infrastructure/
   └─ Persistence/
      └─ Configurations/
```

Kiến trúc dùng repository/service truyền thống, không CQRS, không MediatR. `ProjectMgmt.Contracts` là cổng giao tiếp công khai; module implementation chỉ reference `Core` và `Contracts`, không reference implementation của nhóm khác.

## Quyền sở hữu dữ liệu

| DbContext | Logical module | Số bảng |
|---|---|---:|
| `IdentityExperienceDbContext` | IdentityAccess 11 + Notification 1 + AiAssist 2 | 14 |
| `PlanningDbContext` | ProjectManagement 10 + SprintBacklog 3 + AiAssignment 5 | 18 |
| `DeliveryIntelligenceDbContext` | IssueTracking 14 + AiCore 2 + AiDataOps 7 | 23 |

Tổng cộng 55 bảng, không trùng quyền sở hữu. Cả ba context dùng cùng database `projectmgmt` qua cùng connection-string key, nhưng có migrations-history name riêng:

- `__EFMigrationsHistory_IdentityExperience`
- `__EFMigrationsHistory_Planning`
- `__EFMigrationsHistory_DeliveryIntelligence`

Các GUID XMOD được giữ dạng scalar. Business validation qua contract service; không tạo navigation xuyên `DbContext`. Các foreign key vật lý đã có trong DDL vẫn do database bảo vệ.

## Đồng bộ code và database

Database và code được phát triển song song, vì vậy runtime không tự tạo hoặc sửa schema. Không có `EnsureCreated`, `EnsureDeleted`, startup migration hay tự import DDL.

Readiness gồm hai lớp cho từng context:

1. kết nối được MySQL;
2. toàn bộ bảng/cột mà context sở hữu đang tồn tại trong schema hiện hành.

Kiểm tra thứ hai chỉ đọc `INFORMATION_SCHEMA.COLUMNS`. Khi DBA chưa áp dụng đủ DDL, `/health` trả 503 và liệt kê phần thiếu mà không thay đổi database.

## Provider

Solution target .NET 10. Pomelo stable hiện dùng EF Core 9, nên package được pin tập trung tại `Directory.Packages.props`: EF Core 9.0.19 và Pomelo 9.0.0. Toàn bộ setup provider nằm trong `AddProjectMgmtMySqlDbContext<TContext>` để có thể nâng cấp tập trung sau này.
