# Test harness và seed dữ liệu dùng chung

Phạm vi: SCRUMAI-168. Bộ hỗ trợ nằm trong `ProjectMgmt.Tests/Support/`, dùng chung cho A/B/C.

| Thành phần | Dùng để |
|---|---|
| `SharedTestData` | Id cố định cho tổ chức, hai Project (`DEMO`, `OTHER`), bốn người dùng (owner, developer, viewer, người ngoài), 5 loại Issue, workflow mặc định 5 trạng thái/7 transition, một Sprint đang chạy và cây Issue mẫu `DEMO-1 … DEMO-5`, `OTHER-1`. |
| `FakeProjectLookupService`, `FakeUserLookupService`, `FakePermissionEvaluator` | Fake contract liên module để phát triển song song. Chỉ dùng trong test, không đăng ký vào Host. |
| `InMemoryIssueRepository` | Fake `IIssueRepository` có cùng thứ tự/lọc/phân trang với repository thật. |
| `MySqlTestDatabase` + `[MySqlFact]` | Integration test trên MySQL thật: tạo database tạm `projectmgmt_test_<ngẫu nhiên>`, chạy migration Identity → Planning → Delivery, seed `SharedTestData`, xóa database khi chạy xong. |

## Chạy integration test MySQL

Mặc định các test `[MySqlFact]` được bỏ qua, nên CI không cần MySQL. Để chạy trên máy cá nhân:

```powershell
$env:PROJECTMGMT_TEST_MYSQL = "Server=127.0.0.1;Port=3306;User=<user_test>;Password=<mật_khẩu>;SslMode=None;AllowPublicKeyRetrieval=True"
dotnet test ProjectMgmt.Tests/ProjectMgmt.Tests.csproj
```

- Tài khoản cần quyền `CREATE`/`DROP DATABASE` và tạo trigger/view. Phần `Database=` trong chuỗi kết nối bị bỏ qua: harness luôn tự tạo database riêng, không ghi vào `projectmgmt`.
- Không commit chuỗi kết nối. Đặt biến môi trường trong phiên terminal hoặc User Secrets của máy.
- Nếu một lần chạy bị dừng giữa chừng, database `projectmgmt_test_*` có thể còn lại; xóa thủ công bằng `DROP DATABASE`.

## Viết test mới

- Unit test: dựng service với `InMemoryIssueRepository` và các fake; mỗi lần gọi `SharedTestData.CreateIssues()` trả về bản sao mới.
- Integration test: gắn `[Collection(MySqlCollection.Name)]`, nhận `MySqlTestDatabase` qua constructor và đánh dấu test bằng `[MySqlFact]`. Dữ liệu seed dùng chung giữa các test trong collection, nên test ghi dữ liệu phải dùng Id/số Issue mới hoặc chỉ kiểm lỗi ràng buộc.
