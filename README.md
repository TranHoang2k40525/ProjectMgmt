# BÁO CÁO DỰ ÁN: HỆ THỐNG QUẢN LÝ DỰ ÁN SCRUM TÍCH HỢP AI (PROJECTMGMT)

> **Đơn vị thực hiện:** Nhóm 7 — Đồ án Tốt nghiệp  
> **Phiên bản hệ thống:** v1.0.0-GA  
> **Trạng thái:** Hoàn thiện tích hợp, vượt qua kiểm thử chất lượng toàn diện (100% Tests Passed)

---

## 📌 1. TỔNG QUAN DỰ ÁN (EXECUTIVE SUMMARY)

**ProjectMgmt** là giải pháp phần mềm quản lý dự án toàn diện được thiết kế theo phương pháp luận **Scrum/Agile**, tích hợp các mô hình **Trí tuệ nhân tạo (AI)** có sự giám sát của con người (**Human-in-the-loop**). Hệ thống giải quyết các thách thức điển hình trong chuyển giao phần mềm hiện đại:
- **Tối ưu hóa thời gian lập kế hoạch:** AI tự động phân rã yêu cầu lớn (*User Story*) thành danh sách nhiệm vụ con (*Sub-tasks*) và tiêu chí nghiệm thu (*Acceptance Criteria*).
- **Phân công công việc thông minh:** Đề xuất nhân sự phù hợp nhất cho từng nhiệm vụ dựa trên ma trận kỹ năng (*Skill Matrix*), mức thâm niên, tải công việc thực tế (*Workload*) và năng lực Sprint (*Capacity*).
- **Minh bạch và bảo vệ quy trình:** Kiểm soát quyền hạn chặt chẽ theo tổ chức/dự án (*RBAC*), trực quan hóa tiến độ bằng Kanban Board thời gian thực, biểu đồ Burndown và sơ đồ Roadmap Gantt.

---

## 🏛️ 2. KIẾN TRÚC HỆ THỐNG (SYSTEM ARCHITECTURE)

Hệ thống được thiết kế theo mô hình **Modular Monolith** kết hợp nguyên lý **Clean Architecture**, phân định ranh giới nghiệp vụ độc lập, giúp mã nguồn mạch lạc, dễ mở rộng và kiểm thử:

```
                      ┌──────────────────────────────────────────┐
                      │        ProjectMgmt Web Frontend          │
                      │    (Angular Standalone + Signal State)   │
                      └─────────────────────┬────────────────────┘
                                            │ HTTP / REST / SignalR
                                            ▼
                      ┌──────────────────────────────────────────┐
                      │        ASP.NET Core Web Host             │
                      │       (ProjectMgmt.Solution)             │
                      └──────┬──────────────┼─────────────┬──────┘
                             │              │             │
              ┌──────────────┴──────┐ ┌─────┴───────┐ ┌───┴────────────────┐
              │ IdentityExperience  │ │  Planning   │ │DeliveryIntelligence│
              │ (Domain/App/Infra)  │ │(Domain/App/ │ │ (Domain/App/Infra) │
              │                     │ │   Infra)    │ │                    │
              └──────────────┬──────┘ └─────┬───────┘ └───┬────────────────┘
                             │              │             │
                             ▼              ▼             ▼
                      ┌──────────────────────────────────────────┐
                      │               MySQL 8.0                  │
                      │   (55 Entities, 4 Views, Migrations)     │
                      └──────────────────────────────────────────┘
```

### 2.1 Các Module nghiệp vụ chính
1. **`IdentityExperience`**: Quản lý định danh, bảo mật phiên làm việc (JWT, Refresh Token Rotation), phân quyền truy cập đa cấp (RBAC), hồ sơ kỹ năng cá nhân và cổng thông báo đẩy thời gian thực qua SignalR.
2. **`Planning`**: Quản lý không gian làm việc (Workspaces, Projects), phân cấp nhiệm vụ (Epic, User Story, Task, Bug, Sub-task), chu kỳ phát triển Sprint, bảng Kanban kéo thả, Product Backlog và xuất/nhập dữ liệu Excel.
3. **`DeliveryIntelligence`**: Bộ máy suy luận trí tuệ nhân tạo (AI Work Breakdown Structure, Đề xuất phân bổ nhân sự theo thuật toán tối ưu hóa đa yếu tố), quản trị mô hình AI và Audit Log.

