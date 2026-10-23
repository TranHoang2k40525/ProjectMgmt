# Thiết kế hoàn thiện Module 1 — Tài khoản, bảo mật và trải nghiệm người dùng

Ngày: 2026-09-29  
Chủ trì: Trần Văn Hoàng  
Trạng thái: Đã được người dùng phê duyệt  
Phạm vi: Backend `IdentityExperience`, các hợp đồng tối thiểu với `Planning` và `DeliveryIntelligence`, API và kiểm thử liên quan

## 1. Mục tiêu

Hoàn thiện Module 1 thành một lát cắt nghiệp vụ dùng được từ đầu đến cuối:

- Đăng ký, xác minh email bằng OTP, đăng nhập, làm mới token và đăng xuất.
- Quên mật khẩu, đặt lại mật khẩu và đổi mật khẩu an toàn.
- Hồ sơ cá nhân, ảnh đại diện và kỹ năng.
- Vai trò, quyền hạn và thành viên theo phạm vi hệ thống/tổ chức/dự án.
- Thông báo lưu trong cơ sở dữ liệu, đẩy thời gian thực và gửi email cho các sự kiện quan trọng.
- AI 1 phân rã yêu cầu: giả lập riêng kết quả suy luận, nhưng lưu lịch sử thật và khi áp dụng phải tạo Issue thật.
- Người tạo tổ chức/dự án được lấy từ JWT và được cấp đúng vai trò trong cùng transaction nghiệp vụ.

Thiết kế tuân theo Modular Monolith kết hợp Clean Architecture. Mỗi module giữ quyền sở hữu bảng và nghiệp vụ của mình; giao tiếp liên module qua contract/service, không truy cập DbContext của module khác từ repository.

## 2. Nguồn sự thật và nguyên tắc xử lý tài liệu cũ

Thứ tự ưu tiên khi có mâu thuẫn:

1. Mã nguồn, migration, test và cấu hình đang chạy.
2. Quyết định mới nhất đã được người dùng xác nhận.
3. Hai thiết kế xác thực ngày 2026-09-28 và 2026-09-29.
4. `docs/cam-nang-du-an/Cam-nang-ky-thuat.md`.
5. Đặc tả API HTML, sau khi sửa các điểm không an toàn hoặc không rõ phạm vi.
6. Bảng phân công nhóm 7 dùng để xác định người/phần việc, không dùng trạng thái Jira cũ làm bằng chứng hoàn thành.
7. Các kế hoạch và cẩm nang cũ chỉ là dữ liệu lịch sử.

Các quyết định hiệu chỉnh bắt buộc:

- External login chưa có ClientId/ClientSecret phải trả `503`, tuyệt đối không giả lập đăng nhập thành công.
- API thành viên dự án dùng `projectId`, không dùng `projectKey` làm khóa phân quyền.
- Ảnh đại diện nằm tại `ProjectMgmt.Solution/Assets/avatars`.
- AI chỉ giả lập inference; dữ liệu log, suggestion, feedback và Issue được lưu thật.
- Không nhận `OwnerId`, `CreatedByUserId` hoặc `LeadUserId` đáng tin từ request; lấy người thao tác từ JWT.

## 3. Quy tắc code đã thống nhất

- Dùng `class`, không dùng `record` hoặc `sealed` cho phần mới.
- DTO dùng chung theo năng lực, thuộc tính nullable; không tách Request/Response máy móc.
- Request có ít giá trị có thể nhận tham số/DTO nhỏ phù hợp, không tạo lớp chỉ để bọc một trường.
- Không đưa `CancellationToken` vào luồng mới của Module 1.
- Chỉ giữ attribute có hành vi runtime cần thiết: route, HTTP method, binding, rate limit và ngoại lệ anonymous tại controller xác thực.
- Không dùng `ProducesResponseType`, `AddProblemDetails` hoặc `UseExceptionHandler` để che logic.
- Controller ánh xạ kết quả sang HTTP status bằng code dễ đọc và có `try/catch`.
- Serilog ghi file vào `ProjectMgmt.Solution/Logs`, không ghi password, OTP, token hoặc secret.
- Mọi thay đổi schema phải có migration và cập nhật model snapshot.

## 4. Ranh giới transaction

