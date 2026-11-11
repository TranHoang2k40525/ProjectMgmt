Đề xuất Sprint hiện tại:
- Sprint Goal: {{SPRINT_GOAL}}
- Danh sách Tasks hiện tại:
{{CURRENT_TASKS_JSON}}

Năng lực và kỹ năng thành viên đội ngũ:
{{TEAM_CAPACITIES}}

Yêu cầu làm lại / điều chỉnh toàn bộ từ người dùng:
"{{USER_FEEDBACK}}"

Chỉ đạo tái cấu trúc cho Agent:
1. Tái cấu trúc Sprint Goal và danh sách Tasks/Subtasks theo đúng yêu cầu phản hồi từ người dùng.
2. Không giới hạn số lượng Task hay Subtask; bóc tách linh hoạt theo đúng phạm vi kỹ thuật cần thiết.
3. Hỗ trợ đa dạng loại công việc (Story, Task, Bug, Technical Debt, Improvement) theo ngữ cảnh.
4. Story Points tuân thủ dãy Fibonacci (1, 2, 3, 5, 8), phân công đúng chuyên môn kỹ thuật (Frontend, Backend, QA) và cân bằng tải.
5. Giữ vững trích dẫn tài liệu thật (documentCitations) từ các tệp Spec Hub của dự án.
6. Trả về đúng duy nhất đối tượng JSON SprintProposal theo quy chuẩn (không có markdown giải thích ở ngoài).
