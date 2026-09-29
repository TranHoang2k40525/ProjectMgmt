# Hướng dẫn kỹ thuật xác thực Backend

Tài liệu này là nguồn hướng dẫn ổn định cho quy trình đăng ký, xác minh email bằng OTP, đăng nhập, làm mới token và đăng xuất của **Hệ thống Quản lý Dự án Scrum tích hợp AI**. Người tiếp nhận dự án có thể dùng tài liệu mà không cần biết lịch sử phát triển hay đọc lại hội thoại.

## 1. Phạm vi và nguyên tắc

Module `IdentityExperience` sở hữu các bảng `User`, `UserProfile`, `OtpCode`, `RefreshToken`, `Role` và `UserRole`.

- Không lưu mật khẩu, OTP hoặc refresh token dạng rõ.
- Mật khẩu dùng BCrypt work factor 12.
- OTP gồm 6 chữ số, sinh bằng bộ tạo số ngẫu nhiên mật mã và lưu HMAC-SHA256.
- Access token là JWT HMAC-SHA256, thời hạn mặc định 15 phút.
- Refresh token là 64 byte ngẫu nhiên, chỉ SHA-256 hash được lưu trong DB, thời hạn mặc định 30 ngày.
- Chỉ role phạm vi `System` được đưa vào claim JWT. Role theo tổ chức/dự án phải được kiểm tra cùng `ScopeId`, không được coi như quyền toàn hệ thống.
- `IsActive` biểu thị tài khoản có bị quản trị viên vô hiệu hóa hay không. `IsEmailVerified` biểu thị đã hoàn tất xác minh đăng ký.

## 2. Ranh giới transaction

Đăng ký không thể là một transaction ACID duy nhất bao trùm cả MySQL và SMTP. Quy trình chuẩn dùng hai transaction dữ liệu và một bước gửi email ở giữa:

1. Transaction A khóa bất biến dữ liệu và ghi cùng lúc `User` + `UserProfile` + `OtpCode`. Nếu một lệnh ghi lỗi, cả ba đều rollback.
2. Sau khi commit, hệ thống gửi OTP qua SMTP. Nếu SMTP lỗi, tài khoản vẫn ở `PendingVerification`; người dùng phục hồi bằng API gửi lại OTP.
3. Transaction B khóa hàng `User`, kiểm tra OTP, đánh dấu OTP đã dùng và đổi `User.IsEmailVerified = true` trong cùng một commit.

Cách chia này tránh trạng thái “đã gửi email nhưng DB rollback” và cũng không giả vờ rằng SMTP có thể tham gia transaction MySQL.

Các request gửi lại/xác minh OTP đều khóa hàng `User` bằng `SELECT ... FOR UPDATE`. Vì vậy hai request đồng thời không thể cùng cấp hoặc cùng tiêu thụ một OTP.

## 3. API contract

Base path: `/api/v1/auth`. Tất cả body dùng JSON. DTO dùng chung; mỗi endpoint chỉ đọc các trường được liệt kê.

Quy ước triển khai của module xác thực:

- Controller nhận một `AccountDto` dùng chung để model binding, sau đó truyền các giá trị đơn như `email`, `password`, `code` vào service. Không truyền DTO xuyên qua mọi tầng.
- Các trường không dùng ở một endpoint được để `null`; không tạo thêm lớp Request/Response chỉ để chứa một hoặc hai giá trị.
- Controller chỉ giữ attribute cần thiết cho route, HTTP method, body và rate limit. Mã trạng thái HTTP được quyết định trực tiếp trong thân hàm, không khai báo dày đặc bằng `ProducesResponseType`.
- Mỗi action có `try/catch`, ghi exception nội bộ vào log và chỉ trả mã `AUTH_INTERNAL_ERROR` chung cho client. Không trả stack trace hoặc nội dung secret.
- Luồng auth không truyền `CancellationToken`; service và repository dùng chữ ký tham số thông thường để mã dễ theo dõi.

| Endpoint | Body cần dùng | Thành công | Ý nghĩa |
|---|---|---:|---|
| `POST /register` | `email`, `password`, `fullName`, `phoneNumber` | `202` | Tạo tài khoản pending và gửi OTP |
| `POST /otp/send` | `email`, `purpose` | `202` | Cấp và gửi OTP mới; `purpose` là `VerifyEmail` |
| `POST /otp/verify` | `email`, `code`, `purpose` | `200` | Tiêu thụ OTP và kích hoạt email |
| `POST /login` | `email`, `password` | `200` | Cấp access token và refresh token |
| `POST /refresh-token` | `refreshToken` | `200` | Rotation: thu hồi token cũ, cấp cặp token mới |
| `POST /logout` | `refreshToken` | `200` | Thu hồi refresh token; xử lý idempotent |

Ví dụ đăng ký:

