# API Module 1 — Tài khoản, bảo mật và trải nghiệm người dùng

> Tài liệu tích hợp chính thức cho backend **Hệ thống Quản lý Dự án Scrum tích hợp AI**.  
> Cập nhật theo mã nguồn ngày 30/09/2026. Nếu tài liệu cũ mâu thuẫn với trang này, dùng trang này và Swagger của bản build hiện tại.

## 1. Bắt đầu nhanh

- Base URL phát triển: `http://localhost:<backend-port>`.
- API prefix: `/api/v1`.
- API có bảo vệ nhận `Authorization: Bearer <accessToken>`.
- Access token hết hạn sau 15 phút theo cấu hình mặc định; refresh token dùng để lấy cặp token mới.
- JSON dùng `camelCase`; thời gian trả về theo UTC/ISO 8601.
- Health probe công khai: `GET /health` và `GET /health/live`.
- Swagger chỉ bật trong `Development` hoặc `Staging`.

Các cấu hình bắt buộc phải đến từ User Secrets hoặc biến môi trường, không commit secret:

```text
ConnectionStrings__ProjectMgmt
Jwt__Issuer
Jwt__Audience
Jwt__SigningKey        # ít nhất 32 byte UTF-8
Otp__HashKey           # khóa hash OTP riêng
Email__SmtpHost
Email__Port
Email__User
Email__Pass
Email__From
```

## 2. Cấu trúc kết quả và xử lý lỗi

Mutation trả về cấu trúc chung và có thể bổ sung trường nghiệp vụ:

```json
{
  "success": false,
  "message": "Thông báo có thể hiển thị cho người dùng",
  "errorCode": "AUTH_OTP_INVALID"
}
```

Frontend phải rẽ nhánh theo cả HTTP status và `errorCode`. Không suy luận lỗi bằng chuỗi `message`.

| HTTP | Ý nghĩa |
|---|---|
| `200` | Truy vấn/cập nhật thành công |
| `201` | Tạo tài nguyên hoặc generation thành công |
| `202` | Đã tiếp nhận đăng ký, gửi OTP hoặc quên mật khẩu |
| `400` | Request không hợp lệ |
| `401` | Thiếu/sai JWT, đăng nhập hoặc refresh token thất bại |
| `403` | Đã xác thực nhưng thiếu quyền/tài khoản chưa đủ điều kiện |
| `404` | Không tìm thấy tài nguyên trong phạm vi được phép |
| `409` | Trùng dữ liệu hoặc vi phạm bất biến nghiệp vụ |
| `410` | OTP hết hạn |
| `413` | Avatar quá lớn |
| `415` | Avatar sai loại dữ liệu |
| `429` | Rate limit hoặc OTP vượt số lần thử |
| `503` | SMTP/external provider chưa sẵn sàng |

## 3. Luồng tài khoản hoàn chỉnh

### 3.1 Đăng ký và xác minh email

1. `POST /api/v1/auth/register`
2. Backend tạo `User`, `UserProfile`, OTP `VerifyEmail` trong một transaction.
3. Sau commit, backend gửi OTP qua email.
4. Người dùng nhập mã tại `POST /api/v1/auth/otp/verify`.
5. Thành công mới được đăng nhập.

Request đăng ký:

```json
{
  "email": "member@example.com",
  "password": "StrongPassword@123",
  "fullName": "Nguyễn Văn A",
  "phoneNumber": "+84901234567"
}
```

Response `202`:

```json
{
  "success": true,
  "message": "Đăng ký thành công. Vui lòng kiểm tra email để nhập OTP.",
  "userId": "uuid",
  "email": "member@example.com",
  "status": "PendingVerification",
  "otpExpiresAt": "2026-09-30T05:05:00Z",
  "resendAfterSeconds": 60
}
```

Gửi lại OTP, chỉ hỗ trợ purpose `VerifyEmail`:

```http
POST /api/v1/auth/otp/send
Content-Type: application/json

{
  "email": "member@example.com",
  "purpose": "VerifyEmail"
}
```

Xác minh OTP:

```http
POST /api/v1/auth/otp/verify
Content-Type: application/json

{
  "email": "member@example.com",
  "code": "123456",
  "purpose": "VerifyEmail"
}
```

OTP sống 5 phút, gửi lại tối thiểu sau 60 giây và bị khóa sau 5 lần nhập sai. Backend không log mã OTP.

### 3.2 Đăng nhập, refresh và logout

