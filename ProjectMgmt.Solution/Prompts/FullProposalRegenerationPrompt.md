Đề xuất Sprint hiện tại:
- Sprint Goal: {{SPRINT_GOAL}}
- Danh sách Tasks hiện tại:
{{CURRENT_TASKS_JSON}}

Cấu hình ĐANG ÁP DỤNG của dự án (BẮT BUỘC tuân thủ):
- Quy trình Workflow: {{PROJECT_WORKFLOW_SUMMARY}} (Trạng thái khởi tạo: `{{PROJECT_INITIAL_STATUS}}`)
- Loại công việc (Issue Types): {{PROJECT_ISSUE_TYPES}} (phân biệt `Story` nhu cầu người dùng vs `Task` thực thi kỹ thuật vs `Bug`)
- Mức độ ưu tiên (Priorities): {{PROJECT_PRIORITIES}}
- Nhãn dự án (Labels): {{PROJECT_LABELS}}

Năng lực và kỹ năng thành viên đội ngũ:
{{TEAM_CAPACITIES}}

Yêu cầu làm lại / điều chỉnh toàn bộ từ người dùng:
"{{USER_FEEDBACK}}"

Chỉ đạo tái cấu trúc cho Agent:
1. Tái cấu trúc Sprint Goal và danh sách Tasks/Subtasks theo đúng yêu cầu phản hồi từ người dùng. Giữ nguyên `sourceBacklogIssueId` và `sourceBacklogIssueKey` cho các hạng mục có nguồn gốc từ Product Backlog nếu vẫn được chọn đưa vào Sprint.
2. Không giới hạn số lượng Task hay Subtask; bóc tách linh hoạt theo đúng phạm vi kỹ thuật cần thiết.
3. Chỉ dùng đúng `status`, `priority`, `type` và ưu tiên nhãn từ cấu hình dự án ở trên.
4. Story Points tuân thủ dãy Fibonacci (1, 2, 3, 5, 8), phân công đúng chuyên môn kỹ thuật (Frontend, Backend, QA) và cân bằng tải.
5. Giữ vững trích dẫn tài liệu thật (`documentCitations`) từ các tệp Spec Hub của dự án.
6. Trả về đúng duy nhất đối tượng JSON SprintProposal theo quy chuẩn (không có markdown giải thích ở ngoài).