### 4.1 Quên và đặt lại mật khẩu

`POST /api/v1/auth/forgot-password` luôn trả kết quả chung để không lộ email có tồn tại hay không. Nếu tài khoản hợp lệ, hệ thống tạo OTP mục đích `ResetPassword` trong transaction; email được gửi sau khi commit.

`POST /api/v1/auth/reset-password` thực hiện nguyên tử:

1. Khóa người dùng và OTP reset mới nhất.
2. Kiểm tra hash OTP, hạn dùng và số lần thử.
3. Cập nhật `PasswordHash` và `SecurityStamp`.
4. Đánh dấu OTP đã dùng.
5. Thu hồi toàn bộ refresh token của người dùng.
6. Commit một lần.

`PUT /api/v1/users/me/password` kiểm tra mật khẩu hiện tại rồi cập nhật mật khẩu, `SecurityStamp` và thu hồi các refresh token trong một transaction tương tự.

JWT phải so sánh claim `security_stamp` với dữ liệu hiện tại để access token cũ mất hiệu lực ngay sau khi đổi/reset mật khẩu.

### 4.2 Tạo tổ chức và dự án

Người dùng mới không được tự nhiên có quyền hệ thống. Điểm khởi tạo quyền hợp lệ là:

- Khi tạo tổ chức: Planning lưu tổ chức với Owner lấy từ JWT; IdentityExperience cấp `OrgOwner` cho cùng người dùng.
- Khi tạo dự án: Planning lưu `CreatedByUserId` và `LeadUserId` từ JWT, tạo cấu hình mặc định; IdentityExperience thêm thành viên đầu tiên và cấp `ProjectManager`.

Các bước của mỗi use case dùng cùng một `DbConnection` và `DbTransaction` cho các DbContext liên quan. Một application orchestration service điều phối qua contract; repository không gọi chéo module. Nếu bất kỳ bước nào lỗi, toàn bộ thay đổi rollback.

### 4.3 Thông báo

Thông báo được ghi DB trước. Sau commit mới đẩy SignalR và, với sự kiện được chọn, gửi email. Lỗi SignalR/SMTP không rollback nghiệp vụ chính; lỗi được log với mã sự kiện và người nhận.

Email được gửi cho: OTP/reset password, mật khẩu đã đổi, lời mời dự án, đổi/thu hồi vai trò dự án và cảnh báo bảo mật.

### 4.4 AI 1 giả lập

`generate` tạo `AiGenerationLog` và `AiSuggestion` thật, sau đó bộ fake inference sinh danh sách gợi ý có cấu trúc và đánh dấu hoàn tất. `apply` kiểm tra quyền, trạng thái và idempotency, rồi tạo Issue thật qua contract của DeliveryIntelligence trong transaction dùng chung. Gọi apply lại không được tạo Issue trùng.

## 5. API và quyền

### 5.1 Ma trận endpoint