```json
{
  "email": "member@example.com",
  "password": "StrongPassword@123",
  "fullName": "Nguyễn Văn Thành Viên",
  "phoneNumber": "0901234567"
}
```

Ví dụ xác minh:

```json
{
  "email": "member@example.com",
  "code": "123456",
  "purpose": "VerifyEmail"
}
```

Mọi response đều có `success`, `message`, `errorCode`. Response đăng nhập còn có `accessToken`, `refreshToken`, thời điểm hết hạn, thông tin người dùng và system roles.

Các mã lỗi chính:

| Mã | Xử lý phía client |
|---|---|
| `AUTH_EMAIL_VERIFICATION_PENDING` | Chuyển sang màn hình OTP; cho phép gửi lại |
| `AUTH_EMAIL_DELIVERY_FAILED` | Giữ màn hình OTP, báo lỗi gửi mail và chờ cooldown |
| `AUTH_OTP_RATE_LIMITED` | Dùng `Retry-After` hoặc `resendAfterSeconds` để đếm ngược |
| `AUTH_OTP_INVALID` | Hiển thị số lần còn lại |
| `AUTH_OTP_EXPIRED` | Yêu cầu OTP mới |
| `AUTH_OTP_ATTEMPTS_EXCEEDED` | OTP bị khóa; yêu cầu OTP mới |
| `AUTH_EMAIL_NOT_VERIFIED` | Không cho đăng nhập; chuyển sang xác minh |
| `AUTH_INVALID_CREDENTIALS` | Không phân biệt email sai hay mật khẩu sai |
| `AUTH_REFRESH_TOKEN_REUSE_DETECTED` | Xóa token phía client và buộc đăng nhập lại |

## 4. Cấu hình bí mật

Trên máy phát triển hiện tại, cấu hình SMTP tương thích đã được chuyển từ MovieTicket vào **.NET User Secrets** của `ProjectMgmt.Solution`; `Jwt:Issuer`, `Jwt:Audience`, một JWT signing key riêng cho ProjectMgmt và `Otp:HashKey` riêng cũng đã được thiết lập. Vì vậy ứng dụng không còn dừng với lỗi `Missing secure JWT configuration`. Các giá trị bí mật không nằm trong repository và không xuất hiện trong tài liệu này.

Khi chạy bằng IIS local, tiến trình `w3wp.exe` có identity riêng và không đọc được User Secrets của tài khoản Windows. Development vì vậy có thể nạp thêm `ProjectMgmt.Solution/local.settings.json`. File này được ignore khỏi Git và bị loại khỏi output/publish; chỉ dùng để chuyển cấu hình bí mật cho IIS trên máy phát triển. Không tạo file này trên production: production phải dùng environment variables hoặc secret store của hạ tầng.

Không dùng lại JWT signing key của MovieTicket: hai hệ thống dùng khóa riêng để token của ứng dụng này không thể được một ứng dụng khác tin cậy nhầm.

Không ghi secret thật vào `appsettings.json` hoặc Git. Khi chạy local, cấu hình bằng User Secrets tại thư mục repository:

```powershell
dotnet user-secrets set "ConnectionStrings:ProjectMgmt" "Server=localhost;Port=3306;Database=projectmgmt;User=projectmgmt_app;Password=YOUR_PASSWORD;SslMode=None" --project ProjectMgmt.Solution
dotnet user-secrets set "Jwt:Issuer" "ProjectMgmt.Api" --project ProjectMgmt.Solution
dotnet user-secrets set "Jwt:Audience" "ProjectMgmt.Web" --project ProjectMgmt.Solution
dotnet user-secrets set "Otp:HashKey" "A_RANDOM_SECRET_OF_AT_LEAST_32_CHARACTERS" --project ProjectMgmt.Solution
dotnet user-secrets set "Jwt:SigningKey" "ANOTHER_RANDOM_SECRET_OF_AT_LEAST_32_CHARACTERS" --project ProjectMgmt.Solution
dotnet user-secrets set "Email:SmtpHost" "smtp.gmail.com" --project ProjectMgmt.Solution
dotnet user-secrets set "Email:Port" "587" --project ProjectMgmt.Solution
dotnet user-secrets set "Email:User" "YOUR_SMTP_USERNAME" --project ProjectMgmt.Solution
dotnet user-secrets set "Email:Pass" "YOUR_SMTP_APP_PASSWORD" --project ProjectMgmt.Solution
dotnet user-secrets set "Email:From" "YOUR_SENDER_EMAIL" --project ProjectMgmt.Solution
```

Cấu hình SMTP dùng cùng quy ước với MovieTicket: `Email:SmtpHost`, `Email:Port`, `Email:User`, `Email:Pass`, `Email:From`. Mặc định Gmail SMTP là `smtp.gmail.com:587`, SSL bật. Với Gmail phải dùng App Password, không dùng mật khẩu tài khoản chính.

