Sprint Goal hiện tại: {{SPRINT_GOAL}}

Công việc hiện tại cần điều chỉnh:
- Tiêu đề: {{TASK_TITLE}}
- Loại công việc: {{TASK_TYPE}}
- Mô tả: {{TASK_DESCRIPTION}}
- Tiêu chí nghiệm thu: {{TASK_AC}}
- Điểm Story Points: {{TASK_SP}}
- Giờ ước tính: {{TASK_HOURS}}
- Người phụ trách hiện tại: {{TASK_ASSIGNEE}}

Cấu hình ĐANG ÁP DỤNG của dự án (BẮT BUỘC tuân thủ):
- Quy trình Workflow: {{PROJECT_WORKFLOW_SUMMARY}} (Trạng thái khởi tạo mặc định: `{{PROJECT_INITIAL_STATUS}}`)
- Loại công việc (Issue Types): {{PROJECT_ISSUE_TYPES}}
- Mức độ ưu tiên (Priorities): {{PROJECT_PRIORITIES}}
- Nhãn dự án (Labels): {{PROJECT_LABELS}}

Năng lực đội ngũ dự án:
{{TEAM_CAPACITIES}}

Yêu cầu điều chỉnh từ người dùng:
"{{USER_FEEDBACK}}"

Chỉ đạo điều chỉnh cho Agent:
1. Cập nhật công việc này (Title, Description, Type, Priority, Status, AcceptanceCriteria, StoryPoints, EstimatedHours, AssigneeName, Tags, Subtasks) theo đúng cấu hình dự án ở trên.
2. Phân biệt rõ User Story (`Story` - nhu cầu người dùng) và `Task` (công việc thực thi kỹ thuật). Tiêu đề phải là câu hành động kỹ thuật chuẩn mực, không dùng văn phong AI sáo rỗng hay copy nguyên văn ghi chú thô.
3. Điểm Story Points tuân thủ Fibonacci (1, 2, 3, 5, 8).
4. Nếu công việc có độ phức tạp cao (>= 5 SP) hoặc là User Story cần triển khai nhiều tầng kỹ thuật, chia các việc con (`Subtasks`) cụ thể theo chuyên môn (Frontend, Backend, QA) kèm số giờ, mô tả, AC và người làm phù hợp. Nếu là việc độc lập (1-3 SP), để `subtasks: []`.
5. Giữ nguyên hoặc cập nhật trích dẫn tài liệu thật (`documentCitations`).
6. Trả về đúng duy nhất định dạng JSON của một đối tượng Task.
