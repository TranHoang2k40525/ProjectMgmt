# Sprint 1 Foundation Completion

Ngày hoàn tất: 2026-08-30 (Asia/Ho_Chi_Minh)

## 1. Current tree sau sửa

Các thư mục sinh tự động (`bin`, `obj`, `node_modules`, `dist`, `.angular`, `.vs`) được lược bỏ:

```text
ProjectMgmt/
├─ ProjectMgmt.Solution/                 API Host / composition root
├─ ProjectMgmt.Core/                     BuildingBlocks-equivalent
├─ ProjectMgmt.IdentityAccess[.Contracts]/
├─ ProjectMgmt.ProjectManagement[.Contracts]/
├─ ProjectMgmt.SprintBacklog/
├─ ProjectMgmt.IssueTracking[.Contracts]/
├─ ProjectMgmt.AiCore/
├─ ProjectMgmt.AiAssist[.Contracts]/
├─ ProjectMgmt.AiAssignment[.Contracts]/
├─ ProjectMgmt.AiDataOps[.Contracts]/
├─ ProjectMgmt.Notification[.Contracts]/
├─ ProjectMgmt.Tests.Unit/
├─ ProjectMgmt.Tests.Integration/
├─ ProjectMgmt.Tests.Architecture/
├─ ProjectMgmt.Tests.Fakes/
├─ frontend/projectmgmt-web/
│  └─ src/app/{core,layouts,features,shared}/
├─ data/ai-evaluation/v1/
├─ docs/
├─ scripts/
├─ .github/workflows/ci.yml
├─ .github/CODEOWNERS
├─ Directory.Build.props
├─ Directory.Packages.props
├─ docker-compose.yml
├─ ProjectMgmt.slnx
└─ README.md
```

Solution hiện có 22 project: Host, BuildingBlocks-equivalent, chín implementation module, bảy Contracts project và bốn test/fake project.

## 2. Danh sách file tạo mới

- Root/tooling: `.editorconfig`, `.env.example`, `.gitignore`, `Directory.Build.props`, `Directory.Packages.props`, `README.md`, `docker-compose.yml`, `CONTRACTS-CHANGELOG.md`.
- GitHub: `.github/workflows/ci.yml`, `.github/CODEOWNERS`.
- BuildingBlocks trong project được giữ tên `ProjectMgmt.Core`: `Common/CommonTypes.cs`, `Domain/Primitives.cs`, `Exceptions/ProjectMgmtExceptions.cs`, `Persistence/MySqlPersistenceExtensions.cs`, `Results/Result.cs`, `Security/SecurityConventions.cs`, `Web/WebCommonExtensions.cs`.
- Contracts: toàn bộ file/project trong `ProjectMgmt.IdentityAccess.Contracts`, `ProjectMgmt.ProjectManagement.Contracts`, `ProjectMgmt.IssueTracking.Contracts`, `ProjectMgmt.Notification.Contracts`, `ProjectMgmt.AiAssist.Contracts`, `ProjectMgmt.AiAssignment.Contracts`, `ProjectMgmt.AiDataOps.Contracts`.
- Implementation module mới: toàn bộ file/project trong `ProjectMgmt.SprintBacklog`, `ProjectMgmt.AiCore`, `ProjectMgmt.AiAssignment`, `ProjectMgmt.AiDataOps`, `ProjectMgmt.Notification`.
- Registration/DbContext mới trong module có sẵn: `IdentityAccessModuleRegistration.cs`, `ProjectManagementModuleRegistration.cs`, `IssueTrackingModuleRegistration.cs`, `AiAssistModuleRegistration.cs`.
- Tests/fakes: toàn bộ file/project trong `ProjectMgmt.Tests.Unit`, `ProjectMgmt.Tests.Integration`, `ProjectMgmt.Tests.Architecture`, `ProjectMgmt.Tests.Fakes`.
- Angular: toàn bộ `frontend/projectmgmt-web`, gồm config/lockfile, app shell, core services/guards/interceptors/realtime, tám lazy feature pages, shared components và environments.
- AI evaluation: `data/ai-evaluation/v1/evaluation.jsonl`, `README.md`, `rubric.md`.
- Scripts: `scripts/validate-ai-evaluation.ps1`, `scripts/validate-xmod-indexes.ps1`, `scripts/pull-ollama-model.ps1`.
- Docs: `ARCHITECTURE.md`, `MODULE-OWNERSHIP.md`, `CONTRACTS.md`, `LOCAL-DOCKER.md`, `JIRA-CONVENTIONS.md`, `TEST-STRATEGY.md`, `DEFINITION-OF-READY.md`, `DEFINITION-OF-DONE.md`, `SEVERITY-MATRIX.md`, `BUG-TEMPLATE.md`, `Sprint1-Audit.md`, và file completion này.