```http
POST /api/v1/auth/login

{
  "email": "member@example.com",
  "password": "StrongPassword@123"
}
```

Kết quả gồm `accessToken`, `refreshToken`, hạn token, thông tin người dùng và system roles. Frontend phải lưu cặp token theo chính sách bảo mật của ứng dụng; không đưa token vào log.

```http
POST /api/v1/auth/refresh-token

{
  "refreshToken": "token-hien-tai"
}
```

Refresh token được xoay vòng. Nếu token cũ đã xoay bị tái sử dụng, backend thu hồi các phiên liên quan và trả `AUTH_REFRESH_TOKEN_REUSE_DETECTED`.

```http
POST /api/v1/auth/logout

{
  "refreshToken": "token-can-thu-hoi"
}
```

### 3.3 Quên, đặt lại và đổi mật khẩu

`POST /api/v1/auth/forgot-password` luôn trả `202` với nội dung chung, kể cả email không tồn tại, để chống dò tài khoản.

```json
{ "email": "member@example.com" }
```

Đặt lại bằng OTP:

```http
POST /api/v1/auth/reset-password

{
  "email": "member@example.com",
  "code": "123456",
  "newPassword": "NewStrongPassword@123"
}
```

Đổi khi đã đăng nhập:

```http
PUT /api/v1/users/me/password
Authorization: Bearer <accessToken>

{
  "currentPassword": "OldStrongPassword@123",
  "newPassword": "NewStrongPassword@123"
}
```

Hai luồng thành công đều cập nhật password, đổi `SecurityStamp` và thu hồi toàn bộ refresh token trong cùng transaction. Access token cũ cũng mất hiệu lực do backend đối chiếu `security_stamp` ở mỗi request. Sau commit, hệ thống lưu thông báo bảo mật và gửi email; nếu lưu Notification lỗi, email vẫn được thử gửi dự phòng.

### 3.4 External login

`POST /api/v1/auth/external-login` hiện trả `503 AUTH_EXTERNAL_LOGIN_NOT_CONFIGURED`. Không có đăng nhập giả lập; chỉ bật khi có ClientId/ClientSecret thật.

## 4. Hồ sơ, avatar, kỹ năng và trang For You

| Method | Endpoint | Công dụng |
|---|---|---|
| `GET` | `/api/v1/users/me` | Hồ sơ và kỹ năng của người đang đăng nhập |
| `PUT` | `/api/v1/users/me/profile` | Cập nhật hồ sơ |
| `POST` | `/api/v1/users/me/avatar` | Upload multipart field `avatar` |
| `GET` | `/api/v1/users/for-you` | Công việc được giao, công việc gần đây và điểm cần chú ý |
| `GET` | `/api/v1/skills?category=&searchQuery=` | Tra danh mục kỹ năng |
| `POST` | `/api/v1/skills` | Tạo kỹ năng; cần `skill.catalog.manage` |
| `GET` | `/api/v1/users/{userId}/skills?projectId=` | Xem kỹ năng theo quan hệ dự án |
| `PUT` | `/api/v1/users/me/skills` | Thay toàn bộ danh sách kỹ năng cá nhân |
| `PUT` | `/api/v1/users/{userId}/skills/{skillId}/verify?projectId=` | Xác nhận kỹ năng; cần `skill.verify` |

Ví dụ cập nhật hồ sơ:

```json
{
  "fullName": "Nguyễn Văn A",
  "phoneNumber": "+84901234567",
  "timezone": "Asia/Ho_Chi_Minh",
  "jobTitle": "Backend Developer",
  "seniorityLevel": "Junior",
  "yearsOfExperience": 1.5,
  "bio": "Thành viên nhóm phát triển"
}
```

Avatar chấp nhận nội dung ảnh JPEG/PNG/WebP hợp lệ sau kiểm tra chữ ký tệp, không chỉ tin `Content-Type`. File được đặt tên ngẫu nhiên tại `ProjectMgmt.Solution/Assets/avatars` và phục vụ qua `/assets/avatars/{file}`.

`GET /api/v1/users/for-you` lấy dữ liệu thật qua read contract từ Delivery/Planning:

```json
{
  "success": true,
  "assignedIssues": [
    {
      "issueId": "uuid",
      "projectId": "uuid",
      "issueKey": "SCRUM-8",
      "title": "Hoàn thiện đăng nhập",
      "statusName": "In Progress",
      "dueDate": "2026-10-02"
    }
  ],
  "recentIssues": [],
  "attentionFocus": [
    "Bạn có 1 công việc quá hạn cần xử lý.",
    "Bạn đang có 4 công việc chưa hoàn thành."
  ]
}
```

