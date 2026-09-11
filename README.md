# ProjectMgmt — Modular Monolith

Backend được tổ chức thành đúng ba business module:

1. `DeliveryIntelligence`
2. `IdentityExperience`
3. `Planning`

Mỗi module là một solution folder trong `ProjectMgmt.slnx` và chứa ba class library thật: `Domain`, `Application`, `Infrastructure`. `ProjectMgmt.Solution` là ASP.NET Core host/composition root; toàn bộ đăng ký DI nằm trực tiếp trong `Program.cs`.

## Database hiện tại

Schema chuẩn: `docs/projectmgmt_schema_mysql_optimized.sql` (MySQL 8.0.16+, database `projectmgmt`).

EF Core đã được hiện thực đầy đủ theo schema:

- 55 entity cho 55 bảng và 4 entity keyless cho 4 view.
- 59 `IEntityTypeConfiguration<T>`.
- 3 `DbContext`, mỗi module sở hữu một context và một bảng lịch sử migration riêng.
- 6 migration, gồm bảng/index/FK/CHECK/default/computed column/comment/seed, 4 view và 2 trigger.
- Pomelo EF Core MySQL và `dotnet-ef` được khóa phiên bản tại repository.

Connection string chỉ được đọc từ host bằng khóa `ConnectionStrings:ProjectMgmt`. `appsettings.json` không chứa mật khẩu. Khi phát triển, dùng User Secrets; khi chạy IIS/production, dùng biến môi trường hoặc secret store:

```powershell
dotnet user-secrets --project .\ProjectMgmt.Solution set `
  "ConnectionStrings:ProjectMgmt" `
  "Server=127.0.0.1;Port=3306;Database=projectmgmt;User=root;Password=<MẬT_KHẨU>;SslMode=None"
```

`SslMode=None` chỉ phù hợp với MySQL local đang dùng. Môi trường thật phải cấu hình TLS phù hợp, ưu tiên `VerifyCA` hoặc `VerifyFull`.

## Tài liệu

- Kế hoạch nguồn có thể chỉnh sửa: `docs/plane.txt`
- Báo cáo HTML đầy đủ: `docs/project-structure-guide.html`

Lưu ý: database `projectmgmt` hiện hữu chưa có ba bảng lịch sử EF Migration. Không chạy các initial migration lên database đó trước khi thực hiện baseline. Quy trình an toàn được ghi rõ trong tài liệu HTML.