## 3. Danh sách file sửa/xóa

Đã sửa:

- `ProjectMgmt.slnx` để chứa đủ 22 project.
- Các `.csproj` có sẵn của Host, Core, IdentityAccess, ProjectManagement, IssueTracking, AiAssist để dùng central package versions và reference đúng boundary.
- `ProjectMgmt.Solution/Program.cs`, `appsettings.json`, `appsettings.Development.json`, `ProjectMgmt.Solution.http` để tạo composition root, CORS, logging, OpenAPI và health endpoints.

Đã bỏ template/placeholder không còn giá trị: các `Class1.cs`, `WeatherForecast.cs`, `WeatherForecastController.cs`, cùng project placeholder `ProjectMgmt.Tests` cũ. Không xóa business code vì repository ban đầu chưa có business implementation.

## 4. Module và Contracts đã hoàn thành

Chín implementation module đều có registration và DbContext riêng: IdentityAccess, ProjectManagement, SprintBacklog, IssueTracking, AiCore, AiAssist, AiAssignment, AiDataOps, Notification.

Contracts công khai đã freeze cho:

- IdentityAccess: user lookup, public display DTO, user skills và profile features.
- ProjectManagement/SprintBacklog: project lookup, workflow/WIP validation và issue-number generator.
- IssueTracking: create/read/sprint/required-skill services, DTO, Result và pagination.
- Notification: notification sender/DTO.
- AiAssist và AiAssignment: hai feedback exporter có tên không mơ hồ.
- AiDataOps: evaluation-set catalog.

Không Contracts project nào reference implementation hoặc EF entity. Implementation không reference implementation module khác; các quy tắc này được bảo vệ bằng architecture tests.

## 5. BuildingBlocks đã có gì

- Entity/AggregateRoot, audit, soft delete, domain event và Unit of Work abstraction.
- `Error`, `ErrorType`, `Result`, `Result<T>`, validation details.
- Shared exception hierarchy và global `IExceptionHandler` xuất ProblemDetails thống nhất.
- Correlation ID, Serilog request logging, UTC clock, pagination và Guard.
- Claim names, scope, permission-code convention, `ICurrentUser`/`HttpCurrentUser`.
- Helper đăng ký Pomelo MySQL cho từng module, retry và health checks.

Tên project `ProjectMgmt.Core` được giữ để tránh đổi namespace/layout không cần thiết; API public dùng namespace `ProjectMgmt.BuildingBlocks.*`.

## 6. DB connection cho từng module

Tất cả module đọc đúng một logical key `ConnectionStrings:ProjectMgmt`; environment key là `ConnectionStrings__ProjectMgmt`. `appsettings.json` chỉ chứa chuỗi rỗng, không có credential. Development dùng User Secrets hoặc environment.

Mỗi registration gọi `AddProjectMgmtMySqlDbContext<TContext>` với:

- Pomelo `UseMySql`, server baseline MySQL 8.0.16;
- retry tối đa năm lần, độ trễ tối đa 10 giây;
- migration assembly là assembly của module;
- history table riêng;
- một readiness health check có tag `db`, `ready` và module name;
- sensitive-data logging chỉ có thể bật đồng thời ở Development và qua config explicit.

Không có `EnsureCreated`, `EnsureDeleted`, startup `Migrate`, schema import hoặc DDL execution. DDL gốc không bị sửa/chạy.

## 7. Migration history names

- `__EFMigrationsHistory_IdentityAccess`
- `__EFMigrationsHistory_ProjectManagement`
- `__EFMigrationsHistory_SprintBacklog`
- `__EFMigrationsHistory_IssueTracking`
- `__EFMigrationsHistory_AiCore`
- `__EFMigrationsHistory_AiAssist`
- `__EFMigrationsHistory_AiAssignment`
- `__EFMigrationsHistory_AiDataOps`
- `__EFMigrationsHistory_Notification`

## 8. Angular structure

Angular 21 standalone SPA nằm tại `frontend/projectmgmt-web`, không SSR và không có nested `.git`. App có:

- app shell/navigation và signal hiển thị backend online/offline;
- lazy route `/auth`, `/projects`, `/backlog`, `/board`, `/issues/:id`, `/notifications`, `/reports`, `/ai-dataops`;
- `provideHttpClient`, auth/error interceptors, token store, auth/permission guard skeleton;
- SignalR client skeleton với automatic reconnect nhưng không tự connect;
- API/error/loading models, development/production environments;
- shared avatar, AI badge, confirm dialog, AI breakdown và feature placeholder.

Các page chỉ là foundation placeholder; chưa triển khai nghiệp vụ Sprint sau.

## 9. Docker và CI

Compose khai báo MySQL 8.4 và Ollama, named volumes, health checks, utf8mb4, `lower_case_table_names=1`, credentials qua `.env`, không mount/chạy DDL. Script Ollama pull mặc định `qwen2.5:3b`. Hướng dẫn start/stop/log/reset an toàn nằm trong `docs/LOCAL-DOCKER.md`.

GitHub Actions có ba job: backend restore/build/test, frontend `npm ci`/lint/test/build, và validation Docker Compose + AI dataset. Không có deploy hoặc secret trong workflow. Repo ban đầu không có Git metadata/remote, nên GitHub Actions là default repo-ready; CODEOWNERS đang dùng handle-shaped placeholder cần thay bằng username thật.

## 10. Test strategy docs

Đã tạo test strategy, DoR, DoD, severity matrix và bug template. Test code gồm unit tests cho Result/security/fake, integration tests cho Host health/system info, architecture tests cho project/assembly boundaries, và deterministic configurable fakes cho toàn bộ cross-module interface bắt buộc.

## 11. AI evaluation dataset

`evaluation.jsonl` có 60 record hợp lệ, cân bằng 30 Việt/30 Anh. Phân bố: auth-security 8, CRUD 8, workflow 8, backlog-sprint-board 8, reporting 7, notification-realtime 7, AI-data 7, devops-deployment 7. Mỗi record có input, expected sub-tasks, acceptance criteria, tags và reviewer status. Validator còn kiểm tra schema tối thiểu, ID trùng và giới hạn 50–100 mẫu.

## 12. Kết quả command