| Nhóm | Endpoint | Bảo vệ/quyền |
|---|---|---|
| Auth | `register`, `login`, `refresh-token`, `logout`, `forgot-password`, `reset-password`, `otp/send`, `otp/verify` | Anonymous có rate limit phù hợp |
| External auth | `POST /api/v1/auth/external-login` | Trả `503 AUTH_EXTERNAL_LOGIN_NOT_CONFIGURED` khi chưa có cấu hình thật |
| Profile | `GET /api/v1/users/me`, `PUT /api/v1/users/me/profile`, `POST /api/v1/users/me/avatar`, `PUT /api/v1/users/me/password` | Người dùng đã đăng nhập, chỉ dữ liệu của chính mình |
| Gợi ý người dùng | `GET /api/v1/users/for-you` | Đã đăng nhập; dữ liệu qua read contract, không đọc chéo DbContext |
| Skills | `GET /api/v1/skills` | Đã đăng nhập |
| Skills | `POST /api/v1/skills` | `skill.catalog.manage` |
| Skills | `GET /api/v1/users/{userId}/skills` | Chính mình hoặc có quan hệ dự án hợp lệ |
| Skills | `PUT /api/v1/users/me/skills` | Chính mình |
| Skills | `PUT /api/v1/users/{userId}/skills/{skillId}/verify` | `skill.verify` trong phạm vi phù hợp |
| Vai trò | `GET /api/v1/roles`, `GET /api/v1/permissions` | Đã đăng nhập; kết quả lọc theo phạm vi có thể quản lý |
| Vai trò | `POST /api/v1/roles`, `PUT /api/v1/roles/{roleId}/permissions` | `identity.role.manage` |
| Gán vai trò | `GET/POST /api/v1/users/{userId}/roles`, `DELETE /api/v1/users/{userId}/roles/{userRoleId}` | Đọc: `member.read`; sửa: `member.role.assign` |
| Thành viên dự án | `GET/POST /api/v1/projects/{projectId}/members` | `member.read` / `member.invite` |
| Thành viên dự án | `PUT /api/v1/projects/{projectId}/members/{userId}/role` | `member.role.assign` |
| Thành viên dự án | `DELETE /api/v1/projects/{projectId}/members/{userId}` | `member.remove` |
| Thông báo | `GET /api/v1/notifications`, `GET unread-count`, `PUT {id}/read`, `PUT read-all` | Chỉ thông báo của người đang đăng nhập |
| AI 1 | `POST /api/v1/ai/breakdown/generate` | `ai.breakdown.request` |
| AI 1 | `GET /api/v1/ai/breakdown/{generationId}`, `POST feedback` | Chủ yêu cầu hoặc thành viên có quyền trong dự án |
| AI 1 | `POST /api/v1/ai/breakdown/{generationId}/apply` | `ai.breakdown.apply` |
| Prompt | `GET/POST /api/v1/ai/prompt-templates`, `PUT {id}/activate` | Đọc theo phạm vi; sửa: `ai.prompt.manage` |

Fallback policy yêu cầu authenticated cho toàn API. Chỉ `AccountController` được đánh dấu anonymous ở cấp controller; từng action vẫn chịu rate limit cần thiết.

### 5.2 Bổ sung danh mục quyền

Giữ nguyên 22 permission hiện có và bổ sung đúng nghĩa, không tái sử dụng một quyền sai mục đích:

- `project.read`
- `member.read`
- `member.remove`
- `identity.role.manage`
- `skill.catalog.manage`
- `skill.verify`
- `ai.prompt.manage`

`Admin` có toàn quyền. `OrgOwner` có quyền quản trị trong tổ chức. `ProjectManager` có toàn quyền vận hành dự án nhưng không có quyền hệ thống. Scrum Master, Product Owner, Developer và Viewer nhận tập quyền tối thiểu theo nhiệm vụ. Quyền được tính từ role đang hoạt động, đúng scope và chưa hết hạn.

Các bất biến RBAC:

- Không tự nâng quyền.
- Không gán role có scope cao hơn quyền của người thao tác.
- Không xóa/hạ vai trò Project Manager cuối cùng.
- Không thao tác thành viên của dự án khác chỉ bằng cách đổi route id.
- Các truy vấn và lệnh đều lấy actor từ JWT, không tin `UserId` do client gửi.

## 6. Cấu trúc triển khai

### IdentityExperience

- `Application/Dto`: `AccountDto`, `ProfileDto`, `SkillDto`, `RoleDto`, `NotificationDto`, `AiBreakdownDto` và các result class cần thiết.
- `Application/Services`: tách theo năng lực `AccountServices`, `ProfileServices`, `SkillServices`, `RoleServices`, `NotificationServices`, `AiBreakdownServices`.
- `Domain/IRepositories`: repository tách theo aggregate/năng lực, tránh biến `IIdentityRepository` thành lớp khổng lồ.
- `Infrastructure/Repositories`: hiện thực EF Core tương ứng.
- `Infrastructure/Email`: template email theo sự kiện, dùng chung cấu hình SMTP hiện có.

### API host

- `Controllers/AccountController.cs`
- `Controllers/UsersController.cs`
- `Controllers/SkillsController.cs`
- `Controllers/RolesController.cs`
- `Controllers/ProjectMembersController.cs`
- `Controllers/NotificationsController.cs`
- `Controllers/AiBreakdownController.cs`
- `Controllers/AiPromptTemplatesController.cs`
- `Hubs/NotificationHub.cs`
- `Security/CurrentUserContext.cs`
- `Assets/avatars/`