## 5. Vai trò, quyền và thành viên dự án

| Method | Endpoint | Quyền |
|---|---|---|
| `GET` | `/api/v1/roles?scope=System|Organization|Project` | Đã đăng nhập |
| `GET` | `/api/v1/permissions` | Đã đăng nhập |
| `POST` | `/api/v1/roles` | `identity.role.manage` ở System |
| `PUT` | `/api/v1/roles/{roleId}/permissions` | `identity.role.manage` ở System |
| `GET` | `/api/v1/users/{userId}/roles?scopeType=&scopeId=` | Chính mình hoặc `member.read` đúng scope |
| `POST` | `/api/v1/users/{userId}/roles` | Quyền quản trị đúng scope |
| `DELETE` | `/api/v1/users/{userId}/roles/{userRoleId}` | Quyền quản trị đúng scope |
| `GET` | `/api/v1/projects/{projectId}/members` | `member.read` |
| `POST` | `/api/v1/projects/{projectId}/members` | `member.invite` |
| `PUT` | `/api/v1/projects/{projectId}/members/{userId}/role` | `member.role.assign` |
| `DELETE` | `/api/v1/projects/{projectId}/members/{userId}` | `member.remove` |

`projectId` là UUID; không dùng `projectKey` trong route thành viên. Backend chặn tự nâng quyền, tự gỡ/hạ vai trò và gỡ Project Manager cuối cùng. Thêm/đổi/gỡ thành viên thành công sẽ lưu Notification, đẩy SignalR và gửi email cho người bị tác động.

Tạo tổ chức/dự án lấy người tạo từ JWT, không tin `OwnerId`, `CreatedByUserId` hoặc `LeadUserId` do client gửi. Dữ liệu Planning và role `OrgOwner`/`ProjectManager` đầu tiên được commit trong cùng transaction dùng chung.

## 6. Notification Center và SignalR

| Method | Endpoint | Kết quả |
|---|---|---|
| `GET` | `/api/v1/notifications?isRead=&page=1&pageSize=20` | `items`, `totalCount`, `page`, `pageSize` |
| `GET` | `/api/v1/notifications/unread-count` | `unreadCount` |
| `PUT` | `/api/v1/notifications/{notificationId}/read` | Thông báo đã cập nhật |
| `PUT` | `/api/v1/notifications/read-all` | `unreadCount: 0`, `totalCount` là số bản ghi vừa cập nhật |

Mọi truy vấn/đánh dấu đều gắn với user trong JWT; đổi ID không thể đọc thông báo của người khác.

SignalR Hub: `/hubs/notifications`. Với WebSocket/SSE, client truyền JWT qua `accessTokenFactory`; server chỉ nhận query `access_token` cho đúng đường dẫn Hub.

```ts
import * as signalR from '@microsoft/signalr';

const connection = new signalR.HubConnectionBuilder()
  .withUrl(`${apiBaseUrl}/hubs/notifications`, {
    accessTokenFactory: () => authStore.accessToken ?? ''
  })
  .withAutomaticReconnect()
  .build();

connection.on('notificationReceived', notification => {
  notificationStore.prepend(notification);
});

await connection.start();
```

Thông báo được lưu DB trước; SignalR/email chạy sau đó. Lỗi kênh vận chuyển được ghi vào `ProjectMgmt.Solution/Logs` và không rollback nghiệp vụ chính.

## 7. AI 1 Core — fake inference, dữ liệu thật

AI hiện tại **không gọi model ngoài**. `FakeAiBreakdownInference` sinh ba nhóm việc xác định: phân tích, triển khai, kiểm thử. Ranh giới thay thế bằng model thật nằm ở `IAiBreakdownInference`; các phần audit, feedback, quyền và apply không cần viết lại.

### 7.1 Generate

```http
POST /api/v1/ai/breakdown/generate
Authorization: Bearer <accessToken>

{
  "issueId": "uuid-cua-story-goc",
  "projectContext": "Modular Monolith, ASP.NET Core, MySQL"
}
```

Backend đọc tiêu đề/mô tả Issue thật từ DB, không tin `storyTitle`/`storyDescription` do client gửi. Cần quyền `ai.breakdown.request` đúng dự án. Kết quả `201`:

```json
{
  "success": true,
  "generationId": "uuid",
  "issueId": "uuid",
  "projectId": "uuid",
  "status": "Completed",
  "modelVersion": "fake:deterministic-breakdown-v1",
  "promptVersion": "breakdown.system.v1",
  "suggestedSubTasks": [
    {
      "suggestedTaskId": "uuid",
      "tempId": "temp_1",
      "title": "Phân tích và thiết kế kỹ thuật...",
      "description": "...",
      "estimatedHours": 2,
      "estimatePoints": 1,
      "acceptanceCriteria": ["..."],
      "suggestedSkills": ["analysis", "backend"],
      "userAction": "Pending"
    }
  ]
}
```

`AiGenerationLog` được tạo thật; raw/parsed response, model, prompt, thời gian và `AiSuggestedTask` được lưu thật.

### 7.2 Đọc kết quả và lưu feedback

```http
GET /api/v1/ai/breakdown/{generationId}
```

Chủ yêu cầu hoặc thành viên còn quyền trong dự án được xem.

```http
POST /api/v1/ai/breakdown/{generationId}/feedback

{
  "suggestedTaskId": "uuid",
  "userAction": "Edited",
  "finalSummary": "Tiêu đề sau chỉnh sửa",
  "finalDescription": "Mô tả sau chỉnh sửa",
  "finalAcceptanceCriteria": ["Điều kiện 1"],
  "finalEstimatePoints": 2
}
```

`userAction` nhận `Kept`, `Edited`, `Rejected`. Khi `Rejected`, phải có `rejectReason`. Backend tự tính `editDistanceRatio`; không tin giá trị ratio từ client.

### 7.3 Apply thành Issue thật

```http
POST /api/v1/ai/breakdown/{generationId}/apply

{
  "parentIssueId": "uuid-cua-story-goc",
  "selectedSubTasks": [
    {
      "suggestedTaskId": "uuid",
      "finalSummary": "Tên cuối cùng",
      "finalDescription": "Mô tả cuối cùng",
      "finalAcceptanceCriteria": ["Điều kiện nghiệm thu"],
      "finalEstimatePoints": 2,
      "estimatedHours": 4
    }
  ]
}
```

Cần `ai.breakdown.apply`. Backend khóa generation và project, kiểm tra suggestion thuộc generation, cấp `IssueNumber`, tạo `Issue`, `AcceptanceCriteria`, `ActivityLog`, cập nhật feedback/`CreatedIssueId` và đánh dấu generation đã apply trong **một transaction chung**. Gọi lại trả danh sách Issue đã tạo trước đó và không sinh bản trùng.

### 7.4 Prompt template

| Method | Endpoint | Quyền |
|---|---|---|
| `GET` | `/api/v1/ai/prompt-templates?taskType=Breakdown|Assignment` | Đã đăng nhập |
| `POST` | `/api/v1/ai/prompt-templates` | `ai.prompt.manage` ở System |
| `PUT` | `/api/v1/ai/prompt-templates/{promptId}/activate` | `ai.prompt.manage` ở System |

Tạo prompt mới tự tăng version theo `code`. JSON Schema phải là JSON object hợp lệ. Kích hoạt một prompt sẽ vô hiệu prompt đang active cùng `taskType` trong transaction có khóa.

## 8. Gợi ý tổ chức API phía frontend

Không gọi `fetch`/`HttpClient` rải rác trong component. Đặt toàn bộ tích hợp trong một thư mục, ví dụ:

```text
src/app/core/api/
├── api-client.ts              # base URL, JSON, error mapping
├── auth-token.store.ts        # access/refresh token
├── auth.interceptor.ts        # Bearer + refresh một lần khi 401
├── account.api.ts
├── profile.api.ts
├── rbac.api.ts
├── notification.api.ts
├── notification-realtime.ts
└── ai-breakdown.api.ts
```

Quy tắc refresh: tại một thời điểm chỉ có một request refresh; các request `401` khác chờ cùng promise. Nếu refresh thất bại, xóa phiên và chuyển về đăng nhập. Không retry `401` vô hạn.

## 9. Kiểm tra trước khi bàn giao

```powershell
dotnet test ProjectMgmt.Tests\ProjectMgmt.Tests.csproj --no-restore
dotnet build ProjectMgmt.Solution\ProjectMgmt.Solution.csproj --no-restore
```

Các kiểm thử hiện có bao phủ transaction auth, security stamp, profile/avatar/skills, RBAC, provisioning creator, Notification, AI fake/feedback/apply contract, For You và bảo vệ endpoint host. Log chạy nằm trong `ProjectMgmt.Solution/Logs`; không gửi thư mục log lên Git.