| Kiểm tra | Kết quả |
|---|---|
| `dotnet restore ProjectMgmt.slnx` | PASS, restore đủ 22 project |
| `dotnet build ProjectMgmt.slnx -c Release --no-restore` | PASS, 0 error, 0 warning |
| `dotnet test ProjectMgmt.slnx -c Release --no-build` | PASS, 16/16 test sau kiểm tra readiness cuối |
| `dotnet package list --project ProjectMgmt.slnx --vulnerable --include-transitive --no-restore` | PASS, không project nào có package vulnerability đã biết từ NuGet sources hiện tại |
| `dotnet format ProjectMgmt.slnx --verify-no-changes --no-restore` | PASS |
| `npm install` | PASS, lockfile tạo thành công, audit 0 vulnerability |
| `npm run lint` | PASS, không warning/error |
| `npm test -- --watch=false` | PASS, 2/2 test |
| `npm run build` | PASS, production initial bundle khoảng 249 kB raw |
| `validate-ai-evaluation.ps1` | PASS, 60 record |
| `validate-xmod-indexes.ps1` | Expected FAIL/report-only: 64 XMOD, 38 thiếu usable left-prefix index |
| `docker compose config` | NOT RUN: máy hiện tại không có Docker CLI |
| Prettier parse/check cho Compose và CI YAML | PASS; đây chỉ là syntax/format check, không thay thế Compose schema validation |
| `/health/live` | PASS/HTTP 200 qua integration host |
| `/health` DB readiness | Expected HTTP 503 khi connection string bị để rỗng; chưa kiểm DB vật lý vì không có credential |

Angular test/build cần chạy ngoài filesystem sandbox của phiên này vì resolver dò các thư mục cha; đây không phải hạn chế của source và cả hai command đều pass khi chạy trong môi trường Windows bình thường.

## 13. Còn thiếu và lý do

- Không thể chạy `docker compose config/up` vì máy không có Docker CLI. CI sẽ chạy `docker compose config`; local start vẫn là manual để tránh đụng MySQL hiện có trên port 3306.
- Không thể xác nhận DB readiness pass vì repository/environment không cung cấp credential. Không bịa secret và không thử sửa/migrate DB thật.
- DDL audit tìm thấy 38/64 XMOD chưa có index ở vị trí left-prefix. Chỉ báo cáo; cần DBA/nhóm xác nhận trước khi tạo migration/ALTER.
- Pomelo stable mới nhất tương thích EF Core 9, chưa có stable EF Core 10 ở thời điểm thực hiện. Host vẫn target .NET 10; persistence pin EF Core 9.0.19 + Pomelo 9.0.0 và C# 13 trong một helper cô lập để nâng cấp sau.
- Tại thời điểm audit ban đầu, workspace chưa phải Git repo. Đến lượt kiểm tra cuối, root đã xuất hiện một Git repository rỗng ở nhánh `main`, không commit và không remote (có metadata `gk`, cho thấy công cụ Git bên ngoài đã khởi tạo trong lúc làm). Không có initial hash để ghi và phiên này không commit/reset/clean hay chỉnh `.git`.

## 14. TODO cố ý để Sprint sau

- 55 business entities/configurations và migrations có review; full auth/JWT; CRUD issue/project/sprint/board.
- SignalR hub/notification delivery thật; AI breakdown/assignment/training business flows.
- Mapster mappings khi xuất hiện mapping thật; Hangfire khi có background job thật.
- Kết nối/pull Ollama model, integration test với MySQL isolated container và coverage threshold.
- Chốt GitHub usernames trong CODEOWNERS và xử lý XMOD index qua change được duyệt.

## 15. Lệnh ngắn để chạy từ máy mới

```powershell
dotnet restore .\ProjectMgmt.slnx
dotnet user-secrets set "ConnectionStrings:ProjectMgmt" "Server=localhost;Port=3306;Database=projectmgmt;User=YOUR_USER;Password=YOUR_PASSWORD;CharSet=utf8mb4;" --project .\ProjectMgmt.Solution\ProjectMgmt.Solution.csproj
dotnet run --project .\ProjectMgmt.Solution\ProjectMgmt.Solution.csproj --launch-profile http

Set-Location .\frontend\projectmgmt-web
cmd /c npm ci
cmd /c npm start
```

Sau khi có Docker Desktop, copy `.env.example` thành `.env`, thay toàn bộ `CHANGE_ME`, rồi chạy `docker compose config` trước. Không chạy Compose trên port 3306 nếu MySQL hiện có đang dùng port đó.