---

## 🚀 3. TÍNH NĂNG CỐT LÕI (CORE FEATURES)

### 🔹 Module 1: Định danh, Phân quyền & Cộng tác (Identity & Access)
* **Xác thực bảo mật:** Đăng nhập/Đăng ký xác thực hai bước qua OTP Email, cơ chế cấp phát JWT Token và cơ chế xoay vòng Refresh Token an toàn.
* **Ma trận phân quyền (RBAC Matrix):** Phân quyền chi tiết theo vai trò hệ thống (*System Admin*) và vai trò dự án (*Project Manager, Scrum Master, Developer, Viewer*). Kiểm tra quyền trên từng Controller Endpoint.
* **Hồ sơ & Danh mục kỹ năng:** Quản lý kỹ năng cá nhân theo cấp độ (Cơ bản, Khá, Chuyên sâu) và số năm kinh nghiệm.
* **Trung tâm thông báo thời gian thực:** Đồng bộ tin nhắn, sự kiện chuyển trạng thái nhiệm vụ tức thời qua WebSockets (SignalR).

### 🔹 Module 2: Lập kế hoạch & Quản trị Scrum (Planning & Scrum Engine)
* **Không gian làm việc linh hoạt:** Tạo và quản lý nhiều dự án độc lập với `ProjectKey` định danh riêng biệt.
* **Phân cấp công việc chuẩn Agile:** Quản lý cây công việc từ **Epic ➔ Story ➔ Task / Bug ➔ Sub-task**.
* **Interactive Kanban Board:** Bảng điều khiển công việc trực quan 4 cột (*To Do, In Progress, In Review, Done*), kiểm soát giới hạn công việc đang thực hiện (*WIP Limit*).
* **Vòng đời Sprint chuẩn tắc:** Quản lý trạng thái Sprint (*Planned ➔ Active ➔ Completed*). Ràng buộc duy nhất 1 Active Sprint/Project tại một thời điểm.
* **Biểu đồ & Báo cáo quản trị:** Biểu đồ Burndown Chart theo dõi tiến độ đốt Story Point, sơ đồ Roadmap Gantt và báo cáo khối lượng công việc.
* **Tích hợp Excel:** Nhập/xuất danh sách nhiệm vụ và Sprint thông minh qua bảng tính Excel chuẩn hóa.

### 🔹 Module 3: Trí tuệ Nhân tạo & Vận hành Chuyển giao (Delivery Intelligence & AI)
* **AI 1 — Tự động phân rã yêu cầu (AI WBS):** Tiếp nhận User Story và tự động phân tách thành các Sub-task kèm tiêu chí nghiệm thu chi tiết (*Acceptance Criteria*).
* **AI 2 — Đề xuất phân công thông minh:** Đánh giá điểm phù hợp của nhân sự dựa trên:
  $$\text{Score} = f(\text{Skill Match}, \text{Seniority}, \text{Capacity}, \text{Historical Velocity})$$
* **Nguyên tắc Human-in-the-Loop:** Toàn bộ đề xuất từ AI chỉ có hiệu lực khi được Product Owner hoặc Scrum Master xét duyệt và xác nhận.
* **AI Governance:** Theo dõi lịch sử phản hồi gợi ý AI (chấp thuận, chỉnh sửa, từ chối) phục vụ cải tiến dữ liệu huấn luyện.

---

## 💻 4. NGĂN XẾP CÔNG NGHỆ (TECHNOLOGY STACK)

