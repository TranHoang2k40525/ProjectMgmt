# TÀI LIỆU THIẾT KẾ KIẾN TRÚC: HỆ THỐNG QUẢN LÝ DỰ ÁN SCRUM TOÀN DIỆN (ENTERPRISE AGILE/SCRUM ENGINE)

**Mã tài liệu:** SPEC-2026-10-25-SCRUM-OVERHAUL  
**Ngày lập:** 2026-10-25  
**Trạng thái:** Chờ phê duyệt (Pending Review)  
**Tài liệu căn cứ:** `Bản đặc tả thiết kế cho một phần mềm quản lý dự án.txt` và quy chuẩn `GEMINI.md`.

---

## 1. TỔNG QUAN VÀ MỤC TIÊU (OVERVIEW & OBJECTIVES)

### 1.1 Bản chất Quản trị Dự án Scrum & Triết lý Agile
Hệ thống quản lý dự án được tái cấu trúc toàn diện để đáp ứng bản chất cốt lõi:
> **Scrum là một khung làm việc (framework) giúp tạo ra các giá trị thích ứng thông qua các giải pháp linh hoạt cho các vấn đề phức tạp, vận hành dựa trên thuyết thực nghiệm (Empiricism) và tư duy tinh gọn (Lean thinking).**
> - **Thuyết thực nghiệm (Empiricism)**: Dựa trên 3 trụ cột: **Minh bạch (Transparency)**, **Thanh tra (Inspection)**, và **Thích nghi (Adaptation)**. Mọi tiêu chí nghiệm thu (AC), điểm nghẽn (Blocker) và tiến độ cam kết đều được trực quan hóa thời gian thực.
> - **Tư duy tinh gọn (Lean thinking)**: Loại bỏ các tính năng dư thừa, tập trung vào giá trị chuyển giao thực tế, tối đa hóa lượng công việc chưa cần làm.

### 1.2 Mục tiêu Tái cấu trúc
1. **Loại bỏ tính năng thừa**: Gỡ bỏ hoàn toàn module AI DataOps, AI Datasets, AI Training Run khỏi hệ thống để tinh giản codebase và tập trung 100% vào nghiệp vụ Agile/Scrum.
2. **Chuyển dịch 100% sang Cấu hình Động ở Backend**:
   - Xóa bỏ toàn bộ các mảng hardcode tĩnh trên Frontend (`['Story', 'Task', 'Bug', 'Epic', 'Sub-task']`).
   - Cung cấp API và giao diện quản lý động: **Loại công việc (Issue Types)**, **Nhãn dán (Labels)**, và **Trạng thái Quy trình (Workflow Statuses)** tùy biến theo từng dự án.
3. **Phẳng hóa Trạng thái Quy trình (Behavior Flags Pattern)**:
   - Xóa bỏ ràng buộc cố định `CK_WorkflowStatus_Category`.
   - Mỗi trạng thái được gắn trực tiếp các cờ hành vi logic (`IsInitial`, `IsDone`, `IsActiveWork`, `IsCanceled`) và mã màu riêng, cho phép người dùng tự do định nghĩa mọi bước nghiệp vụ mà hệ thống vẫn tự động tính toán chính xác Burndown Chart và Velocity.
4. **Hiện thực hóa Trọn vẹn Vòng đời Scrum Khép kín**:
   - **Giai đoạn 0**: Kho tài liệu đặc tả dự án (**Spec Hub** / Project Docs) với tính năng **Bôi đen văn bản tạo User Story** và **Liên kết 2 chiều (Bidirectional Linking)**, kèm cổng kiểm soát chất lượng **Definition of Ready (DoR)**.
   - **Chặng 1 (Sprint Planning)**: Không gian Planning chia 2 cột, bắt buộc `Sprint Goal`, thanh kiểm soát sức chứa kép (Capacity & Velocity Bar), phòng chấm điểm **Planning Poker Online (Fibonacci)** úp bài/lật bài.
   - **Chặng 2 (Daily Scrum)**: Bảng Swimlane Kanban nhóm theo Story, cờ báo vướng mắc **Blocker Flag** (viền đỏ nổi bật, ghim đầu cột, thông báo thời gian thực), Burndown Chart 2 chế độ (Remaining Hours vs Story Points).
   - **Chặng 3 (Sprint Review)**: Màn hình Demo & Nghiệm thu theo từng tiêu chí **Acceptance Criteria (AC Checkboxes)** và chốt chặn phê duyệt của Product Owner (**PO Sign-off**).
   - **Chặng 4 (Sprint Retrospective)**: Bảng Retro 3 cột chuẩn Agile, công tắc **Góp ý Ẩn danh**, **Biểu quyết (Dot Voting)** và nút **Chuyển Action Item thành Task thực tế**.
   - **Chặng 5 (Sprint Rollover)**: Trình thuật sĩ Đóng Sprint (**Complete Sprint Wizard**), điều hướng việc dở dang (về Backlog hoặc sang Sprint N+1) và bảo toàn điểm Velocity.
