# Thiết kế đăng ký, OTP và đăng nhập

Ngày: 2026-09-28

Phạm vi: backend `IdentityExperience` và hợp đồng HTTP của `ProjectMgmt.Solution`

Trạng thái: Đã được người dùng xác nhận trong hội thoại

## 1. Mục tiêu

Hoàn thiện hành trình:

```text
Nhập thông tin đăng ký
→ tạo tài khoản chờ xác minh
→ gửi OTP qua email
→ nhập và xác minh OTP
→ kích hoạt tài khoản
→ đăng nhập và nhận access/refresh token
```

Mọi thay đổi dữ liệu trong từng HTTP request phải nguyên tử. Không giữ database transaction trong thời gian người dùng chờ và nhập OTP.

## 2. Ràng buộc mã nguồn

- Dùng class linh hoạt; không dùng `record` hoặc `sealed`.
- Dùng một `AccountDto` chung cho dữ liệu đầu vào xác thực. Các thuộc tính được phép nullable và use case tự kiểm tra các trường cần thiết.
- Không tạo cặp DTO Request/Response cho từng endpoint.
- Kết quả trả về dùng các class chuyên cho output như `RegisterResult`, `OtpResult`, `ResultLogin`.
- Trường bí mật như password và OTP phải nằm trong JSON body, không truyền trên query string.
- Giữ kiến trúc Application phụ thuộc abstraction; EF Core, SMTP, HMAC và JWT được triển khai tại Infrastructure/Host.

## 3. Hợp đồng API

Base route: `/api/v1/auth`.

### 3.1 `POST /register`

Nhận `AccountDto` với `Email`, `Password`, `FullName`, `PhoneNumber`.

Thành công trả `202 Accepted`:

```json
{
  "success": true,
  "userId": "uuid",
  "email": "user@example.com",
  "status": "PendingVerification",
  "otpExpiresAt": "2026-09-28T10:05:00Z",
  "resendAfterSeconds": 60
}
```

### 3.2 `POST /otp/send`

Nhận `AccountDto` với `Email`, `Purpose`. Endpoint phát hành OTP mới, vô hiệu OTP cũ chưa dùng và gửi email lại. Chỉ chấp nhận purpose hợp lệ.

### 3.3 `POST /otp/verify`

Nhận `AccountDto` với `Email`, `OtpCode`, `Purpose`.

Với `VerifyEmail`, thành công phải đồng thời:

- đánh dấu OTP đã sử dụng;
- đặt `User.IsEmailVerified = true`;
- không tự động cấp quyền Project.

### 3.4 `POST /login`

Nhận `AccountDto` với `Email`, `Password`. Chỉ cấp token khi tài khoản vừa active vừa xác minh email.

### 3.5 `POST /refresh-token` và `POST /logout`

Refresh token dùng rotation. Database chỉ lưu hash; token cũ bị thu hồi và liên kết tới token thay thế. Logout thu hồi token hiện tại.

## 4. Trạng thái nghiệp vụ

```text
Absent
  │ register transaction
  ▼
PendingVerification
  │ verify OTP transaction
  ▼
Verified
  │ admin disable
  ▼
Disabled
```

- `IsEmailVerified=false` biểu diễn tài khoản đang chờ OTP.
- `IsActive=false` chỉ dùng cho soft-disable bởi quản trị, không dùng thay cho pending verification.
- Login yêu cầu `IsActive && IsEmailVerified`.
- Tài khoản pending có thể yêu cầu gửi lại OTP.

## 5. Transaction A — tạo đăng ký chờ xác minh

Trong một unit of work:

1. Chuẩn hóa email và số điện thoại.
2. Kiểm tra `NormalizedEmail`.
3. Tạo `User` với password hash và `IsEmailVerified=false`.
4. Tạo `UserProfile` dùng cùng `UserId`.
5. Sinh OTP bằng `RandomNumberGenerator`.
6. Chỉ lưu hash OTP, purpose `VerifyEmail`, expiry năm phút và attempt bằng 0.
7. Gọi một `SaveChangesAsync` để EF Core bao toàn bộ insert trong transaction.

Unique index `UQ_User_NormalizedEmail` là hàng rào chống hai request đăng ký đồng thời. Kiểm tra trước insert chỉ phục vụ thông báo dễ hiểu; duplicate-key vẫn phải được chuyển thành lỗi xung đột.

Không gửi SMTP bên trong database transaction. Sau commit, service gửi email. Nếu SMTP lỗi, account giữ trạng thái pending và client có thể gọi resend. Đây là lựa chọn thực dụng không thêm bảng outbox vào schema 55 bảng; nâng cấp sang transactional outbox là bước riêng nếu cần bảo đảm phát email qua crash tuyệt đối.

## 6. Transaction B — xác minh OTP

Trong transaction riêng:

1. Tìm user theo `NormalizedEmail`.
2. Khóa OTP mới nhất chưa dùng theo `UserId + Purpose`.
3. Kiểm tra expiry, attempt limit và trạng thái sử dụng.
4. So sánh hash bằng constant-time comparison.
5. Nếu sai, tăng `AttemptCount`; đến giới hạn thì vô hiệu OTP.
6. Nếu đúng, đặt `OtpCode.IsUsed=true` và `User.IsEmailVerified=true`.
7. Gọi `SaveChangesAsync` và commit.

Vì DbContext bật retry strategy, transaction tường minh phải chạy qua `Database.CreateExecutionStrategy()`.

