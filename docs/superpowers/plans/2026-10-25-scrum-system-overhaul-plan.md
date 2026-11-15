# KẾ HOẠCH TRIỂN KHAI: HỆ THỐNG QUẢN LÝ DỰ ÁN SCRUM TOÀN DIỆN (AGILE/SCRUM ENGINE)

**Mã kế hoạch:** PLAN-2026-10-25-SCRUM-OVERHAUL  
**Ngày lập:** 2026-10-25  
**Spec tham chiếu:** `docs/superpowers/specs/2026-10-25-scrum-system-overhaul-design.md`  
**Quy chuẩn áp dụng:** `GEMINI.md` (STRICT NO-KEYBOARD-EMOJI, HIERARCHICAL SCRUM RBAC)

---

## MỤC TIÊU
Thực hiện tái cấu trúc và hoàn thiện hệ thống theo Hướng tiếp cận A:
1. Dọn dẹp module thừa ngoài bản đặc tả (AI DataOps, Dataset, Model Training).
2. Chuyển hóa 100% cấu hình cứng (hardcoded) trên giao diện thành dữ liệu động được quản trị từ Backend và Database (Issue Types, Labels, Workflow Statuses với các cờ hành vi logic `IsInitial`, `IsDone`, `IsActiveWork`, `IsCanceled`).
3. Hoàn thiện các trụ cột Scrum còn thiếu: Spec Hub (bôi đen tạo User Story, liên kết hai chiều, DoR), Planning Poker Online (Fibonacci), Daily Blocker Flag (viền đỏ nổi bật, ghim đầu cột), Sprint Review PO Sign-off (AC Checkboxes), Sprint Retrospective Board, và Complete Sprint Rollover Wizard.

---

## GIAI ĐOẠN 1: DỌN DẸP TÍNH NĂNG THỪA & CẬP NHẬT CƠ SỞ DỮ LIỆU (DATABASE & CLEANUP)

### Công việc chi tiết:
1. **Frontend Cleanup**:
   - Xóa thư mục `frontend/projectmgmt-web/src/app/features/ai-dataops/`.
   - Xóa file `frontend/projectmgmt-web/src/app/features/identity/ai-governance-page.ts`.
   - Gỡ bỏ routes `ai-dataops` và `admin/ai-governance` khỏi `frontend/projectmgmt-web/src/app/app.routes.ts`.
   - Gỡ bỏ mục điều hướng AI DataOps khỏi sidebar / topbar trong `frontend/projectmgmt-web/src/app/layouts/app-shell/app-shell.ts`.
2. **Backend Cleanup**:
   - Xóa file `ProjectMgmt.Solution/Controllers/DataOpsController.cs`.
   - Xóa file `ProjectMgmt.Solution/Controllers/AiPromptTemplatesController.cs`.
   - Dọn dẹp các service/DTO không sử dụng liên quan đến Data Cleaning và Training Run.
3. **Database Schema & Entity Update (`projectmgmt_schema_mysql_optimized.sql` & EF Core Entities)**:
   - Cập nhật bảng `WorkflowStatus`:
     - Xóa ràng buộc `CK_WorkflowStatus_Category`.
     - Thêm các cột: `IsDone` (TINYINT(1)), `IsActiveWork` (TINYINT(1)), `IsCanceled` (TINYINT(1)), `ColorHex` (VARCHAR(10)).
     - Cập nhật EF Core Entity `Planning.Domain.Entities.WorkflowStatus` và cấu hình DbContext.
   - Cập nhật bảng `Issue`:
     - Thêm các cột: `IsBlocked` (TINYINT(1)), `BlockReason` (VARCHAR(500)), `BlockedAt` (DATETIME(6)), `EstimatedHours` (DECIMAL(5,2)), `RemainingHours` (DECIMAL(5,2)).
     - Cập nhật EF Core Entity `DeliveryIntelligence.Domain.Entities.Issue` và cấu hình DbContext.
   - Thêm các bảng mới:
     - `ProjectDocument` và `ProjectDocumentComment` (Spec Hub).
     - `SpecStoryLink` (Liên kết 2 chiều).
     - `PlanningPokerSession` và `PlanningPokerVote`.
     - `SprintRetrospectiveCard`.
     - Cập nhật EF Core Entities và ModelBuilder mapping tương ứng.