5. **Tuân thủ Tuyệt đối Quy chuẩn GEMINI.md**:
   - **STRICT NO-KEYBOARD-EMOJI RULE**: Nghiêm cấm mọi ký tự emoji thô từ bàn phím. Toàn bộ biểu tượng trên giao diện phải sử dụng **Google Material Symbols Outlined**.
   - **HIERARCHICAL SCRUM RBAC**: Phân quyền nghiêm ngặt từ trên xuống (Org Owner ➔ Project Lead `LeadUserId` ➔ Scrum Master / PM ➔ Dev & QA ➔ Viewer). Chống leo thang đặc quyền.

---

## 2. PHẠM VI DỌN DẸP & LOẠI BỎ (CLEANUP & OUT-OF-SCOPE)

### 2.1 Loại bỏ Giao diện Frontend
* Xóa thư mục: `frontend/projectmgmt-web/src/app/features/ai-dataops/`.
* Xóa tệp: `frontend/projectmgmt-web/src/app/features/identity/ai-governance-page.ts`.
* Gỡ bỏ routes trong `app.routes.ts`: `ai-dataops` và `admin/ai-governance`.
* Gỡ bỏ các liên kết điều hướng tương ứng trên thanh Navigation Bar / Sidebar.

### 2.2 Loại bỏ Backend Controllers & Entities
* Xóa controllers:
  * `ProjectMgmt.Solution/Controllers/DataOpsController.cs`
  * `ProjectMgmt.Solution/Controllers/AiPromptTemplatesController.cs`
* Xóa use cases & DTOs liên quan đến Dataset, Training Run, Cleaning Rule trong module `DeliveryIntelligence`.

### 2.3 Loại bỏ Database Tables
* Xóa khỏi schema: `AiDataset`, `AiDatasetVersion`, `AiDatasetSample`, `AiDataCleaningRule`, `AiDataQualityFlag`, `AiTrainingRun`, `AiEvaluationResult`, `AiModel`, `AiPromptTemplate`.

---

## 3. THIẾT KẾ CƠ SỞ DỮ LIỆU (DATABASE SCHEMA DESIGN)

### 3.1 Cải tiến Bảng `WorkflowStatus` (Giải pháp Phẳng hóa Hành vi Logic)
```sql
ALTER TABLE `WorkflowStatus`
    DROP CHECK `CK_WorkflowStatus_Category`;

ALTER TABLE `WorkflowStatus`
    MODIFY COLUMN `Category` VARCHAR(50) NOT NULL DEFAULT 'Custom',
    ADD COLUMN `IsDone` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '1: Task ở trạng thái này được coi là hoàn thành',
    ADD COLUMN `IsActiveWork` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '1: Task đang thực hiện, tiêu tốn tài nguyên',
    ADD COLUMN `IsCanceled` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '1: Task bị hủy bỏ, không tính vào năng lực';

-- Cập nhật dữ liệu ban đầu cho các trạng thái hiện có
UPDATE `WorkflowStatus` SET `IsDone` = 1 WHERE `Category` = 'Done';
UPDATE `WorkflowStatus` SET `IsActiveWork` = 1 WHERE `Category` = 'InProgress';
UPDATE `WorkflowStatus` SET `IsInitial` = 1 WHERE `Category` = 'ToDo' AND `OrderIndex` = 1;
```