Hai request xác minh đồng thời chỉ một request được thành công. Có thể dùng MySQL `SELECT ... FOR UPDATE` trong repository để khóa bản ghi OTP.

## 7. OTP và bảo mật

- OTP gồm sáu chữ số, sinh bằng `RandomNumberGenerator.GetInt32`.
- Không lưu hoặc log mã rõ.
- Hash dùng HMAC-SHA256 với secret `Otp:HashKey` nằm trong User Secrets/biến môi trường.
- Payload hash bao gồm email chuẩn hóa, purpose và code.
- Hiệu lực năm phút, tối đa năm lần thử.
- Resend tối thiểu 60 giây và vô hiệu OTP cũ.
- Rate limit register/login/send/verify theo IP và định danh đã chuẩn hóa.
- Error login không phân biệt “không tồn tại” và “sai mật khẩu”.

## 8. Email SMTP được tái sử dụng

Tái sử dụng ý tưởng từ MovieTicket:

- abstraction `IEmailService`;
- implementation dựa trên `System.Net.Mail.SmtpClient`;
- các khóa `Email:SmtpHost`, `Email:SmtpPort`, `Email:SmtpUser`, `Email:SmtpPass`, `Email:From`;
- SSL, timeout và log không chứa secret hoặc OTP.

Không sao chép Gmail App Password từ repository MovieTicket. `appsettings.json` chỉ có host, port và giá trị trống/placeholder. Credential được truyền bằng:

```text
Email__SmtpUser
Email__SmtpPass
Email__From
```

hoặc .NET User Secrets. Credential đang tồn tại dạng rõ trong project nguồn phải được thu hồi và cấp lại.

## 9. Login, JWT và refresh token

- Password hash dùng BCrypt.
- Access token JWT ngắn hạn, mặc định 15 phút.
- Signing key lấy từ `Jwt:Key`, không commit vào Git.
- Refresh token là chuỗi ngẫu nhiên mật mã; DB chỉ lưu SHA-256 hash.
- Rotation tạo token mới, thu hồi token cũ và điền `ReplacedByTokenId`.
- Reuse token đã thu hồi làm vô hiệu chuỗi token của user theo chính sách.
- Login cập nhật `LastLoginAt` sau khi xác thực thành công.
- Host cấu hình `AddAuthentication().AddJwtBearer()` và gọi `UseAuthentication()` trước `UseAuthorization()`.

## 10. Validation và dữ liệu

- `AccountDto` chứa các thuộc tính nullable: `Email`, `Password`, `FullName`, `PhoneNumber`, `OtpCode`, `Purpose`, `RefreshToken`.
- Use case xác thực các trường đúng theo endpoint.
- Email trim và chuẩn hóa bằng uppercase invariant cho lookup.
- Phone được parse bằng `PhoneNumberUtil.GetInstance()` và lưu dạng E.164.
- Không yêu cầu `DisplayName` duy nhất.
- Nếu phone là định danh duy nhất, cần migration thêm unique index sau khi làm rõ chính sách; bản này chỉ chuẩn hóa và kiểm tra theo hành vi hiện có.
- Không nhận `IsEmailVerified` từ client.
- Password policy thống nhất tối thiểu tám ký tự, chữ hoa, chữ thường, số và ký tự đặc biệt.

## 11. Xử lý lỗi

Controller chuyển kết quả use case thành HTTP status:

| Trường hợp | Status |
|---|---:|
| Đăng ký pending | 202 |
| Xác minh/login thành công | 200 |
| Input không hợp lệ | 400 |
| Sai credentials | 401 |
| Chưa xác minh hoặc bị khóa | 403 |
| Email đã tồn tại | 409 |
| OTP hết hạn | 410 |
| Vượt rate limit | 429 |
| SMTP tạm lỗi | 503 |

Response có `Success`, `Message`, `ErrorCode`; không dùng các thuộc tính sai chính tả như `mess`, `sucsess`, `sucsecc`.

## 12. Kiểm thử

Tối thiểu phải có:

- register tạo đồng thời User/Profile/OTP;
- lỗi insert bất kỳ rollback toàn bộ;
- hai register cùng email chỉ có một user;
- account pending không login được;
- OTP sai tăng attempt;
- OTP hết hạn, sai purpose và replay bị từ chối;
- resend vô hiệu mã cũ;
- hai verify đồng thời chỉ một request thành công;
- verify đúng cập nhật OTP và User cùng transaction;
- login verified trả access/refresh token;
- refresh rotation và logout thu hồi token;
- SMTP lỗi không làm account trở thành verified.

Integration test dùng MySQL thật hoặc container tương thích, không dùng EF InMemory để kết luận về transaction và unique constraint.

## 13. Chuỗi commit

1. `[0/5] docs(auth): chốt thiết kế đăng ký OTP và đăng nhập`
2. `[1/5] feat(auth): tạo đăng ký pending trong một transaction`
3. `[2/5] feat(auth): tích hợp SMTP và gửi lại OTP`
4. `[3/5] feat(auth): xác minh OTP và kích hoạt tài khoản`
5. `[4/5] feat(auth): hoàn thiện JWT login refresh và logout`
6. `[5/5] test(auth): kiểm thử luồng xác thực đầu cuối`

Mỗi commit chỉ stage các file thuộc phần đó, không đưa các thay đổi giao diện hoặc module khác vào commit.