Không tạo thêm thư mục controller song song. Mỗi controller chỉ gọi application service và ánh xạ HTTP; nghiệp vụ nằm ở service/domain/repository.

### Contract liên module

- IdentityExperience công bố kiểm tra quyền, gán/thu hồi role, thành viên và phát thông báo.
- Planning công bố tra cứu tổ chức/dự án và orchestration tạo tổ chức/dự án.
- DeliveryIntelligence công bố read model gợi ý người dùng và lệnh tạo Issue tối thiểu cho AI apply.

## 7. Trạng thái HTTP và mã lỗi chính

- `202`: yêu cầu quên mật khẩu/OTP đã được tiếp nhận.
- `200`: xác minh, reset, đổi mật khẩu hoặc truy vấn thành công.
- `400`: dữ liệu/OTP/mật khẩu không hợp lệ.
- `401`: chưa đăng nhập, token/refresh token không hợp lệ.
- `403`: đã đăng nhập nhưng thiếu quyền hoặc tài khoản bị chặn.
- `404`: tài nguyên trong phạm vi được phép không tồn tại.
- `409`: xung đột duy nhất, vai trò/thành viên đã tồn tại, vi phạm bất biến quản trị.
- `410`: OTP hết hạn.
- `413` hoặc `415`: avatar vượt giới hạn/sai loại nội dung.
- `429`: quá số lần thử hoặc rate limit.
- `503`: SMTP/external provider chưa sẵn sàng theo trường hợp đã định nghĩa.

Mutation result dùng cấu trúc nhất quán `Success`, `Message`, `ErrorCode`; DTO nghiệp vụ thêm trường nullable cần thiết. Không trả stack trace hoặc chi tiết hạ tầng.

## 8. Kiểm thử bắt buộc

- Unit test validation, permission evaluation và fake AI mapper.
- Integration test transaction đăng ký/OTP, reset/đổi mật khẩu, refresh token reuse và `SecurityStamp`.
- Integration test chống account enumeration ở forgot password.
- Integration test RBAC theo scope, self-escalation và Project Manager cuối cùng.
- Integration test rollback khi tạo tổ chức/dự án hoặc AI apply lỗi giữa chừng.
- Integration test notification ownership và idempotency.
- Test upload avatar: loại file, kích thước, tên file ngẫu nhiên, path traversal.
- Contract/API test các status và error code chính.
- Build toàn solution; chạy toàn bộ test; khởi động host và kiểm tra health/Swagger/log.

## 9. Thứ tự commit

1. `docs(identity): chốt thiết kế Module 1`
2. `feat(auth): hoàn thiện quên đặt lại và đổi mật khẩu`
3. `feat(identity): hoàn thiện hồ sơ avatar và kỹ năng`
4. `feat(rbac): hoàn thiện vai trò quyền và thành viên dự án`
5. `feat(project): ràng buộc người tạo tổ chức và dự án từ JWT`
6. `feat(notification): thêm thông báo realtime và email sự kiện`
7. `feat(ai): thêm AI breakdown giả lập và apply issue thật`
8. `test(identity): hoàn thiện kiểm thử Module 1`
9. `docs(api): đồng bộ đặc tả và hướng dẫn tích hợp frontend`

Mỗi commit chỉ stage các file thuộc lát cắt tương ứng. Các thay đổi đang có của người dùng không liên quan không được gom vào commit.

## 10. Điều kiện hoàn tất

Module 1 được coi là hoàn tất khi:

- Người dùng đi trọn các luồng tài khoản mà không cần sửa DB thủ công.
- Token cũ bị vô hiệu đúng lúc sau thay đổi bảo mật.
- Quyền được áp dụng theo scope, không thể vượt quyền bằng request.
- Chủ sở hữu tổ chức/dự án và Project Manager đầu tiên được tạo nguyên tử từ JWT.
- Notification có lịch sử DB, realtime và email đúng tập sự kiện.
- AI fake có lịch sử thật, feedback thật và apply ra Issue thật không trùng.
- Migration chạy được trên DB sạch và DB hiện có.
- Toàn bộ test/build/health check đạt; log không lộ secret.
- Đặc tả API và tài liệu tích hợp frontend phản ánh đúng code cuối cùng.