### 3.2 Cải tiến Bảng `Issue` (Hỗ trợ Daily Blocker & Ước lượng Giờ)
```sql
ALTER TABLE `Issue`
    ADD COLUMN `IsBlocked` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '1: Task đang bị block',
    ADD COLUMN `BlockReason` VARCHAR(500) NULL COMMENT 'Lý do vướng mắc',
    ADD COLUMN `BlockedAt` DATETIME(6) NULL COMMENT 'Thời điểm bị chặn',
    ADD COLUMN `EstimatedHours` DECIMAL(5,2) NULL COMMENT 'Ước lượng thời gian kỹ thuật cho Sub-task',
    ADD COLUMN `RemainingHours` DECIMAL(5,2) NULL COMMENT 'Số giờ còn lại của Sub-task phục vụ Burndown';
```

### 3.3 Bảng Mới: Kho Tài liệu Dự án (Spec Hub) & Liên kết Hai chiều
```sql
CREATE TABLE `ProjectDocument` (
    `Id`                CHAR(36)     NOT NULL,
    `ProjectId`         CHAR(36)     NOT NULL,
    `Title`             VARCHAR(250) NOT NULL,
    `Content`           LONGTEXT     NOT NULL COMMENT 'Nội dung định dạng Markdown/Rich-text',
    `Status`            VARCHAR(20)  NOT NULL DEFAULT 'Draft' COMMENT 'Draft, Review, Approved',
    `Version`           INT          NOT NULL DEFAULT 1,
    `CreatedByUserId`   CHAR(36)     NOT NULL,
    `ApprovedByUserId`  CHAR(36)     NULL,
    `CreatedAt`         DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt`         DATETIME(6)  NULL ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_ProjectDocument_Project` (`ProjectId`),
    CONSTRAINT `FK_ProjectDocument_Project` FOREIGN KEY (`ProjectId`) REFERENCES `Project`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `CK_ProjectDocument_Status` CHECK (`Status` IN ('Draft', 'Review', 'Approved'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Kho tài liệu đặc tả dự án (Spec Hub - Nguồn sự thật)';

CREATE TABLE `ProjectDocumentComment` (
    `Id`                CHAR(36)     NOT NULL,
    `DocumentId`        CHAR(36)     NOT NULL,
    `UserId`            CHAR(36)     NOT NULL,
    `HighlightedText`   TEXT         NULL COMMENT 'Đoạn văn bản được trích dẫn để hỏi đáp',
    `Content`           TEXT         NOT NULL COMMENT 'Nội dung bình luận',
    `CreatedAt`         DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_DocComment_Doc` (`DocumentId`),
    CONSTRAINT `FK_DocComment_Document` FOREIGN KEY (`DocumentId`) REFERENCES `ProjectDocument`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Bình luận inline trên tài liệu đặc tả';