### Tiêu chí nghiệm thu Giai đoạn 1:
- Không còn sót file hoặc route của `ai-dataops` và `ai-governance`.
- Backend build thành công (`dotnet build ProjectMgmt.slnx` đạt 0 error).
- Frontend build thành công (`npm run build` hoặc lint không lỗi).

---

## GIAI ĐOẠN 2: BACKEND DYNAMIC CORE (ISSUE TYPES, LABELS, WORKFLOW STATUSES)

### Công việc chi tiết:
1. **Hoàn thiện CRUD Động cho Issue Types (`ProjectCustomizationController.cs` & `ProjectsController.cs`)**:
   - Cung cấp đầy đủ API:
     - `GET /api/v1/projects/{projectId}/issue-types` (kèm `IconKey`, `ColorHex`, `HierarchyLevel`, `IsSubtask`).
     - `POST /api/v1/projects/{projectId}/issue-types` (hỗ trợ nhập `Name`, `Icon`, `Color`, `HierarchyLevel`, `IsSubtask`).
     - `PUT /api/v1/projects/{projectId}/issue-types/{id}` (chỉnh sửa thông tin loại công việc).
     - `DELETE /api/v1/projects/{projectId}/issue-types/{id}` (kiểm tra an toàn: cấm xóa nếu còn issue đang dùng).
2. **Hoàn thiện CRUD Động cho Labels (`IssueLabelLinkController.cs`)**:
   - `GET /api/v1/projects/{projectId}/labels`: Trả về danh sách nhãn kèm số lượng task và mã màu Hex.
   - `POST /api/v1/projects/{projectId}/labels`: Tạo nhãn với tên và mã màu Hex tùy chọn.
   - `PUT /api/v1/labels/{id}`: Cập nhật tên và mã màu.
   - `DELETE /api/v1/labels/{id}`: Xóa nhãn khỏi dự án.
3. **Hoàn thiện CRUD Động cho Workflow Statuses (`WorkflowController.cs`)**:
   - Cung cấp các API:
     - `POST /api/v1/projects/{projectId}/statuses`: Tạo trạng thái mới kèm cờ hành vi logic (`IsInitial`, `IsDone`, `IsActiveWork`, `IsCanceled`) và `ColorHex`. Đồng thời tự tạo `BoardColumn` tương ứng trên bảng Kanban.
     - `PUT /api/v1/projects/{projectId}/statuses/{statusId}`: Sửa trạng thái và cờ hành vi.
     - `DELETE /api/v1/projects/{projectId}/statuses/{statusId}`: Xóa an toàn (từ chối xóa nếu còn task).
     - `PUT /api/v1/projects/{projectId}/statuses/reorder`: Sắp xếp thứ tự cột.
4. **Cập nhật `WorkItemsController.cs`**:
   - Loại bỏ fallback string cứng (`"To Do"`, `"Task"`).
   - Đọc và validate trực tiếp theo `IssueTypeId` và `WorkflowStatusId` của dự án.
   - Khi task chuyển sang trạng thái có `IsDone = 1`, tự động đánh dấu thời gian hoàn thành `CompletedAt`.

### Tiêu chí nghiệm thu Giai đoạn 2:
- Toàn bộ các API Issue Types, Labels, Workflow Statuses hoạt động chuẩn RESTful và kiểm tra quyền RBAC chặt chẽ.
- Backend tests đạt 100% pass.

---

## GIAI ĐOẠN 3: FRONTEND DYNAMIC CORE (PROJECT SETTINGS & DYNAMIC UI)

