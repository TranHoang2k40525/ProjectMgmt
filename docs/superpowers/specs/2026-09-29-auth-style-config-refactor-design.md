# Thiết kế refactor phong cách code và cấu hình xác thực

Ngày: 2026-09-29  
Phạm vi: Backend `IdentityExperience` và API xác thực  
Trạng thái: Đã được người dùng xác nhận

## 1. Mục tiêu

Giữ nguyên các bất biến bảo mật và transaction của quy trình đăng ký/OTP/JWT, đồng thời làm mã nguồn gần với phong cách MovieTicket, ngắn hơn và dễ đọc hơn:

- Không dùng `CancellationToken` trong luồng auth.
- Service nhận các biến nghiệp vụ thông thường thay vì truyền nguyên DTO xuyên qua các tầng.
- DTO request vẫn là một `AccountDto` nullable dùng chung để ASP.NET bind JSON.
- Loại bỏ attribute chỉ phục vụ mô tả response.
- Dùng `try/catch` rõ ràng tại controller thay vì global exception handler.
- Ghi log ra file để kiểm tra vận hành.
- Chuyển cấu hình SMTP đang hoạt động từ MovieTicket sang ProjectMgmt mà người dùng không phải nhập lại.
- Khắc phục lỗi khởi động do thiếu JWT configuration.

## 2. Cấu hình được chuyển

Các giá trị SMTP không xung đột gồm host, port, username, App Password và địa chỉ From được đọc từ MovieTicket và ghi vào .NET User Secrets của `ProjectMgmt.Solution`. Không ghi secret thật vào Git hoặc log.

ProjectMgmt được tạo JWT signing key và OTP HMAC key riêng, cũng lưu bằng User Secrets. Không tái sử dụng JWT signing key của MovieTicket vì hai ứng dụng phải có ranh giới tin cậy độc lập.

Các cấu hình không được sao chép do xung đột:

- JWT issuer/audience của MovieTicket.
- JWT 60 phút của MovieTicket; ProjectMgmt giữ 15 phút.
- OTP 10 phút của MovieTicket; ProjectMgmt giữ 5 phút.
- Connection string SQL Server của MovieTicket; ProjectMgmt tiếp tục dùng MySQL.

CORS Development hợp nhất các origin hiện tại với `http://localhost:3000` và `http://localhost:8080`.

## 3. API và signature

Controller tiếp tục nhận JSON bằng `[FromBody] AccountDto`. Sau khi bind, controller gọi service bằng tham số rõ nghĩa, ví dụ:

```csharp
await _accountServices.LoginAsync(request.Email, request.Password, ipAddress, userAgent);
```

Interface service và repository không còn `CancellationToken`. Các lời gọi EF Core/SMTP dùng overload async chuẩn không truyền token.

Các attribute được giữ:

- `[ApiController]`
- `[Route]`
- `[HttpPost]`
- `[FromBody]`
- `[EnableRateLimiting]` vì đây là hành vi bảo mật runtime

Các attribute `[ProducesResponseType]` và `[AllowAnonymous]` dư thừa bị loại bỏ.

## 4. Xử lý lỗi và log

Không dùng `AddProblemDetails` hoặc `UseExceptionHandler`. Mỗi action auth có `try/catch`:

- Gọi service và ánh xạ kết quả sang HTTP status trong action.
- Log exception bằng structured logging.
- Trả response 500 thống nhất, không lộ stack trace hoặc thông tin hạ tầng.

Serilog ghi đồng thời console và file:

```text
ProjectMgmt.Solution/Logs/ProjectMgmt-.log
```

File xoay theo ngày và giữ tối đa 14 ngày. `Logs/` được ignore khỏi Git.

Log được phép chứa endpoint, status, error code, UserId và tên miền email. Log không được chứa password, OTP, access token, refresh token, JWT signing key, OTP hash key, SMTP App Password hoặc toàn bộ email nếu không cần thiết.

## 5. Phần giữ nguyên về bảo mật

Không sao chép các kỹ thuật yếu hơn trong MovieTicket:

- Không dùng `Random` cho OTP; tiếp tục dùng `RandomNumberGenerator`.
- Không lưu refresh token rõ; tiếp tục lưu SHA-256 hash.
- Không dùng BCrypt cho OTP; tiếp tục dùng HMAC-SHA256 gắn với email và purpose.
- Không bỏ transaction hoặc row lock.
- Không log nội suy exception/message chứa dữ liệu request.

Transaction đăng ký vẫn gồm:

1. Transaction A lưu `User + UserProfile + OtpCode`.
2. SMTP chạy sau commit.
3. Transaction B tiêu thụ OTP và kích hoạt email nguyên tử.

## 6. Kiểm thử và commit

Các test auth được cập nhật theo signature mới. Điều kiện hoàn tất:

- Solution build không lỗi.
- Toàn bộ test pass.
- API khởi động không còn lỗi thiếu JWT.
- `/health/live` và Swagger trả HTTP 200.
- Swagger chứa đủ sáu endpoint auth.
- File log được tạo khi ứng dụng chạy nhưng không bị Git theo dõi.

Thay đổi được chia commit:

1. Cấu hình, User Secrets và file logging.
2. Refactor controller/service/repository, bỏ `CancellationToken` và attribute dư.
3. Cập nhật test và tài liệu.
