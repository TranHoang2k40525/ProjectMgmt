Sprint Goal hiện tại: {{SPRINT_GOAL}}

Công việc hiện tại cần điều chỉnh:
- Tiêu đề: {{TASK_TITLE}}
- Loại công việc: {{TASK_TYPE}}
- Mô tả: {{TASK_DESCRIPTION}}
- Tiêu chí nghiệm thu: {{TASK_AC}}
- Điểm Story Points: {{TASK_SP}}
- Giờ ước tính: {{TASK_HOURS}}
- Người phụ trách hiện tại: {{TASK_ASSIGNEE}}

Năng lực đội ngũ dự án:
{{TEAM_CAPACITIES}}

Yêu cầu điều chỉnh từ người dùng:
"{{USER_FEEDBACK}}"

Chỉ đạo điều chỉnh cho Agent:
1. Cập nhật công việc này (Title, Description, Type, Priority, AcceptanceCriteria, StoryPoints, EstimatedHours, AssigneeName, Subtasks).
2. Tiêu đề phải là câu hành động kỹ thuật chuẩn mực, không dùng văn phong AI sáo rỗng hay copy nguyên văn ghi chú thô.
3. Điểm Story Points tuân thủ Fibonacci (1, 2, 3, 5, 8).
4. Nếu công việc có độ phức tạp cao (>= 5 SP), hãy chia các việc con (Subtasks) cụ thể theo chuyên môn (Frontend, Backend, QA) kèm số giờ và người làm phù hợp. Nếu là việc độc lập (1-3 SP), để subtasks: [].
5. Giữ nguyên hoặc cập nhật trích dẫn tài liệu thật (`documentCitations`).
6. Trả về đúng duy nhất định dạng JSON của một đối tượng Task.