### Công việc chi tiết:
1. **Nâng cấp Cài đặt Dự án (`project-settings-page.ts`)**:
   - Thêm tab **"Loại công việc (Issue Types)"**: Hiển thị danh mục loại công việc hiện tại. Cung cấp Modal tạo/sửa: chọn icon Google Material Symbols từ bộ icon chuẩn, chọn mã màu Hex, chọn cấp độ.
   - Thêm tab **"Quản lý Nhãn (Labels)"**: Hiển thị danh mục nhãn của dự án kèm mã màu Hex và số lượng task. Cung cấp chức năng tạo/sửa/xóa nhãn.
   - Nâng cấp tab **"Cấu hình Workflow (`workflow-settings-page.ts`)"**:
     - Nút "+ Thêm Trạng thái mới": Cho phép nhập tên, chọn màu, tick các cờ hành vi logic (`IsInitial`, `IsDone`, `IsActiveWork`, `IsCanceled`).
     - Cho phép sửa, xóa trạng thái, sắp xếp thứ tự và cấu hình transitions giữa các trạng thái bất kỳ.
2. **Gỡ bỏ Toàn bộ Mảng Hardcode Tĩnh trên Frontend**:
   - `backlog-page.ts`: Gỡ bỏ `['Story', 'Task', 'Bug', 'Use-Case', 'Epic', 'Sub-task']`. Đọc 100% từ `projectService.issueTypes()`.
   - `board-page.ts` và `issue-detail-page.ts`: Tải loại công việc và trạng thái động từ service.
   - Component chọn nhãn (Label selector): Cho phép tìm kiếm nhãn đã có của dự án và chọn màu sắc động.

### Tiêu chí nghiệm thu Giai đoạn 3:
- Người dùng có thể tạo một loại công việc mới (ví dụ: "Research", icon `menu_book`, màu cam `#F59E0B`), ngay lập tức loại công việc này xuất hiện trong dropdown tạo task tại Backlog và Board.
- Không còn bất kỳ mảng hardcode tĩnh nào trong mã nguồn Angular.

---

## GIAI ĐOẠN 4: KHO TÀI LIỆU ĐẶC TẢ (SPEC HUB) & LIÊN KẾT HAI CHIỀU

### Công việc chi tiết:
1. **Backend Controller & Service (`ProjectDocumentsController.cs`)**:
   - Quản lý tài liệu: `GET`, `POST`, `PUT`, `DELETE` tài liệu đặc tả theo `ProjectId`.
   - Quản lý vòng đời tài liệu: `Draft` ➔ `Review` ➔ `Approved`. Quyền đổi sang `Approved` chỉ cấp cho PO/BA/Lead.
   - Quản lý bình luận: `POST /documents/{id}/comments` (hỗ trợ lưu đoạn văn bản trích dẫn).
   - API bóc tách Backlog: `POST /documents/{id}/extract-story` (nhận đoạn text bôi đen, tạo User Story trong Backlog và lưu liên kết `SpecStoryLink`).
   - Cổng DoR (Definition of Ready): Service tính toán cờ `IsReady` khi Story có đủ AC, Story Points > 0, và liên kết Spec.
2. **Frontend Spec Hub Module (`/projects/:id/documents`)**:
   - Giao diện xem và soạn thảo tài liệu đặc tả nghiệp vụ (Markdown editor/viewer).
   - Phân quyền giao diện: Chỉ BA/PO thấy nút chỉnh sửa/duyệt; Dev/QA xem ở chế độ Read-only và có quyền để lại bình luận inline.
   - Thanh công cụ bôi đen (Text selection tooltip): Khi bôi đen đoạn yêu cầu, hiện nút **"Tạo User Story"** ➔ Mở modal tạo Story với mô tả được trích xuất sẵn và danh sách thêm Acceptance Criteria (AC).
   - Huy hiệu liên kết hai chiều:
     - Trên tài liệu Spec: Hiển thị badge mã Story tương ứng.
     - Trên User Story (Backlog & Issue Detail): Hiển thị chip link mở thẳng tài liệu Spec.

