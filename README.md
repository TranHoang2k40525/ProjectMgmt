# ScrumAI Project Management

Modular monolith dùng ASP.NET Core/.NET 10, EF Core + MySQL và Angular. Backend đã được thu gọn từ 22 xuống đúng 7 project để ba thành viên có ranh giới sở hữu rõ ràng.

## Cấu trúc backend

| Project | Vai trò |
|---|---|
| `ProjectMgmt.Solution` | ASP.NET Core Web API, controller, cấu hình và composition root/DI |
| `ProjectMgmt.Core` | Building blocks dùng chung, persistence/web helpers |
| `ProjectMgmt.Contracts` | DTO và interface giao tiếp liên module; không chứa EF entity |
| `ProjectMgmt.Modules.IdentityExperience` | Nhóm A: IdentityAccess, Notification, AiAssist (14 bảng) |
| `ProjectMgmt.Modules.Planning` | Nhóm B: ProjectManagement, SprintBacklog, AiAssignment (18 bảng) |
| `ProjectMgmt.Modules.DeliveryIntelligence` | Nhóm C: IssueTracking, AiCore, AiDataOps (23 bảng) |
| `ProjectMgmt.Tests` | Unit, architecture, integration và test fakes trong một project |

Mỗi project nghiệp vụ được tổ chức theo `Domain`, `Application`, `Infrastructure`; repository theo kiểu truyền thống, không dùng CQRS/MediatR. Các module không reference trực tiếp nhau mà giao tiếp qua `ProjectMgmt.Contracts`.

## Database

Ba `DbContext` cùng đọc một key `ConnectionStrings:ProjectMgmt`:

- `IdentityExperienceDbContext`
- `PlanningDbContext`
- `DeliveryIntelligenceDbContext`

Code chứa đủ 55 entity và EF configuration theo `projectmgmt_schema_mysql.sql`. Ứng dụng không gọi `EnsureCreated`, `EnsureDeleted`, `Migrate` hoặc tự chạy DDL.

Lưu chuỗi kết nối bằng User Secrets, không ghi mật khẩu vào git:

```powershell
dotnet user-secrets set "ConnectionStrings:ProjectMgmt" "Server=localhost;Port=3306;Database=projectmgmt;User ID=YOUR_USER;Password=YOUR_PASSWORD;SslMode=Preferred;AllowPublicKeyRetrieval=True" --project .\ProjectMgmt.Solution\ProjectMgmt.Solution.csproj
```

`/health/live` chỉ kiểm tra process. `/health` kiểm tra cả kết nối và độ tương thích bảng/cột của từng `DbContext`; endpoint trả 503 nếu database đang phát triển chưa đủ schema.

## Chạy backend

```powershell
dotnet restore .\ProjectMgmt.slnx
dotnet build .\ProjectMgmt.slnx
dotnet test .\ProjectMgmt.Tests\ProjectMgmt.Tests.csproj
dotnet run --project .\ProjectMgmt.Solution\ProjectMgmt.Solution.csproj --launch-profile http
```

- Live health: `http://localhost:5083/health/live`
- DB/schema readiness: `http://localhost:5083/health`
- OpenAPI (Development): `http://localhost:5083/openapi/v1.json`

## Chạy frontend

```powershell
Set-Location .\frontend\projectmgmt-web
cmd /c npm ci
cmd /c npm start
```

Xem thêm [kiến trúc](docs/ARCHITECTURE.md), [phân công module](docs/MODULE-OWNERSHIP.md), [contracts](docs/CONTRACTS.md) và [chiến lược test](docs/TEST-STRATEGY.md).
