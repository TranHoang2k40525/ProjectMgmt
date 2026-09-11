# ProjectMgmt — bộ khung Modular Monolith

Repository này là bộ khung sạch để bắt đầu viết backend. Hệ thống có đúng ba business module:

1. `DeliveryIntelligence`
2. `IdentityExperience`
3. `Planning`

`ProjectMgmt.Solution`, `ProjectMgmt.Core`, `ProjectMgmt.Contracts` và `ProjectMgmt.Tests` là project hỗ trợ, không phải business module.

## Cây hiển thị trong Solution Explorer

```text
Modules
├─ DeliveryIntelligence                   solution folder, không phải project
│  ├─ DeliveryIntelligence.Domain         class library
│  ├─ DeliveryIntelligence.Application    class library
│  └─ DeliveryIntelligence.Infrastructure class library
├─ IdentityExperience
│  ├─ IdentityExperience.Domain
│  ├─ IdentityExperience.Application
│  └─ IdentityExperience.Infrastructure
└─ Planning
   ├─ Planning.Domain
   ├─ Planning.Application
   └─ Planning.Infrastructure
```

Không có project façade `ProjectMgmt.Modules.<Module>`. Một file `.csproj` không thể chứa các `.csproj` khác như thư mục con trong Solution Explorer; `solution folder` trong `ProjectMgmt.slnx` mới là phần tử dùng để nhóm ba class library của mỗi module.

## Cấu trúc vật lý của mỗi module

```text
ProjectMgmt.Modules.<Module>/
├─ Domain/
│  ├─ <Module>.Domain.csproj
│  ├─ Entities/
│  ├─ IRepositories/
│  └─ UseCases/
├─ Application/
│  ├─ <Module>.Application.csproj
│  ├─ IServices/
│  ├─ Services/
│  └─ Dto/
└─ Infrastructure/
   ├─ <Module>.Infrastructure.csproj
   ├─ <Module>DbContext.cs
   ├─ Configurations/
   ├─ Repositories/
   └─ BusinessLogicLayer/
```

Mỗi thư mục khung có một file `.cs` rỗng để Git lưu được cấu trúc. Các file này chưa chứa implementation. Khi viết tính năng, dùng file trực tiếp trong các thư mục chung trên; không tạo sẵn một cây thư mục sâu cho từng feature. Namespace mặc định có thể giữ ngắn theo project, ví dụ:

```csharp
IdentityExperience.Domain
IdentityExperience.Application
IdentityExperience.Infrastructure
```

## Trách nhiệm và chiều phụ thuộc

- `Domain`: entity, quy tắc nghiệp vụ, repository interface và use case/mapping nghiệp vụ không phụ thuộc thư viện ngoài.
- `Application`: service interface, service, DTO, validation và điều phối use case.
- `Infrastructure`: EF Core, MySQL, DbContext, configuration, repository implementation và truy vấn database nặng.
- `BusinessLogicLayer`: wrapper C# gọi procedure/view hoặc raw query đã tối ưu; procedure, view, trigger và index thật phải được quản lý trong SQL/migration.

Chiều `ProjectReference` hiện tại:

```text
Application    -> Domain + ProjectMgmt.Contracts
Infrastructure -> Application + Domain
Solution       -> ba Application + ba Infrastructure + Foundation
```

`Application` không tham chiếu `Infrastructure`. Service nhận interface qua constructor; `Program.cs` nối interface với implementation của Infrastructure. Cách này tránh vòng tham chiếu.

Nhiều `ProjectReference` tại host không làm solution hỏng. Lỗi chỉ xuất hiện khi đường dẫn sai, phiên bản framework/package không tương thích hoặc có vòng tham chiếu. `ProjectMgmt.Solution` là composition root nên việc nó nhìn thấy các Application và Infrastructure là có chủ đích.

## DI và giao tiếp giữa module

Toàn bộ DI được đặt trực tiếp trong `ProjectMgmt.Solution/Program.cs`. Khi bắt đầu có code, đăng ký theo mẫu:

```csharp
builder.Services.AddScoped<IIdentityRepository, IdentityRepository>();
builder.Services.AddScoped<IIdentityService, IdentityService>();
```

Constructor của service chỉ nhận interface:

```csharp
public IdentityService(IIdentityRepository repository)
{
    _repository = repository;
}
```

Các module không tham chiếu implementation của nhau. Dữ liệu cần trao đổi liên module đi qua kiểu dữ liệu/interface/event công khai trong `ProjectMgmt.Contracts`; host hoặc một application service điều phối lời gọi. `Contracts` không thay thế các reference nội bộ giữa Domain, Application và Infrastructure.

## MySQL và cấu hình

Schema nguồn là `docs/projectmgmt_schema_mysql_optimized.sql`, database có tên `projectmgmt`. EF Core và Pomelo MySQL đã được khai báo tập trung trong `Directory.Packages.props` và các project Infrastructure.

Connection string dùng chung được đọc bằng khóa `ConnectionStrings:ProjectMgmt`. Không lưu `root/123456` trong class library hoặc commit vào `appsettings.json`. Với máy local, dùng User Secrets:

```powershell
dotnet user-secrets --project .\ProjectMgmt.Solution set "ConnectionStrings:ProjectMgmt" "Server=localhost;Port=3306;Database=projectmgmt;User=root;Password=123456"
```

Khi triển khai, dùng biến môi trường `ConnectionStrings__ProjectMgmt` hoặc secret store của máy chủ/IIS.

## AI

Hai thư mục sau được chủ động để hoàn toàn rỗng vì AI sẽ được xây riêng sau và cung cấp API cho module tương ứng:

- `ProjectMgmt.Modules.IdentityExperience/AiAssist`
- `ProjectMgmt.Modules.Planning/AiAssignment`

Chúng nằm ngang cấp với `Domain`, `Application`, `Infrastructure`, không nằm trong Application. Vì Git không lưu thư mục rỗng và dự án không dùng `.gitkeep`, hai thư mục này chỉ tồn tại trên máy local cho đến khi có file AI thật.

## Tài liệu

- Kế hoạch có thể chỉnh sửa: `docs/plane.txt`
- Bản HTML trình bày cấu trúc và kế hoạch: `docs/project-structure-guide.html`