| Thành phần | Công nghệ / Thư viện | Vai trò |
| :--- | :--- | :--- |
| **Backend Framework** | ASP.NET Core (.NET 10) | Nền tảng Web API hiệu năng cao |
| **Kiến trúc Backend** | Modular Monolith, Clean Architecture | Phân tách ranh giới nghiệp vụ sạch sẽ |
| **Cơ sở dữ liệu** | MySQL 8.0.x / MariaDB | Hệ cơ sở dữ liệu quan hệ ACID chuẩn tắc |
| **ORM & Data Access** | Pomelo Entity Framework Core 9.0 | Mapping 55 thực thể, Migration tự động |
| **Realtime Engine** | ASP.NET Core SignalR | Đồng bộ thông báo và bảng công việc |
| **API Documentation**| Swashbuckle / OpenAPI 3.0 | Swagger UI tích hợp xác thực JWT Bearer |
| **Frontend Framework**| Angular 20 Standalone Components | Single Page Application phản ứng nhanh |
| **State Management** | Angular Signals (`signal`, `computed`) | Quản lý trạng thái Reactive nhẹ và mượt mà |
| **Testing Engine** | xUnit, Vitest, Playwright E2E | Kiểm thử đơn vị, tích hợp và giao diện thực |
| **Logging & Security** | Serilog, BCrypt.NET, libphonenumber | Ghi nhật ký có cấu trúc, mã hóa mật khẩu |

---

## ⚙️ 5. HƯỚNG DẪN CÀI ĐẶT & KHỞI CHẠY (LOCAL DEVELOPMENT)

### 5.1 Yêu cầu môi trường
* **.NET SDK:** Phiên bản 10.0+
* **Node.js:** Phiên bản 20.x hoặc 22.x LTS & npm 10.x+
* **MySQL Server:** Phiên bản 8.0.16 trở lên

---

### 5.2 Cấu hình Backend (`ProjectMgmt.Solution`)

Hệ thống sử dụng tệp cấu hình [appsettings.Development.json](ProjectMgmt.Solution/appsettings.Development.json) cho môi trường phát triển local. Cấu hình mẫu:

```json
{
  "ConnectionStrings": {
    "ProjectMgmt": "Server=127.0.0.1;Port=3306;Database=projectmgmt;User=root;Password=YOUR_DATABASE_PASSWORD;SslMode=None;"
  },
  "Database": {
    "Provider": "Pomelo.EntityFrameworkCore.MySql",
    "ServerVersion": "8.0.46"
  },
  "Jwt": {
    "Issuer": "ProjectMgmt.Api",
    "Audience": "ProjectMgmt.Web",
    "SigningKey": "YOUR_SECURE_JWT_SIGNING_KEY_MINIMUM_32_BYTES",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 30
  },
  "Otp": {
    "HashKey": "YOUR_OTP_SECRET_HASH_KEY"
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "Port": 587,
    "User": "YOUR_EMAIL_ADDRESS@gmail.com",
    "Pass": "YOUR_EMAIL_APP_PASSWORD",
    "From": "YOUR_EMAIL_ADDRESS@gmail.com",
    "FromName": "Hệ thống Quản lý Dự án Scrum tích hợp AI",
    "EnableSsl": true,
    "FallbackToLogOnFailure": true
  }
}
```

> **Lưu ý an toàn:**
> - `FallbackToLogOnFailure: true` cho phép hệ thống ghi mã OTP ra log Console khi không có kết nối internet/SMTP, giúp việc test local không bị gián đoạn.
> - Tuyệt đối không commit mật khẩu thật hoặc khóa ký bảo mật thực tế lên kho mã nguồn công khai.

---

### 5.3 Khởi chạy ứng dụng

#### 1. Chạy Backend API:
```powershell
cd ProjectMgmt.Solution
dotnet restore
dotnet run --launch-profile http
```
* **Cổng dịch vụ:** `http://localhost:5083`
* **Tài liệu Swagger:** `http://localhost:5083/swagger/index.html`

#### 2. Chạy Frontend Web:
```powershell
cd frontend/projectmgmt-web
npm install
npm start
```
* **Cổng giao diện:** `http://localhost:4200`

