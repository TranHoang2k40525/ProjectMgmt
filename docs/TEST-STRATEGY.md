# Chiến lược test

Toàn bộ backend test nằm trong một project `ProjectMgmt.Tests`, chia bằng thư mục thay vì nhiều `.csproj`:

```text
ProjectMgmt.Tests/
├─ Unit/
├─ Architecture/
├─ Integration/
└─ Fakes/
```

Chạy:

```powershell
dotnet test .\ProjectMgmt.Tests\ProjectMgmt.Tests.csproj
```

Các lớp kiểm tra hiện có:

- Unit: `Result`, security convention và contract fake.
- Architecture: solution đúng 7 project; ba module không reference chéo; Web API là composition root; ba EF model sở hữu đúng 14/18/23 = 55 bảng và không trùng.
- Integration: `/health/live`, readiness khi không có credential và `/api/system/info` qua `WebApplicationFactory<Program>`.

CI không được tự kết nối database thật của developer. Test DB sau này phải dùng instance cô lập và không tự chạy file DDL có lệnh phá hủy. Schema readiness trong ứng dụng production/development là kiểm tra chỉ đọc, không phải migration.
