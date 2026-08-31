# ScrumAI Project Management

Modular monolith dùng ASP.NET Core/.NET 10, EF Core + MySQL và Angular. Backend có đúng 7 project để ba thành viên có ranh giới sở hữu rõ ràng.

## Cấu trúc backend

| Project | Vai trò |
|---|---|
| `ProjectMgmt.Solution` | ASP.NET Core Web API, thư mục `Controllers`, cấu hình HTTP và composition root/DI |
| `ProjectMgmt.Core` | Building blocks thuần dùng chung; không chứa EF Core, MySQL hoặc cấu hình kết nối DB |
| `ProjectMgmt.Contracts` | DTO và interface giao tiếp liên module; không chứa EF entity |
| `ProjectMgmt.Modules.IdentityExperience` | IdentityAccess, Notification, AiAssist (14 bảng) |
| `ProjectMgmt.Modules.Planning` | ProjectManagement, SprintBacklog, AiAssignment (18 bảng) |
| `ProjectMgmt.Modules.DeliveryIntelligence` | IssueTracking, AiCore, AiDataOps (23 bảng) |
| `ProjectMgmt.Tests` | Unit, architecture, integration và test fakes |

Mỗi logical module dùng cấu trúc `Domain/IRepositories`, `Application/IServices`, `Application/Services`, `Infrastructure/Repositories` và `Infrastructure/Persistence/Configurations`. Code dùng class, constructor và repository/service truyền thống; không dùng `record`, `sealed`, primary constructor, CQRS hoặc MediatR.

## Database

Ba context cùng đọc `ConnectionStrings:ProjectMgmt`, nhưng tự sở hữu cấu hình provider và bảng của module:

- `IdentityExperienceAppDbContext`
- `PlanningAppDbContext`
- `DeliveryIntelligenceAppDbContext`

Các file `*DatabaseConfiguration.cs` trong từng module cấu hình MySQL, retry, migration history và health check. Các lớp `IEntityTypeConfiguration<T>` trong `Infrastructure/Persistence/Configurations` cấu hình table, column, relationship và index. Tổng cộng 55 bảng được ánh xạ, không trùng quyền sở hữu.

Ứng dụng không gọi `EnsureCreated`, `EnsureDeleted`, `Migrate` hoặc tự chạy DDL. Chuỗi kết nối lưu bằng User Secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:ProjectMgmt" "Server=localhost;Port=3306;Database=projectmgmt;User ID=YOUR_USER;Password=YOUR_PASSWORD;SslMode=Preferred;AllowPublicKeyRetrieval=True" --project .\ProjectMgmt.Solution\ProjectMgmt.Solution.csproj
```

## Chạy và kiểm tra

```powershell
dotnet restore .\ProjectMgmt.slnx
dotnet build .\ProjectMgmt.slnx
dotnet test .\ProjectMgmt.Tests\ProjectMgmt.Tests.csproj
dotnet run --project .\ProjectMgmt.Solution\ProjectMgmt.Solution.csproj --launch-profile http
```

- Live health: `http://localhost:5083/health/live`
- Database readiness: `http://localhost:5083/health`
- OpenAPI (Development): `http://localhost:5083/openapi/v1.json`

Frontend nằm tại `frontend/projectmgmt-web`. Xem thêm [kiến trúc](docs/ARCHITECTURE.md), [phân công module](docs/MODULE-OWNERSHIP.md), [contracts](docs/CONTRACTS.md), [chiến lược test](docs/TEST-STRATEGY.md) và [Docker cục bộ](docs/LOCAL-DOCKER.md).
