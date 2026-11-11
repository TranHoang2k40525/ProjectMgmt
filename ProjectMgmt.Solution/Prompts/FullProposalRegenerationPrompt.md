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
6. Giữ nguyên các Task không bị yêu cầu thay đổi (cùng `id`, nội dung), chỉ sửa/bỏ/thêm đúng phần người dùng yêu cầu.
7. Trả về đúng duy nhất một đối tượng JSON (không có markdown giải thích ở ngoài), khóa viết camelCase đúng như sau:
{
  "sprintGoal": "Mục tiêu Sprint sau điều chỉnh",
  "rationale": "Lý do điều chỉnh",
  "tags": ["#SprintPlan"],
  "tasks": [
    {
      "id": "task_01",
      "epicTitle": "Tên Epic",
      "title": "Tiêu đề hành động kỹ thuật",
      "description": "Mô tả chi tiết",
      "type": "Story",
      "priority": "High",
      "storyPoints": 5,
      "estimatedHours": 14,
      "assigneeName": "Họ và tên thành viên",
      "tags": ["#Backend"],
      "acceptanceCriteria": ["Tiêu chí 1", "Tiêu chí 2"],
      "documentCitations": ["TênTàiLiệu.md - Mục X"],
      "aiRationale": "Căn cứ phân công",
      "subtasks": [
        {
          "id": "task_01_1",
          "title": "Tiêu đề việc con",
          "role": "Backend",
          "estimatedHours": 6,
          "storyPoints": 2,
          "priority": "High",
          "assigneeName": "Họ và tên thành viên",
          "description": "Mô tả việc con",
          "acceptanceCriteria": ["Tiêu chí"]
        }
      ]
    }
  ]
}
