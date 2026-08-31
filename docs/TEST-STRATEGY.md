# Chiến lược test

Toàn bộ backend test nằm trong `ProjectMgmt.Tests`:

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

Phạm vi hiện tại:

- Unit: result, security convention và contract fake.
- Architecture: đúng 7 project; không reference chéo module; `.Solution` là Web API/composition root; ba EF model sở hữu đúng 14/18/23 = 55 bảng không trùng.
- Integration: `/health/live`, readiness khi thiếu credential và `/api/system/info` qua `WebApplicationFactory<Program>`.

CI không kết nối database thật của developer. Test DB phải dùng instance cô lập. Runtime và test không tự chạy file DDL có lệnh phá hủy dữ liệu.
