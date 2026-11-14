Dữ liệu đầu vào thực tế của dự án:

### 1. Tài liệu / Biên bản cuộc họp & Lịch sử hội thoại trong phiên:
{{DOC_CONTENT}}

### 2. Danh sách tệp tài liệu thực tế trong Spec Hub của dự án (BẮT BUỘC chỉ trích dẫn từ danh sách này):
{{SPEC_HUB_FILE_NAMES}}

### 3. Trích đoạn tài liệu Đặc tả từ Spec Hub của Dự án:
{{SPEC_HUB_DOCS}}
{{SPEC_HUB_EXCERPTS}}

### 4. Cấu hình ĐANG ÁP DỤNG THỰC TẾ của dự án (trích từ cơ sở dữ liệu):
- **Workflow hiện hành ({{PROJECT_WORKFLOW_NAME}} - {{PROJECT_WORKFLOW_TYPE}})**: {{PROJECT_WORKFLOW_SUMMARY}}
  * Trạng thái khởi tạo mặc định: `{{PROJECT_INITIAL_STATUS}}`
  * Danh sách trạng thái hợp lệ: `{{PROJECT_WORKFLOW_STATUSES}}`
- **Loại công việc (Issue Types)**: {{PROJECT_ISSUE_TYPES}}
  * Loại cấp 1 được phép gán cho field `type`: `{{PROJECT_PARENT_ISSUE_TYPES}}`
- **Mức độ ưu tiên đang áp dụng (Priorities)**: {{PROJECT_PRIORITIES}}
  * Chỉ chọn `priority` trong: `{{PROJECT_PRIORITY_NAMES}}` (mặc định: `{{PROJECT_DEFAULT_PRIORITY}}`)
- **Nhãn dự án (Labels)**: {{PROJECT_LABELS}}

### 5. Yêu cầu định hướng từ Quản lý Dự án (PM / PO / Scrum Master):
{{PM_DIRECTION}}

### 6. Vận tốc, Lịch sử Sprint trước và Các hạng mục dở dang (Sprint Rollover & Overdue Tasks):
{{SPRINT_HISTORY}}

### 7. Năng lực, kỹ năng chuyên môn và số giờ khả dụng của thành viên nhóm:
{{TEAM_CAPACITIES}}

### 8. Kho Product Backlog hiện tại của dự án (User Story, Task, Bug chưa đưa vào Sprint):
{{BACKLOG_STORIES}}

CHỈ ĐẠO THỰC THI CHO AGENT:
1. Đọc kỹ biên bản họp, tài liệu đặc tả Spec Hub và kho Product Backlog ở Mục 8.
2. **Xử lý Product Backlog & phân biệt User Story vs Task**:
   - Xem xét các hạng mục trong kho Backlog liên quan tới Sprint Goal. Khi lấy một User Story hoặc Task/Bug từ kho Backlog đưa vào Sprint, BẮT BUỘC gắn `"sourceBacklogIssueId"` và `"sourceBacklogIssueKey"` để di chuyển hạng mục đó khỏi kho Backlog vào Sprint (tránh trùng lặp).
   - Phân biệt chuẩn xác `Story` (nhu cầu nghiệp vụ của người dùng -> bóc tách thành các `subtasks` kỹ thuật hoặc `Task` cụ thể) và `Task` (đầu việc kỹ thuật triển khai trực tiếp) và `Bug` (sửa lỗi).
3. **Tuân thủ tuyệt đối cấu hình áp dụng ở Mục 4**:
   - `status`: CHỈ dùng tên trạng thái nằm trong `{{PROJECT_WORKFLOW_STATUSES}}` (mặc định `{{PROJECT_INITIAL_STATUS}}`).
   - `priority`: CHỈ dùng tên mức ưu tiên nằm trong `{{PROJECT_PRIORITY_NAMES}}`.
   - `type`: CHỈ dùng tên loại công việc nằm trong `{{PROJECT_PARENT_ISSUE_TYPES}}`.
   - `tags`: Ưu tiên dùng nhãn từ `{{PROJECT_LABELS}}` khi phù hợp.
4. **Kế thừa Sprint Rollover (Mục 6)**: Kế thừa Bug tồn đọng (ưu tiên cao nhất theo cấu hình dự án) và việc đang làm dở (WIP); không tự đưa việc thường quá hạn chưa làm vào Sprint nếu không cấp thiết.
5. Định cỡ Story Points đa dạng (1, 2, 3, 5, 8) cân đối với năng lực thành viên (Mục 7), phân công đúng họ tên thành viên theo chuyên môn Backend, Frontend, QA.
6. Nếu có lịch sử hội thoại trước đó ở Mục 1, bảo toàn ngữ cảnh và cập nhật theo đúng yêu cầu nối tiếp ở Mục 5; đặt `recommendedTarget` là `"backlog"` nếu người dùng chuẩn bị việc cho kho Backlog hoặc `"sprint"` nếu lập Sprint, đồng thời viết `assistantReply` tự nhiên, rành mạch.
7. Trả về đúng duy nhất đối tượng JSON hợp lệ.