CREATE TABLE `SpecStoryLink` (
    `Id`                CHAR(36)     NOT NULL,
    `DocumentId`        CHAR(36)     NOT NULL,
    `IssueId`           CHAR(36)     NOT NULL,
    `Excerpt`           TEXT         NULL COMMENT 'Đoạn yêu cầu trích xuất khi bôi đen',
    `CreatedAt`         DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_SpecStoryLink` (`DocumentId`, `IssueId`),
    KEY `IX_SpecStoryLink_Issue` (`IssueId`),
    CONSTRAINT `FK_SpecStoryLink_Doc` FOREIGN KEY (`DocumentId`) REFERENCES `ProjectDocument`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Liên kết 2 chiều giữa User Story và Tài liệu đặc tả';
```

### 3.4 Bảng Mới: Chấm điểm Planning Poker Online
```sql
CREATE TABLE `PlanningPokerSession` (
    `Id`                CHAR(36)     NOT NULL,
    `ProjectId`         CHAR(36)     NOT NULL,
    `SprintId`          CHAR(36)     NULL,
    `IssueId`           CHAR(36)     NOT NULL,
    `Status`            VARCHAR(20)  NOT NULL DEFAULT 'Voting' COMMENT 'Voting, Revealed, Finished',
    `FinalStoryPoints`  DECIMAL(4,1) NULL,
    `CreatedAt`         DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `UpdatedAt`         DATETIME(6)  NULL ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_Poker_Issue` (`IssueId`),
    CONSTRAINT `CK_Poker_Status` CHECK (`Status` IN ('Voting', 'Revealed', 'Finished'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Phiên chấm điểm Planning Poker trực tuyến';

CREATE TABLE `PlanningPokerVote` (
    `Id`                CHAR(36)     NOT NULL,
    `SessionId`         CHAR(36)     NOT NULL,
    `UserId`            CHAR(36)     NOT NULL,
    `VoteValue`         DECIMAL(4,1) NOT NULL COMMENT 'Quân bài Fibonacci: 1, 2, 3, 5, 8, 13, 21...',
    `VotedAt`           DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_PokerVote_User` (`SessionId`, `UserId`),
    CONSTRAINT `FK_PokerVote_Session` FOREIGN KEY (`SessionId`) REFERENCES `PlanningPokerSession`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Lá bài vote của từng thành viên trong phiên Poker';
```

### 3.5 Bảng Mới: Bảng Cải tiến Sprint Retrospective
```sql
CREATE TABLE `SprintRetrospectiveCard` (
    `Id`                CHAR(36)     NOT NULL,
    `SprintId`          CHAR(36)     NOT NULL,
    `ColumnType`        VARCHAR(20)  NOT NULL COMMENT 'Well, Wrong, Action',
    `Content`           TEXT         NOT NULL,
    `IsAnonymous`       TINYINT(1)   NOT NULL DEFAULT 0,
    `AuthorUserId`      CHAR(36)     NOT NULL,
    `VoteCount`         INT          NOT NULL DEFAULT 0,
    `ConvertedToIssueId` CHAR(36)    NULL COMMENT 'Mã Task được tạo ra từ Action item này',
    `CreatedAt`         DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_Retro_Sprint` (`SprintId`),
    CONSTRAINT `CK_Retro_Column` CHECK (`ColumnType` IN ('Well', 'Wrong', 'Action'))
) ENGINE=InnoDB ROW_FORMAT=DYNAMIC COMMENT='Thẻ ý kiến trong buổi họp Sprint Retrospective';
```

---

## 4. THIẾT KẾ BACKEND & CÁC API ĐỘNG (BACKEND & API SPECIFICATION)

### 4.1 Quản trị Động Issue Types (`ProjectCustomizationController.cs`)
* `GET /api/v1/projects/{projectId}/issue-types`
  * Trả về danh sách loại công việc của dự án (`Id`, `Name`, `IconKey`, `ColorHex`, `HierarchyLevel`, `IsSubtask`, `OrderIndex`).
* `POST /api/v1/projects/{projectId}/issue-types`
  * Quyền yêu cầu: `project.settings.manage` hoặc Project Lead.
  * Body: `{ name: string, icon: string, color: string, hierarchyLevel: number, isSubtask: boolean }`.
* `PUT /api/v1/projects/{projectId}/issue-types/{id}`
  * Cho phép đổi tên, mã màu, icon Material Symbols, thứ tự.
* `DELETE /api/v1/projects/{projectId}/issue-types/{id}`
  * Kiểm tra tính toàn vẹn: Không cho xóa nếu đang có `Issue` mang `IssueTypeId` này (trả về HTTP 409 Conflict kèm thông báo).

### 4.2 Quản trị Động Labels (`IssueLabelLinkController.cs`)
* `GET /api/v1/projects/{projectId}/labels`
  * Trả về danh sách nhãn kèm số lượng task đang gắn nhãn và mã màu Hex.
* `POST /api/v1/projects/{projectId}/labels`
  * Body: `{ name: string, colorHex: string }`.
* `PUT /api/v1/labels/{id}`: Cập nhật tên và mã màu.
* `DELETE /api/v1/labels/{id}`: Xóa nhãn khỏi dự án và gỡ bỏ liên kết khỏi các task.

### 4.3 Quản trị Động Workflow Statuses (`WorkflowController.cs`)
* `POST /api/v1/projects/{projectId}/statuses`
  * Body: `{ name: string, colorHex: string, isInitial: boolean, isDone: boolean, isActiveWork: boolean, isCanceled: boolean }`.
  * Tự động thêm cột vào bảng Kanban mặc định của dự án (`BoardColumn`).
* `PUT /api/v1/projects/{projectId}/statuses/{id}`
  * Cập nhật thông tin và các cờ hành vi của trạng thái.
* `DELETE /api/v1/projects/{projectId}/statuses/{id}`
  * Kiểm tra tính an toàn: Nếu đang có issue ở trạng thái này, từ chối xóa và yêu cầu chuyển đổi (re-map) task trước.
* `PUT /api/v1/projects/{projectId}/statuses/reorder`
  * Body: `{ orderedStatusIds: Guid[] }` — Cập nhật thứ tự hiển thị cột quy trình.

### 4.4 Kho Tài liệu Đặc tả & Bóc tách Story (`ProjectDocumentsController.cs`)
* `GET /api/v1/projects/{projectId}/documents`
* `GET /api/v1/documents/{documentId}`
* `POST /api/v1/projects/{projectId}/documents` (Chỉ BA / PO có quyền tạo).
* `PUT /api/v1/documents/{documentId}`
* `PUT /api/v1/documents/{documentId}/status`: Duyệt trạng thái (`Draft` ➔ `Review` ➔ `Approved`).
* `POST /api/v1/documents/{documentId}/comments`: Thêm inline comment kèm đoạn trích dẫn.
* `POST /api/v1/documents/{documentId}/extract-story`:
  * Tiếp nhận đoạn text bôi đen, tiêu đề Story, danh sách Acceptance Criteria.
  * Tự động tạo `Issue` (User Story) trong Product Backlog và ghi nhận liên kết vào `SpecStoryLink`.

### 4.5 Planning Poker Trực tuyến (`PlanningPokerController.cs` & `Realtime/PokerHub.cs`)
* `POST /api/v1/poker/sessions`: Tạo hoặc mở phiên chấm điểm cho một Story.
* `POST /api/v1/poker/sessions/{sessionId}/vote`: Gửi lá bài vote bí mật (Fibonacci).
* `POST /api/v1/poker/sessions/{sessionId}/reveal`: Lật bài đồng loạt cho cả phòng (SignalR).
* `POST /api/v1/poker/sessions/{sessionId}/finalize`: Chốt điểm Story Points vào User Story.

### 4.6 Báo động Điểm nghẽn (Daily Blocker Flag)
* `POST /api/v1/issues/{id}/block`:
  * Body: `{ reason: string }`
  * Đặt `IsBlocked = 1`, `BlockReason = reason`, `BlockedAt = UtcNow`.
  * Gửi thông báo thời gian thực qua SignalR đến PO, SM và Lead dự án.
* `POST /api/v1/issues/{id}/unblock`: Gỡ cờ blocked.

### 4.7 Sprint Review & PO Sign-off
* `GET /api/v1/sprints/{sprintId}/review-items`: Lấy danh sách các Story đã xong 100% Sub-tasks kèm tiêu chí AC.
* `POST /api/v1/sprints/{sprintId}/accept-story/{storyId}`:
  * PO nghiệm thu chính thức: Đánh dấu trạng thái Story là Hoàn thành (`IsDone = 1`), cộng điểm Story Points vào Velocity.
* `POST /api/v1/sprints/{sprintId}/reject-story/{storyId}`:
  * PO từ chối: Nhập lý do từ chối, chuyển Story về danh sách cần khắc phục.

### 4.8 Sprint Retrospective Board
* `GET /api/v1/sprints/{sprintId}/retro-cards`
* `POST /api/v1/sprints/{sprintId}/retro-cards`: Thêm thẻ ý kiến (hỗ trợ cờ `isAnonymous = true`).
* `POST /api/v1/retro-cards/{cardId}/vote`: Thả tim/vote cho thẻ.
* `POST /api/v1/retro-cards/{cardId}/convert-to-task`: Chuyển Action Item thành Task nằm trong Backlog/Sprint.

### 4.9 Trình thuật sĩ Đóng Sprint & Rollover (`SprintRolloverController.cs`)
* `GET /api/v1/sprints/{sprintId}/close-summary`: Báo cáo điểm cam kết vs điểm nghiệm thu thực tế và trạng thái Sprint Goal.
* `POST /api/v1/sprints/{sprintId}/complete-and-rollover`:
  * Body: `{ targetDestination: "Backlog" | "NextSprint", nextSprintId?: Guid }`.
  * Đóng Sprint hiện tại (`Status = 'Completed'`).
  * Điều hướng toàn bộ các User Story/Task chưa hoàn tất (chưa được PO Accept) về đích đã chọn.
  * Giữ nguyên điểm ước lượng của Story dở dang để tính vào Sprint mới khi hoàn tất.

---

## 5. THIẾT KẾ GIAO DIỆN NGƯỜI DÙNG (FRONTEND ANGULAR DESIGN)

### 5.1 Cài đặt Dự án Động (Project Settings Tabs)
Bổ sung và chuẩn hóa các tab cài đặt trong `project-settings-page`:
1. **Thông tin chung**: Tên, mô tả, Lead dự án.
2. **Cấu hình Quy trình Workflow (`workflow-settings-page`)**:
   * Thêm/sửa/xóa các cột quy trình động. Bảng màu trực quan, tích chọn các cờ (`IsInitial`, `IsDone`, `IsActiveWork`, `IsCanceled`).
   * Ma trận định nghĩa đường chuyển trạng thái (Transitions) giữa các cột.
3. **Cấu hình Loại Công việc (`issue-type-settings-tab`)**:
   * Danh sách loại công việc động với icon Google Material Symbols và màu sắc riêng biệt.
   * Modal tạo/sửa loại công việc: Cho phép chọn icon Material Symbols và mã màu.
4. **Quản lý Nhãn dán (`label-settings-tab`)**:
   * Danh mục nhãn dự án kèm mã màu Hex, cho phép tạo/sửa/xóa nhãn tập trung.
5. **Thành viên & Phân quyền (`project-members-page`)**:
   * Quản lý vai trò phân cấp theo chuẩn Scrum: Lead (`LeadUserId`), Scrum Master, Developer, QA, Viewer.

### 5.2 Module Kho Tài liệu Đặc tả (Spec Hub - `/projects/:id/documents`)
* Trình bày dạng 2 cột: Cột trái là cây thư mục/danh sách tài liệu đặc tả; cột phải là không gian soạn thảo/đọc tài liệu Markdown.
* Thanh trạng thái hiển thị nhãn: `Bản nháp (Draft)`, `Đang xem xét (Review)`, `Đã duyệt (Approved)`.
* Tính năng tương tác:
  * Bôi đen đoạn văn bản ➔ Xuất hiện popover nổi chứa nút **"Tạo User Story"** và **"Bình luận"**.
  * Modal bôi đen tạo Story: Trích xuất đoạn text làm mô tả, thêm các dòng Tiêu chí nghiệm thu (AC).
  * Hiển thị huy hiệu gắn mã Story bên lề văn bản đã được bóc tách.

### 5.3 Màn hình Product Backlog & Sprint Planning (`backlog-page.ts`)
* Gỡ bỏ toàn bộ mảng tĩnh `['Story', 'Task', 'Bug', 'Use-Case', 'Epic', 'Sub-task']`; nạp trực tiếp danh sách loại công việc và trạng thái từ Project Service.
* Hiển thị huy hiệu **`Ready`** tự động cho các User Story đã đạt chuẩn Definition of Ready (DoR).
* Chế độ Sprint Planning chia 2 cột:
  * Bên trái: Product Backlog (lọc nhanh các Story đã `Ready`).
  * Bên phải: Sprint chuẩn bị chạy kèm ô nhập bắt buộc `Sprint Goal` và thanh kiểm soát sức chứa kép (Capacity & Velocity Bar).
  * Nút mở phòng **Planning Poker** với bộ bài Fibonacci (1, 2, 3, 5, 8, 13, 21), hiệu ứng lật bài đồng thời.

### 5.4 Bảng Sprint Board (`board-page.ts`)
* Thanh trên cùng ghim cố định `Sprint Goal`.
* Bảng Kanban hiển thị dạng Swimlane nhóm theo User Story.
* Nút lọc nhanh 1-Click **"My Work"** theo avatar cá nhân.
* Thẻ công việc bị vướng mắc hiển thị **viền đỏ rực (red border)**, nhãn **`BLOCKED`**, lý do vướng mắc và được ghim lên đầu cột.
* Biểu đồ Burndown Chart hỗ trợ chuyển đổi linh hoạt: Số giờ còn lại (Remaining Hours) và Điểm câu chuyện (Story Points).

### 5.5 Màn hình Sprint Review & PO Sign-off
* Danh sách User Story hoàn thành 100% Sub-tasks.
* Giao diện đối chiếu: Danh sách Checkbox Acceptance Criteria song song với link tài liệu đặc tả và link demo.
* Hai nút thao tác của PO: **"Nghiệm thu (Accept)"** và **"Yêu cầu làm lại (Reject)"** kèm ô nhập lý do.

### 5.6 Bảng Sprint Retrospective (`retro-board`)
* 3 cột trực quan: *What went well*, *What went wrong*, *Action items*.
* Form nhập thẻ có công tắc **Ẩn danh (Anonymous)**.
* Nút thả tim/vote (Dot voting) trên từng thẻ.
* Nút **"Chuyển thành Task"** trên thẻ Action Item để đẩy thẳng vào Backlog.

### 5.7 Wizard Đóng Sprint (Complete Sprint Modal)
* Modal 3 bước dẫn dắt người dùng khi bấm "Complete Sprint":
  1. Tổng kết Vận tốc (Velocity) và mục tiêu Sprint.
  2. Xử lý các việc dở dang (chọn chuyển về Backlog hoặc dời sang Sprint tiếp theo).
  3. Kích hoạt và mở không gian Sprint mới.

---

## 6. TUÂN THỦ QUY CHUẨN GEMINI.MD & BẢO MẬT

1. **Quy tắc Icon Tuyệt đối (No Raw Keyboard Emojis)**:
   * Toàn bộ mã nguồn HTML, TS, SCSS, chuỗi nhãn và thông báo không được chứa bất kỳ ký tự emoji Unicode nào.
   * Mọi biểu tượng bắt buộc dùng thẻ:
     ```html
     <span class="material-symbols-outlined text-[16px]" aria-hidden="true">icon_name</span>
     ```
2. **Kiểm soát Quyền hạn Phân tầng (Scrum RBAC)**:
   * Tổ chức: Chủ tổ chức (Org Owner) có toàn quyền. Không cho phép xóa Super Admin duy nhất nếu chưa nhượng quyền.
   * Dự án: Lead dự án (`LeadUserId`) là vai trò tối cao bất biến trong dự án.
   * Scrum Master & PO: Có quyền quản lý Sprint (`sprint.create`, `sprint.start`, `sprint.close`), quản lý Backlog và duyệt nghiệm thu.
   * Developer & QA: Thao tác trên Sub-task, chuyển trạng thái board, cắm cờ Blocker, vote Planning Poker. Không có quyền sửa tài liệu Spec gốc đã duyệt, không tự tạo Sprint.
3. **Bảo toàn Dữ liệu Lịch sử (Audit Log & Soft Delete)**:
   * Khi nhân sự rời dự án: Không xóa cứng bản ghi, đánh dấu `IsActive = 0` / nhãn `Deactivated` (Cựu thành viên). Giữ nguyên toàn bộ Story Points, lịch sử kéo thả và bình luận đã đóng góp.

---

## 7. KẾ HOẠCH KIỂM THỬ & BẢO ĐẢM CHẤT LƯỢNG (QA PLAN)

1. **Backend Integration & Security Tests (xUnit)**:
   * Kiểm thử tính năng CRUD Issue Types và Labels động.
   * Kiểm thử tính năng thêm trạng thái mới và kiểm tra cờ hành vi logic (`IsDone`, `IsActiveWork`, `IsCanceled`).
   * Kiểm thử ràng buộc bảo mật: Quyền sửa tài liệu Spec Hub, quyền PO Sign-off, quyền Complete Sprint.
   * Kiểm thử tính năng Sprint Rollover: Đảm bảo Story Points dở dang không bị tính vào Sprint cũ.
2. **Frontend Unit Tests (Vitest)**:
   * Kiểm thử tính năng tải động `issueTypes` và `workflowStatuses` từ service, bảo đảm không bị gãy khi gỡ mảng hardcode.
   * Kiểm thử component Spec Hub: thao tác bôi đen tạo Story và liên kết hai chiều.
   * Kiểm thử cờ Blocker hiển thị viền đỏ và ghim đầu cột.
3. **Kiểm tra Chuẩn Mã Nguồn**:
   * `npm run lint` đạt 0 lỗi, 0 cảnh báo.
   * Quét toàn bộ repository để đảm bảo không vi phạm quy tắc Emoji của `GEMINI.md`.