Development CORS cho phép các frontend local tại cổng `3000`, `4200` và `8080`. Khi deploy, phải giới hạn `Cors:AllowedOrigins` đúng domain thật, không dùng wildcard cùng credentials.

Khi chạy Docker, sao chép `.env.example` thành `.env`, thay toàn bộ placeholder và không commit `.env`.

## 5. Khởi tạo database và chạy

```powershell
dotnet restore ProjectMgmt.slnx
dotnet ef database update --project ProjectMgmt.Modules.IdentityExperience/Infrastructure/IdentityExperience.Infrastructure.csproj --startup-project ProjectMgmt.Solution/ProjectMgmt.Solution.csproj --context IdentityExperienceDbContext
dotnet run --project ProjectMgmt.Solution/ProjectMgmt.Solution.csproj
```

Migration `AddUniqueUserProfilePhone` tạo unique index cho số điện thoại. Nếu database cũ đã có số điện thoại trùng, phải làm sạch dữ liệu trùng trước khi chạy migration.

Swagger chạy ở `/swagger` trong môi trường Development hoặc Staging.

## 6. Quy tắc bảo mật và vận hành

- Đăng ký, gửi OTP và xác minh OTP bị rate-limit 5 request/phút/IP; đăng nhập 10 request/phút/IP.
- Mỗi OTP sống 5 phút, cooldown gửi lại 60 giây và tối đa 5 lần nhập sai.
- Cấp OTP mới làm OTP cũ mất hiệu lực.
- Xác minh email là idempotent: email đã xác minh trả về trạng thái `Active`.
- Refresh token được rotation sau mỗi lần dùng. Nếu token đã được thay thế bị dùng lại, hệ thống coi là dấu hiệu đánh cắp và thu hồi toàn bộ refresh token đang hoạt động của người dùng.
- Access token cũ có thể còn hiệu lực tối đa 15 phút sau logout; đây là đặc tính của JWT tự chứa. Nếu cần thu hồi tức thời, bổ sung security-stamp validation hoặc deny-list phân tán.
- Log không được chứa password, OTP, access token, refresh token hay toàn bộ địa chỉ email.

## 7. Log để kiểm tra và xử lý lỗi

- Serilog ghi đồng thời ra console và `ProjectMgmt.Solution/Logs/ProjectMgmt-yyyyMMdd.log`.
- File được tách theo ngày và giữ tối đa 14 file. Thư mục `Logs` đã được ignore khỏi Git.
- Request log chỉ ghi HTTP method, path, status code và thời gian xử lý.
- Exception ngoài dự kiến trong auth được controller ghi kèm tên endpoint; SMTP failure chỉ ghi domain của email.
- Tuyệt đối không ghi password, OTP, access token, refresh token, secret hoặc toàn bộ địa chỉ email.
- Khi kiểm tra lỗi, đọc file mới nhất bằng `Get-Content ProjectMgmt.Solution/Logs/ProjectMgmt-*.log -Tail 100` rồi đối chiếu timestamp, endpoint và status code.

Ứng dụng cố ý dùng `try/catch` tại controller theo quy ước của dự án, không dùng `AddProblemDetails` hoặc `UseExceptionHandler` để thay thế luồng xử lý này.

## 8. Kiểm thử

```powershell
dotnet build ProjectMgmt.slnx --no-restore
dotnet test ProjectMgmt.Tests/ProjectMgmt.Tests.csproj --no-build --no-restore
```

Bộ test xác thực kiểm tra BCrypt, HMAC OTP ràng buộc theo email/mục đích, JWT/refresh hash, aggregate đăng ký pending, trạng thái phục hồi khi SMTP lỗi, cooldown gửi lại OTP, kích hoạt sau OTP và chặn đăng nhập khi email chưa xác minh. Kiểm thử tích hợp MySQL nên dùng database tách biệt và không dùng tài khoản SMTP thật.

## 9. Vị trí mã nguồn

- API: `ProjectMgmt.Solution/Controller/AccountController.cs`
- Điều phối use case: `ProjectMgmt.Modules.IdentityExperience/Application/Services/AccountServices.cs`
- Repository contract: `ProjectMgmt.Modules.IdentityExperience/Domain/IRepositories/IIdentityRepository.cs`
- Transaction EF Core: `ProjectMgmt.Modules.IdentityExperience/Infrastructure/Repositories/IdentityRepository.cs`
- Password/OTP/JWT/SMTP: `ProjectMgmt.Modules.IdentityExperience/Infrastructure/Services/`
- Entity và mapping: `ProjectMgmt.Modules.IdentityExperience/Domain/Entities/` và `Infrastructure/Configurations/`
- Test: `ProjectMgmt.Tests/AuthenticationTests.cs`