### Tiêu chí nghiệm thu Giai đoạn 4:
- BA/PO có thể soạn tài liệu đặc tả, bôi đen văn bản để tạo User Story, kiểm tra thẻ Story thấy link trỏ ngược lại văn bản gốc.
- User Story đạt đủ tiêu chí tự động hiển thị huy hiệu `Ready`.

---

## GIAI ĐOẠN 5: CHU TRÌNH SCRUM KHÉP KÍN (POKER, DAILY, REVIEW, RETRO, ROLLOVER)

### Công việc chi tiết:
1. **Planning Poker Online (SignalR Real-time)**:
   - Backend: `PlanningPokerController.cs` và `PokerHub.cs`.
   - Frontend: Modal Planning Poker với bộ bài Fibonacci (`1, 2, 3, 5, 8, 13, 21`), cơ chế úp bài bí mật và lật bài đồng loạt khi chủ tọa bấm Reveal.
2. **Daily Blocker Flag**:
   - Backend: API `POST /issues/{id}/block` và `unblock`.
   - Frontend: Nút cắm cờ Blocker trên thẻ Kanban; khi bị chặn, thẻ có **viền đỏ rực**, nhãn `BLOCKED`, lý do vướng mắc, và tự động ghim lên đầu cột. Bắn thông báo thời gian thực đến PO/SM.
3. **Sprint Review & PO Sign-off**:
   - Backend: API nghiệm thu `accept-story` và `reject-story`.
   - Frontend: Màn hình Demo & Nghiệm thu: lọc danh sách Story xong 100% Sub-tasks, đối chiếu checkbox AC song song với link demo/PR, nút Accept (cộng điểm Velocity) và Reject (nhập lý do).
4. **Sprint Retrospective Board**:
   - Backend: `RetroCardsController.cs` (CRUD card, vote, convert-to-task).
   - Frontend: Bảng Retro 3 cột (*What went well*, *What went wrong*, *Action items*), công tắc **Ẩn danh (Anonymous)**, **Dot Voting**, và nút **Chuyển Action Item thành Task thực tế**.
5. **Complete Sprint Wizard & Rollover**:
   - Backend: API tổng kết Sprint và xử lý điều hướng việc dở dang về Backlog hoặc Sprint N+1.
   - Frontend: Modal Complete Sprint 3 bước với báo cáo Velocity và lựa chọn điều hướng việc chưa xong theo chuẩn Scrum.

### Tiêu chí nghiệm thu Giai đoạn 5:
- Toàn bộ chu kỳ Scrum hoạt động liền mạch từ Planning ➔ Daily ➔ Review ➔ Retro ➔ Đóng Sprint & Kế thừa việc dở dang.

---

## GIAI ĐOẠN 6: KIỂM THỬ TOÀN DIỆN & QUY CHUẨN GEMINI.MD

### Công việc chi tiết:
1. Quét toàn bộ repository để đảm bảo tuyệt đối không có emoji bàn phím nào (tuân thủ 100% Material Symbols Outlined).
2. Kiểm tra phân quyền Scrum RBAC phân tầng: Cấm leo thang đặc quyền, không để lộ chức năng quản trị hệ thống cho người dùng thường.
3. Chạy `npm run lint` trên Frontend (đảm bảo 0 lỗi).
4. Chạy toàn bộ test suite `dotnet test` và `npm test` để xác nhận hệ thống vận hành ổn định.

---

Sau khi kế hoạch này được phê duyệt, việc triển khai sẽ được bắt đầu ngay lập tức từ **Giai đoạn 1: Dọn dẹp tính năng thừa & Cập nhật Cơ sở Dữ liệu**.
