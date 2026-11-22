# HƯỚNG DẪN CẤU HÌNH & KẾT NỐI .NET MCP SERVER CHO AI SPRINT PLANNER

## 1. Giới thiệu tổng quan
`ProjectMgmt.McpServer` là một máy chủ giao thức **Model Context Protocol (MCP)** chuẩn JSON-RPC 2.0 viết bằng C# (.NET 10), giao tiếp qua luồng chuẩn **Stdio**.
Máy chủ này đóng vai trò cầu nối dữ liệu chuyên dụng giữa hệ thống quản lý dự án Scrum với các mô hình trí tuệ nhân tạo (LLM) như **Qwen 2.5**, **Claude 3.5 / 3.7**, **GPT-4o**.

```text
[ PM / PO / Scrum Master ]
          │ (Nhập ghi chú cuộc họp)
          ▼
[ LLM Engine (Qwen 2.5 / Claude) ]
          │
          ├── Gọi MCP Tools (JSON-RPC qua Stdio) ──► [ ProjectMgmt.McpServer (.NET 10) ]
          │                                                    │
          │                                    ┌───────────────┴───────────────┐
          │                                    ▼                               ▼
          │                            [ Kho Spec Hub ]            [ MySQL Database ]
          │                         (PDF, Word, Excel, Txt)       (Backlog, Stories, Sprints)
          ▼
[ Gợi ý Sub-tasks (BE/FE/QA) + Ước lượng Giờ + Gán Người ]
```

---

## 2. Danh mục Công cụ (Tools) được cung cấp

| Tên Tool | Đầu vào (Arguments) | Chức năng chi tiết |
| :--- | :--- | :--- |
| `spec_hub_list_documents` | `projectId` (string) | Liệt kê toàn bộ cây thư mục và danh mục tài liệu đặc tả (.pdf, .docx, .xlsx, .txt, .md) trong Spec Hub. |
| `spec_hub_read_document` | `documentId` (string) | Đọc và trích xuất nội dung toàn văn của tài liệu (hỗ trợ đọc sâu file Word .docx, bảng tính Excel .xlsx, PDF và Text). |
| `spec_hub_search` | `projectId` (string), `keyword` (string) | Tìm kiếm từ khóa, thuật ngữ kỹ thuật hoặc quy tắc nghiệp vụ trong toàn bộ tài liệu Spec Hub. |
| `project_get_active_config` | `projectId` (string) | Truy xuất cấu hình thực tế đang áp dụng của dự án: Quy trình Workflow (Board, các cột Statuses, cờ khởi tạo/hoàn thành, luồng chuyển tiếp), Mức ưu tiên đang dùng, Loại công việc (IssueTypes) và Nhãn (Labels). |
| `backlog_get_stories` | `projectId` (string) | Lấy danh sách toàn bộ hạng mục trong Product Backlog (Story, Task, Bug) kèm loại việc, mức ưu tiên, nhãn, Story Points, việc con sẵn có và Tiêu chí nghiệm thu (AC). |
| `sprint_get_history_and_velocity`| `projectId` (string) | Truy xuất Vận tốc (Velocity) trung bình các Sprint trước, các Story dở dang rollover và Bug kỹ thuật còn tồn đọng. |
| `team_get_dev_capacities` | `projectId` (string) | Lấy danh sách thành viên kỹ thuật, chức danh, kỹ năng và số giờ làm việc khả dụng (Capacity). |
| `ai_sprint_plan_propose` | `projectId` (string), `meetingNotes` (string), `targetStoryIds` (array) | Phân tích tóm tắt cuộc họp + Tiêu chí nghiệm thu Spec Hub để sinh cấu trúc Sub-tasks (BE, FE, QA), gán người và ước lượng giờ. |

---

## 3. Cách cấu hình kết nối MCP Server

### 3.1. Cấu hình với Claude Desktop
Thêm vào tệp `claude_desktop_config.json` (nằm tại `%APPDATA%\Claude\claude_desktop_config.json` trên Windows):

```json
{
  "mcpServers": {
    "projectmgmt-ai-planner": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\Users\\hoang\\Downloads\\ProjectMgmt\\ProjectMgmt.McpServer\\ProjectMgmt.McpServer.csproj"
      ],
      "env": {
        "ConnectionStrings__ProjectMgmt": "Server=127.0.0.1;Port=3306;Database=projectmgmt;User=YOUR_DB_USER;Password=YOUR_DATABASE_PASSWORD;SslMode=None;AllowPublicKeyRetrieval=True;",
        "PROJECTMGMT_MCP_USER_ID": "YOUR_USER_ID",
        "PROJECTMGMT_MCP_ORG_ID": "YOUR_ORG_ID",
        "PROJECTMGMT_MCP_ALLOWED_PROJECTS": "YOUR_ALLOWED_PROJECT_IDS",
        "PROJECTMGMT_API_TOKEN": "YOUR_API_TOKEN"
      }
    }
  }
}
```

### 3.2. Cấu hình với Cursor / VS Code / Roo Code
Thêm vào cài đặt MCP server của trình soạn thảo (kèm đầy đủ biến môi trường phạm vi & API token giống mục 3.1; mọi giá trị thực tế giữ tại máy cục bộ, không commit lên Git):
- Command: `dotnet`
- Arguments: `["run", "--project", "C:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.McpServer/ProjectMgmt.McpServer.csproj"]`
- Environment (`env`): `ConnectionStrings__ProjectMgmt`, `PROJECTMGMT_MCP_USER_ID`, `PROJECTMGMT_MCP_ORG_ID`, `PROJECTMGMT_MCP_ALLOWED_PROJECTS`, `PROJECTMGMT_API_TOKEN` (khi cấu hình `PROJECTMGMT_MCP_*` hoặc `PROJECTMGMT_MCP_SERVER_API_TOKEN`, server kiểm tra fail-closed và trả JSON-RPC `-32001` nếu vượt phạm vi hoặc sai token).

### 3.3. Chạy kiểm tra trực tiếp qua dòng lệnh Stdio
Tại thư mục gốc dự án:
```powershell
dotnet run --project ProjectMgmt.McpServer\ProjectMgmt.McpServer.csproj
```
Dán lệnh JSON-RPC test:
```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
```
Máy chủ sẽ phản hồi thông tin máy chủ và khả năng sẵn sàng phục vụ.
