Dữ liệu đầu vào thực tế của dự án:

### 1. Tài liệu / Biên bản cuộc họp hoạch định Sprint:
{{DOC_CONTENT}}

### 2. Danh sách tệp tài liệu thực tế trong Spec Hub của dự án (BẮT BUỘC chỉ trích dẫn từ danh sách này):
{{SPEC_HUB_FILE_NAMES}}

### 3. Trích đoạn tài liệu Đặc tả từ Spec Hub của Dự án:
{{SPEC_HUB_DOCS}}
{{SPEC_HUB_EXCERPTS}}

### 4. Quy chuẩn dự án động từ cơ sở dữ liệu:
- Trạng thái công việc theo Workflow: {{PROJECT_WORKFLOW_STATUSES}}
- Loại công việc áp dụng (Issue Types): {{PROJECT_ISSUE_TYPES}}
- Mức độ ưu tiên áp dụng (Priorities): {{PROJECT_PRIORITIES}}

### 5. Yêu cầu định hướng từ Quản lý Dự án (PM / PO / Scrum Master):
{{PM_DIRECTION}}

### 6. Vận tốc, Lịch sử Sprint trước và Các hạng mục dở dang (Sprint Rollover & Overdue Tasks):
{{SPRINT_HISTORY}}

### 7. Năng lực, kỹ năng chuyên môn và số giờ khả dụng của thành viên nhóm:
{{TEAM_CAPACITIES}}

### 8. Backlog hiện tại của dự án:
{{BACKLOG_STORIES}}

CHỈ ĐẠO THỰC THI CHO AGENT:
1. Đọc kỹ biên bản họp và tài liệu đặc tả để nắm bắt trọn vẹn mục tiêu Sprint (Sprint Goal).
2. Tự do quyết định số lượng công việc (Story, Task, Bug, Technical Debt) và số lượng việc con (Subtasks) phù hợp với quy mô thực tế, KHÔNG bị gò bó bởi bất kỳ giới hạn cứng nào.
3. Xem xét kỹ mục 6 (Các hạng mục dở dang từ Sprint trước):
   - Kế thừa (Rollover) các Bug nghiêm trọng hoặc Task đang làm dở sang Sprint mới với tag ["#Rollover"], phân công người tiếp tục xử lý.
   - Đối với các Task thường quá hạn chưa từng được bắt đầu, đánh giá hợp lý: nếu không cấp bách, không tự động đưa vào Sprint mới để tránh làm quá tải nhóm.
4. Định cỡ Story Points đa dạng theo dãy Fibonacci (1, 2, 3, 5, 8). Phân công chính xác họ tên thành viên có trong danh sách theo kỹ năng Frontend, Backend, QA.
5. Tuyệt đối không sao chép nguyên văn các tiêu đề thô hoặc ghi chú người dùng vào `title` của Task. Tiêu đề phải là câu hành động kỹ thuật chuẩn mực.
6. Trả về đúng duy nhất đối tượng JSON SprintProposal theo quy chuẩn.