---

### 5.4 Hướng dẫn xác thực trên Swagger UI
1. Mở trang Swagger tại `http://localhost:5083/swagger/index.html`.
2. Gọi API `POST /api/v1/auth/login` để lấy chuỗi `accessToken`.
3. Nhấp vào nút **Authorize 🔓** ở góc trên bên phải màn hình Swagger.
4. Dán token vừa lấy vào ô **Value** rồi nhấn **Authorize** ➔ **Close**.
5. Biểu tượng khóa sẽ chuyển sang trạng thái khóa **🔒**. Toàn bộ các API được gọi sau đó sẽ tự động được gửi kèm token mà không cần nhập lại.

---

## 🧪 6. CHẤT LƯỢNG MÃ NGUỒN & KIỂM THỬ (QUALITY ASSURANCE)

Dự án áp dụng quy trình kiểm thử tự động đa tầng nghiêm ngặt:

```
┌────────────────────────────────────────────────────────┐
│  End-to-End Tests (Playwright Chromium)               │ 18 luồng kiểm thử giao diện
├────────────────────────────────────────────────────────┤
│  Frontend Unit Tests (Vitest + Angular TestBed)       │ 51/51 tests PASSED
├────────────────────────────────────────────────────────┤
│  Backend Integration & Security Tests (xUnit)         │ 45/45 tests PASSED
├────────────────────────────────────────────────────────┤
│  Linter & Code Analysis (ESLint + Roslyn Analyzers)   │ 0 Errors, 0 Warnings
└────────────────────────────────────────────────────────┘
```

* **Chạy kiểm thử Frontend:**
  ```powershell
  cd frontend/projectmgmt-web
  npm run lint    # Kiểm tra chuẩn mã nguồn (0 problems)
  npm test        # Chạy 51 bài kiểm thử Vitest
  ```
* **Chạy kiểm thử Backend:**
  ```powershell
  dotnet test ProjectMgmt.Tests/ProjectMgmt.Tests.csproj
  ```
* **Kiểm thử E2E trên trình duyệt thực:**
  ```powershell
  node tests/e2e-chrome-test.cjs
  ```

---

## 📚 7. TÀI LIỆU DỰ ÁN LIÊN QUAN (DOCUMENTATION)

Toàn bộ tài liệu phân tích, đặc tả và kế hoạch thực hiện được lưu trữ trong thư mục `docs/`:

1. **[Cẩm nang kỹ thuật toàn diện](docs/cam-nang-du-an/Cam-nang-ky-thuat.md)**: Tài liệu chuẩn tắc đặc tả kiến trúc, ranh giới module, quy tắc nghiệp vụ và cơ sở dữ liệu chi tiết.
2. **[Báo cáo đặc tả API hệ thống](docs/Bao-cao-Dac-ta-API-He-thong-ProjectMgmt.html)**: Báo cáo chi tiết toàn bộ các Endpoint API, phương thức HTTP, kiểu dữ liệu Request/Response tương ứng với giao diện.
3. **[Bảng phân công nhiệm vụ nhóm 7](docs/b%E1%BA%A3n%20ph%C3%A2n%20c%C3%B4ng%20nhi%E1%BB%87m%20v%E1%BB%A5%20nh%C3%B3m%207.xlsx)**: Bảng chi tiết khối lượng công việc và phân chia vai trò thành viên nhóm.

---

## 👥 8. ĐỘI NGŨ THỰC HIỆN (PROJECT TEAM)

* **Nhóm sinh viên:** Nhóm 7
* **Dự án:** Đồ án tốt nghiệp đại học
* **Đề tài:** Xây dựng Hệ thống Quản lý Dự án Scrum tích hợp Trí tuệ Nhân tạo hỗ trợ phân rã yêu cầu và đề xuất phân công công việc.

---
*© 2026 ProjectMgmt Team. Tài liệu được biên soạn phục vụ đồ án tốt nghiệp và bàn giao kỹ thuật.*
