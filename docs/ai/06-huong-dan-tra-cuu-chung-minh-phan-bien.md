# CHUYÊN ĐỀ 6: CẨM NANG TRA CỨU & TRẢ LỜI PHẢN BIỆN CHUYÊN SÂU (Q&A MASTER DEFENSE GUIDE)

---

## 1. Lời nói đầu & Phương pháp luận Phản biện
Khi bảo vệ đồ án, thẩm định kiến trúc hoặc báo cáo kỹ thuật trước Hội đồng Đánh giá, các câu hỏi thường xoay quanh 5 khía cạnh cốt lõi:
1. **Bản chất lý thuyết**: *"Cái này là gì? Tại sao lại dùng công nghệ này mà không dùng công nghệ khác?"*
2. **Vị trí hiện thực**: *"Cái này nằm ở file nào? Dòng code nào xử lý?"*
3. **Cơ chế lưu trữ & Luồng dữ liệu**: *"Dữ liệu đi qua đâu? Lưu trữ ở đâu? Khi nào thì ghi vào Database?"*
4. **Bằng chứng thực nghiệm (Proof of Concept)**: *"Làm sao chứng minh hệ thống đang chạy AI thật 100% chứ không phải dữ liệu giả (mock data)?"*
5. **Độ an toàn & Khả năng chịu lỗi (Resilience & Security)**: *"Nếu Redis chết thì sao? Nếu token hết hạn thì sao? Phân quyền chống vượt cấp thế nào?"*

Tài liệu này tổng hợp toàn bộ câu hỏi và câu trả lời chuẩn xác nhất, kèm theo đường dẫn mã nguồn có thể nhấp trực tiếp để mở đúng vị trí code.

---

## 2. Nhóm Câu Hỏi 1: Tổng Quan Kiến Trúc & Vị Trí Mã Nguồn

### Câu hỏi 1.1: Trợ Lý AI trong hệ thống là gì và giải quyết bài toán gì trong Scrum?
- **Trả lời**:
  **Trợ Lý AI** trong hệ thống là một **Trợ lý Hội thoại Có Trạng thái (Stateful Hermes Agentic Assistant)** hỗ trợ toàn diện quy trình Quản lý Dự án Scrum/Agile: từ lập kế hoạch Sprint, bóc tách công việc đẩy vào Kho Product Backlog, trả lời câu hỏi về số liệu/quy chuẩn dự án, xuất tệp Excel báo cáo cho tới điều chỉnh từng hạng mục qua đối thoại tự nhiên.
  Trợ Lý AI giải quyết triệt để 4 vấn đề:
  1. Đọc và hiểu toàn bộ tài liệu đặc tả (Word, Excel, PDF, Text, Biên bản họp) qua cơ chế RAG & Spec Hub.
  2. Tự động đối chiếu năng lực thực tế của từng thành viên (Frontend, Backend, QA), vận tốc Sprint trước, Backlog và bộ Workflow/Priority/IssueType/Labels đang áp dụng của dự án.
  3. Đề xuất bản kế hoạch cân bằng cho Sprint hoặc Kho Product Backlog kèm tiêu chí nghiệm thu và trích dẫn tài liệu minh bạch 100%.
  4. Duy trì đoạn hội thoại liền mạch nhiều lượt trên Redis để người dùng tinh chỉnh nối tiếp, hỏi đáp dự án hoặc xuất báo cáo Excel bằng ngôn ngữ tự nhiên.

### Câu hỏi 1.2: Tại sao gọi đây là một "Hermes Agentic Workflow Có Trạng thái" mà không phải Chatbot Stateless hay API Wrapper?
- **Trả lời**:
  Một Chatbot Stateless hay API Wrapper chỉ nhận từng câu hỏi độc lập, quên ngay lượt trao đổi trước đó và không có khả năng tương tác nghiệp vụ an toàn.
  Ngược lại, Trợ Lý AI của hệ thống vận hành theo quy trình **Stateful Hermes Agentic Workflow**:
  - **Định tuyến Ý định & Rào chắn Ngoài luồng**: Tự động phân loại 9 nhóm ý định (`sprint_plan`, `backlog_plan`, `refine_proposal`, `project_qa`, `export_excel`, `remove_tasks`, `push_backlog`, `apply_sprint`, `out_of_scope_refusal`) và từ chối lịch sự mọi yêu cầu không thuộc phạm vi công cụ quản lý dự án.
  - **Tự chủ Công cụ MCP**: Tự động gọi chuỗi công cụ (`SpecHubTools`, `BacklogTools`, `SprintTools`, `TeamTools`, `project_get_active_config`) để thu thập dữ liệu thật trước khi trả lời.
  - **Trí nhớ Hội thoại & Staging trên Redis (`ConversationHistory`)**: Truyền `sessionId` qua từng lượt SSE (`POST /api/ai/agent/decompose/stream`), nạp lại toàn bộ lịch sử trò chuyện và bản đề xuất đang lưu để chỉnh sửa hoặc thực thi hành động (như xuất Excel, đẩy Kho Backlog) mà vẫn giữ nguyên ngữ cảnh.

### Câu hỏi 1.3: Cấu trúc mã nguồn của Module AI nằm ở đâu?
- **Trả lời**:
  Hệ thống tuân thủ Clean Architecture kết hợp Modular Monolith, phân tách rõ ràng:
  - **Tầng Điều Khiển API**: [`AiAgentController.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Controllers/AiAgentController.cs#L25-L160)
  - **Tầng Dịch Vụ AI Core Pipeline**: [`AiSprintDecompositionService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiSprintDecompositionService.cs#L45-L250)
  - **Động cơ Quản lý Prompt Động**: [`PromptTemplateService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/PromptTemplateService.cs) & [`Prompts/`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Prompts/)
  - **Tầng Router Đa Mô Hình (Multi-Provider)**: [`MultiProviderAiModelClient.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/MultiProviderAiModelClient.cs#L25-L120)
  - **Bộ Trích Xuất Tài Liệu Vạn Năng**: [`AiDocumentTextExtractor.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiDocumentTextExtractor.cs#L10-L120)
  - **Tầng Trí Nhớ Đệm Phân Tán (Redis Staging)**: [`StackExchangeRedisStagingProvider.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/Adapters/StackExchangeRedisStagingProvider.cs#L8-L115) & [`ResilientStateStagingService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Core/Staging/ResilientStateStagingService.cs#L15-L110)
  - **Hệ Thống MCP Tools Nội Bộ**: [`SpecHubTools.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/McpServers/SpecHubTools.cs), [`BacklogTools.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/McpServers/BacklogTools.cs), [`SprintTools.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/McpServers/SprintTools.cs), [`TeamTools.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/McpServers/TeamTools.cs)
  - **Máy Chủ Stdio MCP Server Độc Lập**: [`Program.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.McpServer/Program.cs#L20-L150)
  - **Giao Diện Angular Client**: [`ai-agent.service.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/core/services/ai-agent.service.ts), [`ai-decompose-modal.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/backlog/components/ai-decompose-modal/ai-decompose-modal.ts), [`ai-decompose-modal.html`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/backlog/components/ai-decompose-modal/ai-decompose-modal.html)

### Câu hỏi 1.4: Prompt của AI có bị hardcode trong code C# không? Quản lý và thay đổi như thế nào?
- **Trả lời**:
  **Tuyệt đối không hardcode prompt trong mã C#**. Toàn bộ prompt được tách thành các tệp Markdown độc lập trong [`ProjectMgmt.Solution/Prompts/`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Prompts/):
  - `SprintDecompositionSystemPrompt.md`: Thiết lập vai trò Scrum Master, luật phân rã, thang điểm Fibonacci và gán chuyên môn.
  - `SprintDecompositionUserPrompt.md`: Mẫu prompt động với các tham số `{{DOC_CONTENT}}`, `{{SPEC_HUB_DOCS}}`, `{{TEAM_CAPACITY_SUMMARY}}`.
  - `SingleTaskRegenerationPrompt.md`: Prompt làm lại 1 task riêng biệt.
  - `FullProposalRegenerationPrompt.md`: Prompt làm lại toàn bộ đề xuất.
  Dịch vụ [`PromptTemplateService`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/PromptTemplateService.cs) tự động kiểm tra `File.GetLastWriteTimeUtc(fullPath)` để **Hot-Reload ngay lập tức** khi có bất kỳ chỉnh sửa nào trên tệp Markdown mà **không cần biên dịch lại mã nguồn hay khởi động lại ứng dụng**.

### Câu hỏi 1.5: Cấu trúc việc con (Subtasks) và việc tính điểm Fibonacci, gán người làm hoạt động như thế nào?
- **Trả lời**:
  1. **Cấu trúc Việc con (Subtasks) đầy đủ**: Việc con được thiết kế như một task hoàn chỉnh (checkbox, mã phụ `QLDA-AI1.1`, tiêu đề, vai trò kỹ thuật, độ ưu tiên, SP, trạng thái, người làm, và ngăn kéo chi tiết riêng).
  2. **Dây nối Cây phân nhánh (Tree Wire Connector)**: Dưới cụm 6 chấm kéo thả (`drag_indicator`), hệ thống vẽ nhánh cây góc vuông (`└──`) dẫn xuống từng subtask giúp người dùng nhận diện thứ bậc trực quan.
  3. **Phân bổ Năng lực & Fibonacci**: Hệ thống không gán cứng 5 SP hay chỉ 1 người duy nhất; mà tự động phân tích từ khóa kỹ thuật (Frontend $\rightarrow$ Kỹ sư Frontend, Backend $\rightarrow$ Kỹ sư Backend, QA $\rightarrow$ Kỹ sư QA) và trải đều điểm Fibonacci (1, 2, 3, 5, 8) theo độ phức tạp công việc.

---

## 3. Nhóm Câu Hỏi 2: Chứng Minh AI Thật 100% vs Dữ Liệu Giả (Proof of Real AI)

### Câu hỏi 2.1: Làm thế nào chứng minh hệ thống đang gọi mô hình AI thật chứ không phải dữ liệu mock/hardcode?
- **Trả lời & Thao tác chứng minh trực tiếp**:
  1. **Bằng chứng Network Traffic**:
     - Mở trình duyệt, bật F12 -> tab **Network**.
     - Nhập yêu cầu phân rã và bấm gửi.
     - Quan sát request: `POST /api/ai/agent/decompose/stream`.
     - Phản hồi trả về dưới dạng luồng sự kiện **Server-Sent Events (`text/event-stream`)**: từng chunk dữ liệu thời gian thực được đẩy về, thanh tiến trình bước nhảy từ 1 đến 5 và hiển thị thời gian chạy của từng MCP Tool (7ms, 13ms, 37ms, 25ms).
  2. **Bằng chứng Tính Động và Sáng Tạo (Non-deterministic)**:
     - Thử nhập 2 nội dung cuộc họp khác nhau hoàn toàn:
       - Lần 1: *"Cần làm tính năng thanh toán VNPay và ví MoMo"*.
       - Lần 2: *"Cần tối ưu hiệu năng cơ sở dữ liệu và triển khai Redis Cache"*.
     - AI sinh ra 2 bộ Sprint Goal, Epics và Tasks hoàn toàn khác biệt, với các từ khóa chuyên môn kỹ thuật phù hợp với từng bài toán.
  3. **Bằng chứng Tiêu Thụ Token trên Google AI Studio Dashboard**:
     - Đăng nhập vào Google AI Studio với tài khoản cấu hình khóa API (`AIzaSy***`).
     - Xem biểu đồ Usage: số lượng API calls và Input/Output tokens tăng lên ngay lập tức sau mỗi lần phân rã.

### Câu hỏi 2.2: Tại sao AI phản hồi nhanh trong vài giây? Có phải đang dùng dữ liệu dựng sẵn không?
- **Trả lời**:
  Tốc độ nhanh là nhờ 3 tối ưu kiến trúc vượt bậc:
  1. **Mô hình Flash thế hệ mới**: Hệ thống sử dụng `gemini-3.6-flash` và `gemini-3.5-flash` của Google DeepMind. Đây là dòng mô hình được tối ưu hóa cho tốc độ xử lý siêu tốc, đạt hàng trăm tokens/giây và thời gian Time-to-First-Token (TTFT) chỉ ~150-300ms.
  2. **In-Process MCP Tools**: Các công cụ truy xuất dữ liệu (`SpecHubTools`, `BacklogTools`) chạy trực tiếp trong cùng bộ nhớ RAM của ASP.NET Core với Entity Framework Core biên dịch sẵn, nên thời gian truy vấn chỉ mất từ **7ms đến 37ms**.
  3. **Trích xuất văn bản nhị phân bằng ZipArchive**: Bộ trích xuất `AiDocumentTextExtractor` đọc trực tiếp các tệp `.docx` và `.xlsx` bằng luồng bộ nhớ (`MemoryStream`), không qua tiến trình trung gian nên xử lý tài liệu 10 trang chỉ trong vài chục mili-giây.

---

## 4. Nhóm Câu Hỏi 3: Cơ Chế Trí Nhớ & Lưu Trữ Phân Tán Redis

### Câu hỏi 3.1: Dữ liệu đề xuất phân rã và hội thoại của AI được lưu trữ ở đâu trước khi vào bảng `Issues`?
- **Trả lời**:
  Công việc đề xuất **hoàn toàn KHÔNG ghi vào bảng `Issues` của dự án** cho đến khi người dùng xác nhận áp dụng hoặc đẩy Backlog. Trạng thái đề xuất staging (`ProposalJson`), lịch sử trò chuyện đa lượt, nhật ký `TraceItems` và các mảnh trí nhớ `#mem-1..#mem-n` được lưu song song theo cơ chế kép: **Redis Hot Cache (`ProjectMgmt:Ai:staging:{sessionId}`, TTL 24 giờ)** để truy xuất tức thời và **MySQL (`AiConversationSessions` + `AiConversationMemoryEntries`)** để lưu bền vững theo phạm vi `(OrganizationId, ProjectId, UserId)`.
  - Chỉ khi Scrum Master hoặc thành viên có quyền bấm nút **"Áp dụng vào Sprint"** hoặc **"Đẩy vào Kho Backlog"**, API mới đọc từ Redis (hoặc tự động nạp bù từ MySQL nếu Redis hết hạn), chạy bước **Pre-Commit Revalidation** và mở Database Transaction ghi chính thức vào các bảng nghiệp vụ MySQL (`Issues`, `Sprints`, `AcceptanceCriteria`, `SpecStoryLinks`, `IssueLabels`, `IssueRequiredSkills`).

### Câu hỏi 3.2: Làm thế nào chứng minh dữ liệu đề xuất đang nằm ở Staging Redis & Bảng Phiên AI chứ chưa tạo rác trong bảng `Issues`?
- **Trả lời & Câu lệnh kiểm tra thực tế**:
  1. Khi AI vừa phân rã xong trên giao diện, mở terminal chạy câu lệnh:
     ```bash
     redis-cli -h 127.0.0.1 -p 6379
     127.0.0.1:6379> KEYS ProjectMgmt:Ai:staging:*
     # Xuất hiện 1 key: ProjectMgmt:Ai:staging:e7c4f1a2-...
     127.0.0.1:6379> TTL ProjectMgmt:Ai:staging:e7c4f1a2-...
     # Kết quả: 86395 (còn ~24 giờ)
     ```
  2. Mở MySQL Workbench hoặc HeidiSQL kiểm tra **2 nhóm bảng tách biệt**:
     ```sql
     -- A. Bảng lịch sử & trí nhớ AI ĐÃ lưu phiên hội thoại ngay:
     SELECT Id, SessionId, TurnCount, MemoryCount FROM AiConversationSessions ORDER BY UpdatedAtUtc DESC LIMIT 5;
     -- B. Bảng công việc nghiệp vụ VẪN LÀ 0 cho tới khi người dùng bấm Áp dụng / Đẩy Backlog:
     SELECT COUNT(*) FROM Issues WHERE IsAiGenerated = 1;
     -- Số lượng bản ghi mới từ phiên đang xem vẫn là 0!
     ```
  3. Sau đó, trên giao diện bấm nút **"Áp dụng vào Sprint"** hoặc **"Đẩy vào Kho Backlog"**:
     - Kiểm tra lại bảng nghiệp vụ `Issues` trong MySQL: xuất hiện đầy đủ Task/Sub-task, `AcceptanceCriteria`, `SpecStoryLinks`, `IssueLabels` và `IssueRequiredSkills`.
     - Kiểm tra lại Redis CLI (`ProjectMgmt:Ai:staging:e7c4f1a2-...`): trường `tasks` đề xuất đã được dọn sạch (`tasks: []`) để chống áp dụng trùng lặp (`409 ALREADY_COMMITTED`), trong khi `conversationHistory` vẫn được giữ lại kèm tin nhắn xác nhận để người dùng tiếp tục trò chuyện; chỉ khi bấm xóa phiên hoặc hết TTL/clear-cache khóa staging mới bị xóa (`nil`).

### Câu hỏi 3.3: Nếu máy chủ Redis bị sập hoặc mất mạng, hệ thống có bị lỗi không?
- **Trả lời**:
  Hệ thống **không bao giờ sập** nhờ mẫu thiết kế **Circuit Breaker & Resilient Fallback** được lập trình trong [`ResilientStateStagingService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Core/Staging/ResilientStateStagingService.cs#L65-L90):
  - Khi Redis gặp sự cố (timeout quá 2000ms hoặc connection refused), Circuit Breaker chuyển sang trạng thái Open.
  - Hệ thống tự động chuyển hướng (fallback) sang bộ nhớ RAM cục bộ [`MemoryStateStagingService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Core/Staging/MemoryStateStagingService.cs#L10-L60) dựa trên `ConcurrentDictionary`.
  - Người dùng cuối hoàn toàn không nhận thấy bất kỳ thông báo lỗi nào (Zero Downtime / Seamless Fallback).

---

## 5. Nhóm Câu Hỏi 4: RAG Grounding & Trải Nghiệm Spec Hub

### Câu hỏi 4.1: Tại sao hệ thống có thể đọc được file Word (.docx) và Excel (.xlsx) mà máy chủ không cần cài Microsoft Office?
- **Trả lời**:
  Định dạng tệp Word (`.docx`) và Excel (`.xlsx`) thực chất là các gói nén chuẩn **Open Packaging Conventions (OPC / OpenXML)**, bản chất là một tệp nén ZIP chứa các tệp XML bên trong:
  - Lớp [`AiDocumentTextExtractor.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiDocumentTextExtractor.cs#L75-L120) dùng `System.IO.Compression.ZipArchive` để giải nén trực tiếp trong bộ nhớ.
  - Sử dụng LINQ to XML (`System.Xml.Linq.XDocument`) để duyệt cây DOM của `word/document.xml` và `xl/sharedStrings.xml`.
  - Nhờ vậy, code chạy đa nền tảng (Cross-platform) hoàn toàn trên cả Linux Docker Container và Windows Server mà không cần cài bộ ứng dụng Office nặng nề hay trả phí bản quyền.

### Câu hỏi 4.2: Khi người dùng bấm vào xem tài liệu trích dẫn, làm sao để không làm mất đoạn hội thoại chat với AI?
- **Trả lời**:
  Trên giao diện Angular ([`ai-decompose-modal.html`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/backlog/components/ai-decompose-modal/ai-decompose-modal.html#L380-L450)), tài liệu được mở qua một **Ngăn kéo nội tuyến (Inline Drawer)** trượt từ cạnh phải màn hình:
  - Bảng hội thoại và các task đề xuất ở bên trái vẫn giữ nguyên trạng thái 100%.
  - Ngăn kéo bên phải hiển thị nội dung tài liệu trích dẫn kèm nút **"Quay lại đoạn chat"**.
  - Khi bấm quay lại, drawer đóng lại và người dùng tiếp tục làm việc bình thường mà không cần tải lại trang.

---

## 6. Nhóm Câu Hỏi 5: Chuẩn Giao Thức MCP (Model Context Protocol)

### Câu hỏi 6.1: Tại sao lại xây dựng thêm dự án Console `ProjectMgmt.McpServer` bên cạnh Web API?
- **Trả lời**:
  Dự án `ProjectMgmt.McpServer` được xây dựng để biến hệ thống Quản lý Dự án thành một **MCP Provider công nghiệp chuẩn quốc tế**:
  - Giao tiếp qua giao thức luồng chuẩn **Standard I/O (stdin/stdout)** bằng các thông điệp JSON-RPC 2.0.
  - Giúp các công cụ AI lập trình hàng đầu hiện nay như **Claude Desktop, Cursor IDE, Roo Code, Gemini CLI, Antigravity** có thể cắm trực tiếp vào hệ thống quản lý dự án, tra cứu tài liệu và lập kế hoạch mà không cần mở trình duyệt web.

### Câu hỏi 6.2: Có thể chứng minh MCP Server hoạt động bằng lệnh dòng lệnh không?
- **Trả lời**:
  Hoàn toàn có thể. Mở terminal, chạy `dotnet run --project ProjectMgmt.McpServer`:
  - Gửi: `{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"Tester","version":"1.0"}}}`
  - Nhận: Server phản hồi JSON `ProjectMgmt-AiSprintPlanner-McpServer` phiên bản `1.0.0`.
  - Gửi: `{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}`
  - Nhận: Server trả về đầy đủ **8 công cụ** (`spec_hub_list_documents`, `spec_hub_read_document`, `spec_hub_search`, `project_get_active_config`, `backlog_get_stories`, `sprint_get_history_and_velocity`, `team_get_dev_capacities`, `ai_sprint_plan_propose`) kèm `inputSchema` chi tiết.

---

## 7. Nhóm Câu Hỏi 6: Phân Quyền Bảo Mật & Chống Rò Rỉ Bí Mật (RBAC & Zero-Secret-Leakage)

### Câu hỏi 7.1: Làm thế nào ngăn chặn một thành viên dự án này gọi AI truy xuất tài liệu của dự án khác?
- **Trả lời**:
  Hệ thống trang bị rào chắn bảo mật [`ProjectScopeGuard.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Core/Security/ProjectScopeGuard.cs#L15-L60):
  - Mọi request đều được giải mã JWT Claim để lấy `UserId` và `OrganizationId`.
  - Trước khi thực thi bất kỳ MCP Tool nào, `ProjectScopeGuard` kiểm tra xem người dùng có quyền thành viên trong `ProjectId` được yêu cầu không.
  - Nếu cố tình đổi `ProjectId` sang dự án khác, hệ thống ném ngoại lệ `UnauthorizedAccessException` ngay tại tầng API Controller.

### Câu hỏi 7.2: Phân quyền Scrum 5 cấp bảo vệ các hành động AI và Giao diện Dự án như thế nào?
- **Trả lời**:
  Hệ thống thực thi nhất quán cả giao diện (Angular UI đồng bộ từ `myPermissions` của workspace trên `AppShell`, `Backlog`, `Board`, `ContextMenu`, `TaskDetailDrawer` và `projectManagerGuard`) lẫn API Backend (`AiAgentController`, `WorkItemsController` + `ProjectAccess`) theo phân cấp Scrum:
  1. **Chủ tổ chức (Org Owner), Giám đốc dự án (`LeadUserId`), Project Manager & Scrum Master**: Toàn quyền cấu hình, sinh đề xuất, tinh chỉnh, **Đẩy vào Kho Backlog**, **Áp dụng trực tiếp vào Sprint** (`sprint.create` / `project.update`) và xóa mềm có xác nhận (`issue.delete`). Vai trò cấp `Organization` (`OrgOwner`) chỉ cấp riêng cho `Owner`/`Admin`/Lead dự án, tuyệt đối không cấp cho thành viên `Member` thông thường để chống leo thang đặc quyền chéo.
  2. **Developer & QA Tester (`issue.create` / `issue.update`)**: Được quyền mở Trợ Lý AI, trò chuyện phân tách, tách Sub-task nhanh và **Đẩy công việc vào Kho Product Backlog** (`PushToBacklog = true`), nhưng **bị chặn `403 FORBIDDEN_SPRINT_COMMIT`** nếu cố gọi API áp dụng thẳng vào Sprint; bị ẩn nút xóa/quản trị Sprint trên toàn bộ Backlog/Board/Context Menu/Drawer (và khóa chọn Assignee nếu thiếu `issue.assign`); khi yêu cầu AI xóa task thiếu `issue.delete`, hệ thống **từ chối trung thực không phát thẻ ảo** với task ở Backlog hoặc **chỉ cấp thao tác `Gỡ khỏi Sprint (Về Backlog)`** với task đang nằm trong Sprint.
  3. **Viewer / Khách hàng (`project.read`)**: Chỉ xem dữ liệu dự án, xuất Excel và `Sao chép mã & liên kết` — toàn bộ nút `Trợ Lý AI`, tạo/sửa/xóa/kéo thả/chuyển trạng thái/bình luận/cài đặt dự án đều bị ẩn hoặc khóa trên UI, bị chuyển hướng khỏi `/settings`, và bị chặn `403` trên mọi API ghi (`POST/DELETE issues`, `quick-action`, `apply`, `DELETE /sessions`, `/regenerate-*`, `PUT /proposal`; sự kiện `FORBIDDEN` trên `/decompose/stream`).

### Câu hỏi 7.3: Dự án bảo vệ các khóa bí mật (Gemini API Key, Redis Password, JWT Key) ra sao?
- **Trả lời**:
  Hệ thống tuân thủ nghiêm ngặt **Quy chuẩn Bảo mật Không rò rỉ Bí mật (Strict Zero-Secret-Leakage)**:
  - Toàn bộ secret thực tế ở môi trường phát triển được lưu trong `ProjectMgmt.Solution/appsettings.Local.json` và `.env` (đã nằm trong `.gitignore`, không bao giờ được commit lên Git).
  - Tệp công khai trên Git `appsettings.json` chỉ chứa placeholder an toàn (`YOUR_GEMINI_API_KEY`, `YOUR_REDIS_PASSWORD`).
  - Trong log và phản hồi giao diện, mọi khóa đều được chủ động che giấu (masking, ví dụ: `AIzaSy***`).

### Câu hỏi 7.4: Hệ thống đối phó ra sao với Prompt Injection, yêu cầu lập trình hộ/ngoài luồng ngụy trang, bẫy từ đồng âm, dẫn dắt sai sự thật và dữ liệu API bị làm giả (tampering)?
- **Trả lời & Kết quả Kiểm thử E2E Thực tế**:
  1. **Rào chắn Default-Deny & Chống Prompt Injection / XSS (`InputGuardrail` + `AiIntentClassifier`)**: Tự động phát hiện mẫu ghi đè chỉ thị (*"Ignore all previous instructions"*, *"Giả vờ bạn là admin tối cao"*), đòi đọc secret/`.env`/`appsettings.Local.json` (kể cả khi xen từ khóa giữa tên key như `Gemini API key`), đọc dữ liệu xuyên tổ chức/dự án khác, SQLi/XSS/Path Traversal ở cả luồng SSE lẫn các endpoint tinh chỉnh (`/regenerate-task`, `/regenerate-all` trả `400 SECURITY_GUARDRAIL_BLOCKED`), tiêu tốn `0` token LLM (`security_refusal`).
  2. **Từ chối Lập trình hộ & Bẫy Từ đồng âm Kể cả Khi Phiên Đang Mở Đề xuất (`out_of_scope_refusal`)**: Khắc phục triệt để cơ chế fallback cũ; yêu cầu viết mã nguồn trực tiếp ngay trong phiên đang có task đề xuất (như *"code cho tôi"*, *"viết code TypeScript cho task đầu tiên"*, *"tạo class Go kết nối Redis cache giúp tôi"*), thơ/giải trí ngụy trang chữ *"sprint"*/*""công việc"*, hoặc bẫy từ đồng âm đời thường (*"chạy sprint 100m"*, *"hàng backlog quần áo tồn kho"*, *"tại sao Trái Đất tự quay..."* chứa cụm *"của nó"*) đều bị `AiIntentClassifier` từ chối ở tầng 0 với `0` lệnh gọi LLM mà không làm mất bảng đề xuất hay lịch sử phiên. Ngược lại, các yêu cầu lập kế hoạch phần mềm hợp lệ cho domain đặc thù (*"Lập kế hoạch Sprint cho phân hệ đặt vé máy bay trên website du lịch"*, *"Tạo danh sách công việc rà soát bảo mật..."*, *"Thống kê năng lực... vào backlog"*) vẫn được định tuyến chính xác tuyệt đối.
  3. **Chứng minh Kiểm thử Hồi quy 1.000 Hành vi, Gỡ/Xóa Công việc, Tác vụ Nhanh AI Có Xác nhận & E2E Thực tế Chrome**: Bộ kiểm thử phạm vi [`AiScopeGuardrail1000BehaviorsTests.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Tests/Services/AiScopeGuardrail1000BehaviorsTests.cs) gồm **500 hành vi ngoài phạm vi** và **500 hành vi trong phạm vi** (`1.006` test case), bộ kiểm thử gỡ/xóa công việc [`AiTaskRemovalTests.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Tests/Services/AiTaskRemovalTests.cs) (**`19/19` PASSED**) cùng bộ kiểm thử tác vụ nhanh AI [`AiTaskQuickActionTests.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Tests/Services/AiTaskQuickActionTests.cs) (**`6/6` PASSED**) đều chạy xanh trong đợt kiểm thử toàn solution đạt **`2.510/2.510` PASSED (0 Failed)** (`24` ca MySQL env-gated bị skip theo cấu hình chuẩn, tổng `2.534`), kết hợp kiểm chứng E2E trực tiếp trên trình duyệt Chrome qua Playwright trên tổ chức HUCE / dự án QLDA qua đủ vai trò (`PM`, `Scrum Master`, `Developer`, `QA`, `Viewer` đạt `39/39` assertion).
  4. **Chống Dẫn dắt Sai lệch (Anti-Leading Grounding)**:
     - Trong Q&A: Khi người dùng khẳng định sai sự thật (*"Giám đốc đã xóa toàn bộ Priority/Workflow"*), AI đối chiếu MCP thực tế và phản bác chính xác.
     - Khi phân tách Sprint/Backlog: Nếu prompt đưa thông số lệch chuẩn (SP `100`, trạng thái/mức ưu tiên/thành viên hoặc tên tài liệu Spec Hub bịa đặt), `AiSprintDecompositionService` tự động ép lại thang Fibonacci (`1..13`) và lọc chỉ giữ các thực thể có thật từ `ProjectActiveConfigResolver`, `TeamTools` và `SpecHubTools`.
  5. **Chống Tamper Payload & Khóa Idempotency**: Từ chối `400 INVALID_TARGET_SPRINT` khi chèn `TargetSprintId` khác dự án, từ chối tệp `.exe` / `0 byte` ở UI và khóa áp dụng trùng lặp nhiều lần bằng `409 ALREADY_COMMITTED`.

### Câu hỏi 7.5: Hệ thống áp dụng 8 lớp gia cố an toàn kỹ thuật (Hardening A1–A8) nào cho Module AI & MCP Server?
- **Trả lời**:
  1. **A1 — Pre-Commit Revalidation chống Stale State (`AiSprintCommitCoordinator.PreCommitRevalidateAsync`)**: Ngay trước khi mở Transaction ghi MySQL từ bản Staging (TTL 24h), hệ thống nạp lại `ProjectActiveConfigResolver` (tự ánh xạ status/priority cũ không còn hợp lệ về mặc định dự án), bỏ gán (`AssigneeId = null`) nếu thành viên đã rời dự án, tạo mới nếu Issue nguồn Backlog đã xóa mềm (`IsDeleted`), và bỏ qua (`SkippedTasks`/`SkippedIssueIds`) kèm cảnh báo nếu Issue nguồn đã chuyển sang Sprint khác (đồng thời ngăn bước Rollover tự động di chuyển nhầm issue đã skip).
  2. **A2 — Phòng chống Indirect Prompt Injection (`InputGuardrail.SanitizeDocumentContent`)**: Quét và vô hiệu hóa chỉ thị ẩn/thẻ giả mạo hệ thống (`<system>`, `[SYSTEM OVERRIDE]`, `[Cảnh báo bảo mật...]`) và secret ngay trong `AiDocumentTextExtractor` và `SpecHubTools.ReadDocumentAsync`; đồng thời cô lập dữ liệu đầu vào bằng cặp thẻ `<untrusted_document_context>` / `<untrusted_spechub_context>` và Quy tắc #9 trong System Prompt.
  3. **A3 — Chống Zip Bomb & Tràn bộ nhớ RAM (OOM)**: Giới hạn giải nén OpenXML (`.docx`, `.xlsx`) tối đa `1.000` entries, `25 MB`/entry (`MaxSingleEntryUncompressedBytes`), `50 MB` tổng giải nén (`MaxTotalUncompressedBytes`), `500.000` ký tự văn bản (`MaxExtractedTextLength`) và tắt DTD (`DtdProcessing.Prohibit`, `XmlResolver = null`) trên cả API chính lẫn `ProjectMgmt.McpServer`.
  4. **A4 — Cảnh báo Tài liệu PDF Quét Ảnh / Tài liệu Có Ảnh Nhúng**: Phát hiện PDF scan chứa `/Subtype /Image` ít chữ (`ScannedPdfMarker`) và `.docx` chứa ảnh trong `word/media/*`, lan truyền cảnh báo qua `ExtractDocumentWarnings` vào `proposal.Warnings` và phần `Lưu ý hệ thống` trong `assistantReply`.
  5. **A5 — Cửa sổ Trượt 8 lượt & Fallback Ngữ nghĩa `#mem-latest` (`AiIndexedMemoryEngine`)**: Khi câu hỏi tiếp nối dùng đại từ (`nó`, `phần vừa rồi`, `việc đó`) không khớp từ khóa tĩnh, tự động fallback về 1–2 mảnh trí nhớ mới nhất (`#mem-latest`) cùng cửa sổ trượt 8 lượt hội thoại gần nhất.
  6. **A6 — Bảo mật Phạm vi & API Token cho Standalone MCP Server (`McpScopeAuthContext`)**: Hỗ trợ khóa phạm vi 3 chiều (`PROJECTMGMT_MCP_USER_ID`, `PROJECTMGMT_MCP_ORG_ID`, `PROJECTMGMT_MCP_ALLOWED_PROJECTS`) và xác thực token thời gian hằng (`PROJECTMGMT_API_TOKEN` so khớp `Ai:McpServer:ApiToken` / `PROJECTMGMT_MCP_SERVER_API_TOKEN`), trả JSON-RPC `-32001` theo nguyên tắc fail-closed.
  7. **A7 — Tự Phục Hồi JSON Bị Cắt Cụt (`TryRepairTruncatedJson`)**: Vá chuỗi/ngoặc và cứu các hạng mục `tasks` hoàn chỉnh khi phản hồi LLM bị cắt giữa chừng ở giới hạn token, kèm thông báo rõ trong `proposal.Warnings`.
  8. **A8 — Kiểm Soát Tần Suất & Hạn Mức Token Trước Thực Thi (`AiTokenUsageTracker.CheckPreExecutionQuota`)**: Chặn ngay tại `AiAgentController` trước khi gọi LLM theo cấu hình `AiQuotaOptions` (mặc định `12` lượt/phút/người dùng, `1.500.000` token/ngày/người dùng, `5.000.000` token/ngày/dự án), trả `RATE_LIMIT_EXCEEDED` / `QUOTA_EXCEEDED` (HTTP `429` hoặc SSE `event: error`).

---

## 8. Nhóm Câu Hỏi 7: Quản Trị Chi Phí, Lịch Sử Phiên & Trải Nghiệm Giao Diện Mới

### Câu hỏi 8.1: Làm thế nào để theo dõi và kiểm soát chi phí cũng như số lượng Token AI đã tiêu thụ?
- **Trả lời & Thao tác chứng minh**:
  1. Hệ thống tích hợp dịch vụ [`AiTokenUsageTracker.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiTokenUsageTracker.cs) đo lường chính xác lượng Token đầu vào (Prompt Tokens) và đầu ra (Completion Tokens) cho từng phiên làm việc.
  2. Người dùng truy cập **Quản lý cá nhân (`/profile`) $\rightarrow$ Tab "Mức tiêu thụ Token AI"**.
  3. Giao diện hiển thị:
     - 4 thẻ KPI: Tổng Token, Cuộc gọi AI, Tỷ lệ thành công, Chi phí ước tính (USD).
     - Thanh phân bổ theo mô hình: Tỷ lệ sử dụng `gemini-3.6-flash`, `gemini-3.5-flash`, `gemini-3-flash-preview`.
     - Bảng nhật ký 15 lần thực thi gần nhất với thời gian xử lý và mã phiên.
  4. API cung cấp: `GET /api/ai/agent/token-usage?projectId=...` bảo mật theo người dùng và dự án.

### Câu hỏi 8.2: Làm thế nào để lưu trữ, xem lại, tiếp tục hoặc xóa các phiên làm việc cũ mà không bị nhầm giữa các dự án/tổ chức?
- **Trả lời & Thao tác chứng minh**:
  1. **Lưu trữ đích thực cuộc hội thoại trong MySQL + Redis (`AiConversationSessionStore`)**: Mỗi cuộc hội thoại hoàn chỉnh (kèm lịch sử chat nhiều lượt, bản đề xuất, log suy nghĩ `TraceItems` và các mảnh trí nhớ `#mem-1..#mem-n`) được lưu vào bảng `AiConversationSessions` và `AiConversationMemoryEntries` trong MySQL song song với Redis Hot Cache.
  2. **Khóa chặt 3 chiều (`OrganizationId`, `ProjectId`, `UserId`)**: Mọi endpoint (`GET /api/ai/agent/sessions`, `GET /api/ai/agent/proposal/{sessionId}`, `GET /api/ai/agent/sessions/{sessionId}/messages`, `POST /api/ai/agent/decompose/stream`, `DELETE /api/ai/agent/sessions/{sessionId}`) đều đối chiếu đồng thời cả 3 định danh để không bao giờ lẫn phiên giữa hai dự án, hai tổ chức hay hai người dùng khác nhau.
  3. **Tự Cuộn Xuống Mới Nhất, Phân Trang Ngược Kiểu Messenger & Nút "Tạo mới"**: Bấm nút **"Tạo mới"** trên thanh tiêu đề chỉ mở đoạn chat trống mới trên giao diện mà không gọi `clear-cache` xóa lịch sử cũ. Khi mở lại phiên cũ từ popover Lịch sử (`history`), kể cả khi Redis đã hết hạn hoặc restart, hệ thống nạp trước 12 tin nhắn cuối cùng (`tailLimit=12`) từ MySQL/Redis và **tự động cuộn tức thì xuống tin nhắn mới nhất**. Khi người dùng cuộn lên đỉnh hoặc bấm **"Tải thêm tin nhắn cũ hơn"**, `GET /api/ai/agent/sessions/{sessionId}/messages?projectId=...&beforeIndex=...&limit=12` tải từng trang tin cũ và bảo toàn nguyên vị trí đọc (scroll anchor), đồng thời chỉ giữ cửa sổ gọn nhẹ trong DOM và `localStorage` để không bao giờ tràn RAM; người dùng cũng có thể bấm biểu tượng thùng rác để xóa hẳn phiên cũ (`DELETE /api/ai/agent/sessions/{sessionId}?projectId=...`).

### Câu hỏi 8.3: Tại sao trước đây có hiện tượng xả code JSON thô vào ô tư duy (CoT) và hệ thống đã khắc phục thế nào?
- **Trả lời**:
  - **Nguyên nhân trước đây**: Ở phiên bản cũ, sự kiện stream gọi `await onThoughtDelta(chunk.DeltaContent)` cho mọi chunk nội dung, khiến các đoạn JSON cấu trúc bị đẩy trực tiếp vào luồng suy luận.
  - **Khắc phục triệt để**: Trong [`AiSprintDecompositionService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiSprintDecompositionService.cs), hệ thống tách bạch hoàn toàn:
    - `ChunkType == AiStreamChunkType.Thought`: Đẩy vào `onThoughtDelta` để người dùng thấy suy nghĩ tự nhiên.
    - `ChunkType == AiStreamChunkType.Content`: Chỉ gom vào bộ đệm JSON nội bộ để phân tích cú pháp, không đẩy vào ô tư duy.
    - Nếu mô hình không hỗ trợ luồng suy nghĩ độc lập, hệ thống phát luồng suy luận tổng hợp (Synthesized Thought Milestones) bằng tiếng Việt mạch lạc.

### Câu hỏi 8.4: Làm thế nào để AI tuân thủ đúng quy chuẩn Workflow, mức ưu tiên, loại công việc, nhãn đang áp dụng của từng dự án và lấy việc từ kho Backlog vào Sprint mà không bị trùng lặp?
- **Trả lời**:
  1. **Đọc cấu hình thực tế đang áp dụng (`ProjectActiveConfigResolver` & `project_get_active_config`)**:
     - **Workflow**: Đọc bảng mặc định (`Boards`), thứ tự cột (`BoardColumns`), trạng thái (`WorkflowStatuses` kèm cờ `IsInitial`, `IsActiveWork`, `IsDone`, `IsCanceled`) và luồng chuyển tiếp (`WorkflowTransitions`). Kể cả khi dự án áp dụng quy trình tùy chỉnh không theo mẫu chung (ví dụ: `Open -> In Dev -> QA Testing -> Released`), AI và hệ thống tự động nhận diện trạng thái khởi tạo là `Open` và trạng thái hoàn tất là `Released`, tuyệt đối không đoán mò hay gán cứng `"To Do"`.
     - **Mức độ ưu tiên (`Priorities`)**: Nếu dự án có bộ mức ưu tiên riêng (`ProjectId == projectId`), hệ thống **chỉ lấy bộ mức ưu tiên đang áp dụng của dự án đó**, không trộn lẫn với các dòng mặc định toàn cục (`ProjectId == null`). Khi lưu vào Sprint, hệ thống giữ nguyên mức ưu tiên của dự án (như `Urgent`, `Blocker`).
     - **Loại công việc (`IssueTypes`) & Nhãn (`Labels`)**: Tách rõ `Story` (nhu cầu người dùng cần bóc tách thành các đầu việc kỹ thuật), `Task` (đầu việc kỹ thuật thực thi), `Bug` và `Sub-task`, đồng thời nạp danh mục `Labels` của dự án và lưu liên kết `IssueLabels`.
  2. **Di chuyển hạng mục từ kho Backlog vào Sprint không trùng lặp (`AiSprintCommitCoordinator`)**:
     - Khi AI chọn hạng mục từ Product Backlog (`sourceBacklogIssueId` / `sourceBacklogIssueKey`), bộ điều phối thực hiện **di chuyển trực tiếp (Move)** bản ghi từ kho Backlog (`SprintId = null`) sang `TargetSprintId` (kéo theo các việc con sẵn có), tuyệt đối không tạo bản ghi mới trùng lặp trong kho Backlog.

### Câu hỏi 8.5: Trợ Lý AI đẩy công việc trực tiếp vào Kho Product Backlog, xuất file Excel qua đoạn chat và chặn yêu cầu ngoài luồng như thế nào?
- **Trả lời**:
  1. **Đẩy trực tiếp vào Kho Product Backlog (`PushToBacklog = true`)**:
     - Khi người dùng yêu cầu phân tách đưa vào Kho Backlog (`recommendedTarget = "backlog"`), bấm nút **"Đẩy vào Kho Backlog"**, hoặc nhắn lệnh trực tiếp trong chat (`triggerAction = "push_backlog"`), API `POST /api/ai/agent/apply` (chỉ yêu cầu quyền `issue.create`) chuyển qua `AiSprintCommitCoordinator.ApplySprintProposalAsync` tạo Epics/Tasks/Subtasks với `SprintId = null` mà không sinh Sprint mới, đồng thời xóa danh sách việc vừa đẩy khỏi Redis và bổ sung tin nhắn xác nhận vào `ConversationHistory` để đoạn chat vẫn liền mạch.
  2. **Xuất tệp Excel đúng ngữ cảnh ngay trong hội thoại (`triggerAction = "export_excel"` & `ExportSpec`)**:
     - Khi người dùng yêu cầu xuất Excel trong đoạn hội thoại (tách biệt hoàn toàn với yêu cầu *"tạo task xuất excel"*, câu hỏi *"làm sao để xuất"*, yêu cầu xuất định dạng khác như PDF/Word, hay chữ `"excel"` nằm bên trong tài liệu SRS đính kèm dài), backend gọi [`AiExcelExportResolver`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiExcelExportResolver.cs) đối chiếu siêu dữ liệu thực tế từ `BacklogTools`/`TeamTools`: phân giải chuẩn xác phạm vi (`single_sprint`, `sprint_list`, `backlog_only`, `single_task`, `task_detail`, `filtered_tasks`, `ai_proposal`, `project_all`) hoặc đại từ quy chiếu (*"task đó"*, *"sprint đó"*, *"nội dung trên"* — tự động nhận diện Sprint hoặc toàn bộ đề xuất thay vì khóa nhầm một `IssueKey` đơn lẻ, đồng thời hỗ trợ lệnh nối tiếp đặt tên file riêng như *"xuất và đặt tên là tài liệu"* -> `tài liệu.xlsx`). Nếu đối tượng không tồn tại hoặc bộ lọc rỗng, hệ thống **từ chối xuất khống** (`TriggerAction = "none"`); ngược lại gắn `ExportSpec` riêng vào lượt hội thoại và Angular [`ExcelDataService`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/core/services/excel-data.service.ts) tự động sinh tệp `.xlsx` đúng phạm vi kèm nút tải lại cho riêng tin nhắn đó.
  3. **Rào chắn Phạm vi Ngoài luồng (`out_of_scope_refusal`) & Giao diện Hội thoại 2 phía**:
     - Mọi yêu cầu không liên quan đến công cụ quản lý dự án đều được `ClassifyConversationalIntent` chặn lại với phản hồi lịch sự mà không phá hỏng danh sách công việc đang chuẩn bị trong Redis.
     - Trên giao diện [`ai-decompose-modal.html`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/backlog/components/ai-decompose-modal/ai-decompose-modal.html) & [`ai-decompose-modal.scss`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/backlog/components/ai-decompose-modal/ai-decompose-modal.scss), khung trả lời của **Trợ Lý AI** đặt bên trái (loại bỏ hoàn toàn đường viền màu cạnh trái và các thẻ lồng ghép đứt đoạn), tin nhắn của **Tôi** đặt bên phải, tích hợp thống nhất bảng công việc đề xuất ngay dưới phản hồi.

### Câu hỏi 8.6: Giao diện Thinking hoạt động ra sao và làm sao Agent chỉ gọi MCP khi cần thiết kết hợp chỉ mục `#mem-X`, đảm bảo chính xác tuyệt đối giữa trí nhớ và dữ liệu thật?
- **Trả lời**:
  1. **Thinking Accordion tông sáng, mặc định luôn đóng (`isThinkingExpanded = signal(false)`)**:
     - Khi đang suy nghĩ: hiển thị spinner + đồng hồ đếm giây (`Đang suy nghĩ · X.Xs`).
     - Khi hoàn thành: hiển thị biểu tượng xác nhận xanh ngọc (`check_circle`) + tóm tắt (`Đã suy nghĩ xong trong X.Xs · N thao tác`).
     - Mặc định luôn đóng ở mọi trạng thái (streaming, hoàn tất, khôi phục từ DB). Chỉ khi người dùng nhấp vào thanh tiêu đề mới xổ ra từng bước thao tác chi tiết (`actionVerb`, `target`, `summary`, `durationMs`) và các chip `#mem-X` đã nạp.
  2. **Selective MCP Routing & Cơ chế Chính xác Tuyệt đối Giữa Trí Nhớ và Dữ Liệu Thật (`AiIndexedMemoryEngine`)**:
     - Trước khi gọi MCP Tools, `PlanContextExecution` phân tích câu hỏi + lịch sử phiên: chào hỏi/hỏi khả năng $\rightarrow$ `0` MCP calls; câu hỏi về chính đề xuất đang staging, đại từ nối tiếp của lượt hỏi đáp thuần túy, hoặc nhắc `#mem-X` hợp lệ $\rightarrow$ dùng trí nhớ phiên (`UseMemoryOnly = true`); mọi câu hỏi tra cứu ngắn hay nêu thực thể thật của dự án (như *"nội dung sprint 3"*, *"phân tích task có tên là ..."*, *"ai là người làm ..."*) được `ClassifyConversationalIntent` định tuyến vào `project_qa` và **bắt buộc** đối chiếu trực tiếp từ DB (`SprintTools` trả về đủ `AllSprints` gồm cả `Future`/`Active`/`Completed` kèm `AssigneeName`; `BacklogTools.GetProjectWorkItemsDigestAsync` tra cứu toàn bộ Sprint + Backlog). Đồng thời `SelectRelevantMemories` lọc bỏ các mảnh trí nhớ xung đột số Sprint/mã task (ví dụ Sprint 2 không được phép nhiễm sang câu trả lời Sprint 3).
  3. **Bám sát Mạch Hội thoại Đa lượt Thực thụ & Làm sạch Tiền tố Nội bộ (`BuildNativeConversationMessages`, `StripLeakedMetaPrefix`)**:
     - Cả luồng Q&A và luồng lập/tinh chỉnh đề xuất đều gửi chuỗi `AiMessage` luân phiên thực thụ (`User` $\leftrightarrow$ `Assistant` $\leftrightarrow$ `User`) cùng danh mục **Ràng buộc & Thỏa thuận xuyên suốt phiên**, cửa sổ 8 lượt hội thoại gần nhất và chi tiết công việc đang staging; mọi tiền tố kỹ thuật như `[Intent: ...]` được `StripLeakedMetaPrefix` tự động làm sạch khỏi lịch sử và câu trả lời. Đồng thời bộ phân loại hiểu đúng câu hỏi nối tiếp/tra cứu không dấu `?` mà vẫn chặn triệt để yêu cầu ngoài luồng và giữ nguyên liên kết `sourceBacklogIssueId` qua các lần chỉnh sửa.

### Câu hỏi 8.7: Khi áp dụng đề xuất từ Trợ Lý AI vào Sprint/Backlog, toàn bộ thông tin chi tiết của công việc cha và việc con được đồng bộ và hiển thị thế nào?
- **Trả lời**:
  1. **Ghi nhận Đầy đủ Toàn bộ Trường AI sang CSDL Chính (`AiSprintCommitCoordinator`)**:
     - Khi người dùng bấm **Áp dụng vào Sprint** hoặc **Đẩy vào Kho Backlog**, `AiSprintCommitCoordinator` tự động lưu toàn bộ dữ liệu đề xuất cho cả công việc cha lẫn từng việc con (`Sub-task`): phân giải người phụ trách không phân biệt dấu/ký tự vai trò (`ResolveAssigneeId`), gắn nhãn dự án + nhãn vai trò việc con (`IssueLabels`), gán kỹ năng yêu cầu (`IssueRequiredSkills`), tạo các mục tiêu chí nghiệm thu (`AcceptanceCriteria`), gắn liên kết tài liệu Spec Hub (`SpecStoryLinks` — việc con tự động thừa hưởng từ cha nếu chưa tách riêng), ghi metadata AI (`AiRationale`, `SubtaskRole`, `DocumentCitations`, `SuggestedSkills`) vào `ActivityLog` JSON từng issue, và giữ nguyên Sprint Goal hiện hữu nếu form gửi lên bỏ trống.
  2. **Đồng bộ Trực tiếp lên Giao diện Backlog & Ngăn kéo Chi tiết Công việc cha/Việc con**:
     - API `WorkItemsController` trả về đầy đủ `specLinks`, `requiredSkills`, `documentCitations`, `subtaskRole`, `aiRationale`, cùng chuẩn hóa mức ưu tiên/trạng thái; ngay khi commit xong, [`backlog-page.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/backlog/backlog-page.ts) gọi `loadWorkspace` + `loadProjectLabels` để làm mới dữ liệu ngay lập tức.
     - [`TaskDetailDrawerComponent`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/shared/components/task-detail-drawer/task-detail-drawer.ts) và [`IssueDetailPageComponent`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/issue-detail/issue-detail-page.ts) nâng cấp ngang hàng với Modal AI cho cả **công việc cha** và **việc con**: hiển thị huy hiệu `subtaskRole`, nút quay về việc cha / nhấn mở từng việc con, quản lý thêm/sửa/xóa Tiêu chí nghiệm thu (AC), gán/gỡ Thẻ nhãn, Kỹ năng yêu cầu, liên kết Spec Hub khử trùng lặp, chọn Sprint, sửa Story Points (`min=0`) và thẻ **Lý do AI đề xuất**.
  3. **Tinh gọn Giao diện Bên ngoài, Giữ Đầy đủ Trong Chi tiết**:
     - Bảng danh sách đề xuất AI lược bỏ các thẻ trích dẫn xanh trên từng dòng cha/con; tiêu đề Sprint ngoài Backlog lược bỏ mục tiêu Sprint/nhãn trạng thái/bộ đếm việc trùng lặp (do đã có đầy đủ trong modal **Chi tiết Sprint**); toàn bộ không gian ngoài được thu gọn hợp lý để ưu tiên nội dung công việc.

### Câu hỏi 8.8: Trợ Lý AI và Giao diện Hệ thống xử lý xuất Excel đa tình huống, định dạng bảng chuẩn ProjectMgmt (không nhãn công cụ ngoài) như thế nào?
- **Trả lời**:
  1. **Lắng nghe & Phân Giải Đúng Phạm Vi Yêu Cầu (`AiExcelExportResolver`)**:
     - Hệ thống không xuất tràn lan toàn bộ dự án khi người dùng chỉ yêu cầu một phần dữ liệu. Bộ phân giải xử lý 8 phạm vi rõ ràng: `single_sprint` (ví dụ *"xuất Sprint 3"*, *"sprint đang chạy"*, *"sprint đã hoàn thành"*), `sprint_list` (nhiều Sprint), `backlog_only` (*"xuất mỗi kho Backlog"*), `single_task` (*"chỉ xuất 1 task QLDA-5"*), `task_detail` (*"xuất nội dung chi tiết của task QLDA-5"* gồm Thông tin chung, Mô tả, Tiêu chí nghiệm thu AC, Việc con, Trích dẫn Spec & Bình luận — tự động gọi API `getIssue` lấy dữ liệu mới nhất khi xuất từ chat), `filtered_tasks` (theo người phụ trách, trạng thái, ưu tiên, loại việc, `IsBlocked`, Epic), `ai_proposal` (bảng nháp AI hoặc 1 task thứ tự trong đề xuất), và `project_all`.
     - Khi người dùng nêu Sprint, mã công việc hoặc tên thành viên không có thật, hoặc bộ lọc hẹp trả về `0` công việc, Agent **từ chối xuất khống (`TriggerAction = "none"`)** và phản hồi trung thực kèm gợi ý phạm vi hiện có.
     - Đặc tả `ExportSpec` được lưu riêng theo từng `AiConversationTurnDto` trong Redis và MySQL, giúp các nút tải lại Excel trên lịch sử hội thoại nhiều lượt luôn giữ đúng phạm vi tại thời điểm lượt đó.
  2. **Định Dạng Bảng Sạch Chuẩn ProjectMgmt & Bảng Chú Thích Trạng Thái Động (`ExcelDataService`)**:
     - Xóa bỏ 100% nhãn công cụ ngoài (tuyệt đối không dùng `"Jira"` hay `"Bkav eTask & Jira List"` ở tên file, tiêu đề sheet, bộ lọc UI) và loại bỏ hoàn toàn các banner tối màu (`#0F172A`), nền xanh/tím đậm kiểu AI.
     - Mọi bảng `.xlsx` sinh trực tiếp bằng OpenXML theo phong cách doanh nghiệp tối giản: nền trắng viền xám nhạt (`#CBD5E1`), tiêu đề cột `#F1F5F9`, cố định dòng tiêu đề + AutoFilter, **chỉ tô màu pastel tự pha kết hợp chữ đậm tương phản tại cột Trạng thái** theo đúng `ProjectStatus.colorHex` đang cấu hình trên quy trình dự án (cộng màu cảnh báo nhạt `#FEE2E2` khi `IsBlocked`), đồng thời tự động sinh bảng **CHÚ THÍCH TRẠNG THÁI & KÝ HIỆU (Legend)** ngay cuối mỗi sheet.
     - Các nút xuất trực tiếp ngoài AI (Backlog tổng/lựa chọn nhiều dòng/đang lọc, từng Sprint cụ thể, Kho Backlog, Danh sách công việc đã lọc, Ngăn kéo Chi tiết công việc) đều chia sẻ cùng bộ sinh OpenXML sạch chuẩn ProjectMgmt này.

### Câu hỏi 9.8: Khi người dùng nhắn *"gỡ các việc mà bạn áp dụng cho tôi"*, gỡ task khỏi Sprint về Backlog hoặc xóa task thật qua Trợ Lý AI, hệ thống xử lý và kiểm soát an toàn như thế nào?
- **Trả lời**:
  - **Không bao giờ tự ý sửa hay xóa DB ngay khi chat (`Zero Silent Mutation`)**: `AiIntentClassifier` phân loại chính xác ý định `remove_tasks` (tách biệt rạch ròi với xóa dòng trong bảng nháp `refine_proposal`, hỏi đáp `project_qa` như *"cách xóa task"*, hay yêu cầu phá hoại ngoài luồng), sau đó [`AiTaskRemovalResolver`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiTaskRemovalResolver.cs) đối chiếu siêu dữ liệu thực tế từ `BacklogTools.GetExportMetadataAsync` và `GetAiCommittedOriginSnapshotAsync` với `0` lệnh gọi LLM.
  - **Nhận diện thông minh ngữ cảnh gỡ/xóa & bảo vệ Sprint đã hoàn thành**:
    1. **Hoàn tác / Gỡ đợt vừa áp dụng bởi AI**: Theo dõi danh sách `CommittedIssues` (phân biệt rõ việc `MovedFromBacklog` với việc AI tạo mới) cả trong Redis phiên hiện tại lẫn dấu vết `ActivityLog` / `AiGenerated` gần nhất của dự án.
    2. **Gỡ theo Mã công việc (`QLDA-18`, `QLDA-19`), tên trong ngoặc kép, Sprint cụ thể, Kho Backlog, Thành viên hoặc Trạng thái**: Tự động lọc và từ chối rõ ràng nếu công việc nằm trong Sprint `Completed` (*"Công việc ... đang nằm trong Sprint đã hoàn thành nên không thể gỡ hoặc xóa."*).
  - **Thẻ Xác Nhận Nội Tuyến Bắt Buộc & Phản Hồi Trung Thực Theo Ngữ Cảnh RBAC (`TriggerAction = "confirm_remove_tasks"` & `POST /api/ai/agent/remove-tasks/confirm`)**: Giao diện [`ai-decompose-modal.html`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/backlog/components/ai-decompose-modal/ai-decompose-modal.html) hiển thị thẻ `ai-removal-confirm-card` cho phép bỏ chọn từng công việc và chọn đúng thao tác theo quyền thực tế (kiểm soát qua `CanDelete` / `CanDetachToBacklog`):
    - Nếu người dùng thiếu quyền `issue.delete` và toàn bộ công việc mục tiêu nằm ở **Kho Backlog** (không thuộc Sprint nên không thể gỡ về Backlog), `AiAgentController` chủ động xóa `PendingRemovalPlan` rỗng hành động và **từ chối trung thực** ngay trong tin nhắn (*"Bạn không có quyền xóa vĩnh viễn công việc (issue.delete)..."*), tuyệt đối không hiển thị thẻ xác nhận ảo hay câu dẫn *"ở thẻ bên dưới"*.
    - Nếu công việc nằm trong Sprint và người dùng chỉ có quyền gỡ (`CanDetachToBacklog = true, CanDelete = false`), thẻ chỉ mở nút **Gỡ khỏi Sprint (Về Backlog)** và câu trả lời khớp chính xác thao tác được phép (tuyệt đối không hứa nút *Xác nhận Xóa*).
    - **`detach_to_backlog` (*Gỡ khỏi Sprint về Backlog*)**: Yêu cầu `LeadUserId` hoặc một trong các quyền `issue.update`, `sprint.create`, `project.update`; đưa `SprintId = null` cho cả việc cha lẫn việc con (giữ nguyên toàn bộ task).
    - **`soft_delete` (*Xác nhận Xóa*)**: Yêu cầu `LeadUserId` hoặc `issue.delete`; xóa mềm đệ quy (`IsDeleted = true`) cả việc con và dọn liên kết Epic không làm gãy dữ liệu.
    - **`cancel` (*Hủy bỏ*)**: Đánh dấu kế hoạch đã hủy mà không chạm vào DB.
  - **Chống Tampering, Cross-User & Thực thi Trùng (`Idempotency`)**: Endpoint `POST /api/ai/agent/remove-tasks/confirm` khóa chặt phạm vi 3 chiều `(OrgId, ProjectId, UserId)` (`403` nếu sai user/thiếu quyền RBAC), từ chối `400 INVALID_ISSUE_SELECTION` nếu cố gửi `IssueId` ngoài danh sách `PendingRemovalPlan.Candidates`, trả `409 PLAN_ALREADY_PROCESSED` nếu gọi lại cùng `PlanId` sau khi đã thực thi/hủy, đồng thời cập nhật đồng bộ Redis + MySQL và phát `dataChanged` để bảng Backlog/Sprint tải lại ngay lập tức (kiểm chứng `19/19 PASSED` trong [`AiTaskRemovalTests.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Tests/Services/AiTaskRemovalTests.cs)).

### Câu hỏi 9.9: Khi người dùng bấm menu chuột phải / menu `...` chọn *"Phân rã Sub-task bằng AI"* hoặc *"Đề xuất phân công bằng AI"* (chạy ngầm không cần mở khung chat), hệ thống xử lý và kiểm soát dữ liệu ra sao?
- **Trả lời**:
  - **Kích hoạt Trợ Lý AI Ngầm & Tái sử dụng Ngữ cảnh MCP Thật**: Khi người dùng chọn `ai-breakdown` (*Phân rã Sub-task bằng AI*) hoặc `ai-assign` (*Đề xuất phân công bằng AI*) trên [`backlog-page.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/backlog/backlog-page.ts), [`board-page.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/board/board-page.ts) hay [`context-menu.component.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/shared/components/context-menu.component.ts), [`AiQuickActionService`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/core/services/ai-quick-action.service.ts) gọi SSE tới `POST /api/ai/agent/quick-action/stream` (hoặc JSON `POST /api/ai/agent/quick-action/run`). Backend [`AiTaskQuickActionService`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiTaskQuickActionService.cs) đọc cấu hình dự án (`ProjectActiveConfigResolver`), năng lực thành viên (`TeamTools.GetDevCapacitiesAsync`) và tài liệu đặc tả liên quan (`SpecHubTools.SearchAsync`), chuẩn hóa phân loại vai trò (`NormalizeRole` dùng biểu thức chính quy ranh giới từ `\b` để không khớp nhầm chuỗi con như `"ui"` trong `"Circuit"` thành `"Frontend"`), kiểm tra `InputGuardrail`, theo dõi token qua `IAiTokenUsageTracker` và gọi `IAiModelClient.GenerateAsync`.
  - **Chỉ Lưu Tạm Trên Redis — Tuyệt đối Không Sửa DB Khi Chưa Xác Nhận**: Mọi bản đề xuất (`AiQuickActionPlanDto`) chỉ được lưu trên Redis qua `IStateStagingService` với khóa `ai_quick_{Guid:N}` (TTL 2 giờ). Giao diện toàn cục [`AiQuickActionTrayComponent`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/shared/components/ai-quick-action-tray/ai-quick-action-tray.component.ts) gắn trong [`app-shell.html`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/layouts/app-shell/app-shell.html) hiển thị khay xác nhận cho phép tích chọn/sửa tiêu đề từng Sub-task hoặc chọn ứng viên được xếp hạng rồi mới bấm **Xác nhận áp dụng** hoặc **Hủy bỏ**.
  - **Kiểm soát RBAC, Phân Giải Thành Viên Chính Xác & Ghi Nhật ký Phản hồi AI**: Endpoint `POST /api/ai/agent/quick-action/confirm` gọi `AiSprintCommitCoordinator.ExecuteQuickActionCommitAsync`, khóa chặt phạm vi 3 chiều `(OrgId, ProjectId, UserId)` (`403` nếu cross-user hoặc thiếu quyền: cần `isLead || allowUpdate` để tách Sub-task; cần `isLead || allowAssign || (isSelfAssign && allowUpdate)` để phân công), nạp đầy đủ thành viên hợp lệ của dự án từ cả `RoleDto.Members` lẫn `RoleDto.Items` (`LoadEligibleProjectUsersAsync` + `ExtractProjectMemberIds`) để bảo toàn `AssigneeId` cho mọi thành viên không phải Lead (Frontend, Backend, QA) sau khi reload, từ chối phân rã Sub-task của Sub-task hoặc chỉnh sửa task trong Sprint `Completed`, đồng thời chống thực thi trùng `409 PLAN_ALREADY_PROCESSED`. Khi xác nhận thành công, hệ thống ghi thực thể thật (`Issue`, `AcceptanceCriteria`, `IssueRequiredSkills`, `SpecStoryLinks`, `ActivityLog(Source="AI")`) hoặc bảng theo dõi phân công AI (`AiAssignmentRun`, `AiAssignmentCandidate`, `AiAssignmentDecision` `Accepted`/`Overridden`, `IssueAssignmentHistory` `AiSuggested`) và phát tín hiệu làm mới Backlog/Board tức thì (kiểm chứng `6/6 PASSED` trong [`AiTaskQuickActionTests.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Tests/Services/AiTaskQuickActionTests.cs)).

---

## 9. Bảng Tổng Hợp Tham Chiếu Mã Nguồn Toàn Bộ Module AI

| Nhóm Nghiệp Vụ | Tên Lớp / Tệp Mã Nguồn | Đường Dẫn Mã Nguồn Cụ Thể |
| :--- | :--- | :--- |
| **API Endpoints & SSE** | `AiAgentController` | [`AiAgentController.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Controllers/AiAgentController.cs) |
| **Agent Pipeline & Selective Routing** | `AiSprintDecompositionService` | [`AiSprintDecompositionService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiSprintDecompositionService.cs) |
| **Bộ Phân Loại Default-Deny & Guardrail** | `AiIntentClassifier` & `InputGuardrail` | [`AiIntentClassifier.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiIntentClassifier.cs) |
| **Kiểm Thử Hồi Quy 1.000 Hành Vi Phạm Vi** | `AiScopeGuardrail1000BehaviorsTests` | [`AiScopeGuardrail1000BehaviorsTests.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Tests/Services/AiScopeGuardrail1000BehaviorsTests.cs) |
| **Phân Giải Gỡ / Xóa Công Việc (Có Xác Nhận)** | `AiTaskRemovalResolver` & `AiTaskRemovalDtos` | [`AiTaskRemovalResolver.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiTaskRemovalResolver.cs) |
| **Kiểm Thử Gỡ / Xóa & Bảo Mật RBAC AI** | `AiTaskRemovalTests` | [`AiTaskRemovalTests.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Tests/Services/AiTaskRemovalTests.cs) |
| **Tác Vụ Nhanh AI (Phân Rã & Phân Công Ngầm)** | `AiTaskQuickActionService` & `AiQuickActionDtos` | [`AiTaskQuickActionService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiTaskQuickActionService.cs) |
| **Kiểm Thử Tác Vụ Nhanh AI (Redis Staging)** | `AiTaskQuickActionTests` | [`AiTaskQuickActionTests.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Tests/Services/AiTaskQuickActionTests.cs) |
| **Phân Giải Phạm Vi Xuất Excel** | `AiExcelExportResolver` | [`AiExcelExportResolver.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiExcelExportResolver.cs) |
| **Chỉ Mục Trí Nhớ & Selective MCP** | `AiIndexedMemoryEngine` | [`AiIndexedMemoryEngine.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiIndexedMemoryEngine.cs) |
| **Kho Phiên Hội Thoại MySQL (3D Scope)** | `AiConversationSessionStore` | [`AiConversationSessionStore.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiConversationSessionStore.cs) |
| **Thực Thể Phiên & Mảnh Nhớ DB** | `AiConversationSession` & `AiConversationMemoryEntry` | [`AiConversationSession.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Modules.DeliveryIntelligence/Domain/Entities/AiConversationSession.cs) |
| **Phân Giải Cấu Hình Dự Án** | `ProjectActiveConfigResolver` | [`ProjectActiveConfigResolver.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/ProjectActiveConfigResolver.cs) |
| **Ghi Nhận Sprint & Di Chuyển Backlog** | `AiSprintCommitCoordinator` | [`AiSprintCommitCoordinator.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiSprintCommitCoordinator.cs) |
| **Theo Dõi Token & Phiên** | `AiTokenUsageTracker` | [`AiTokenUsageTracker.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiTokenUsageTracker.cs) |
| **Hợp Đồng Đo Lường Token** | `IAiTokenUsageTracker` | [`IAiTokenUsageTracker.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/IAiTokenUsageTracker.cs) |
| **Quản Lý Prompt Động** | `PromptTemplateService` | [`PromptTemplateService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/PromptTemplateService.cs) |
| **Thư Mục Prompt Markdown** | `Prompts/*.md` | [`Prompts/`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Prompts/) |
| **Router Đa Mô Hình AI** | `MultiProviderAiModelClient` | [`MultiProviderAiModelClient.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/MultiProviderAiModelClient.cs) |
| **Bộ Trích Xuất Tài Liệu** | `AiDocumentTextExtractor` | [`AiDocumentTextExtractor.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiDocumentTextExtractor.cs) |
| **Trí Nhớ Staging Redis** | `StackExchangeRedisStagingProvider` | [`StackExchangeRedisStagingProvider.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/Adapters/StackExchangeRedisStagingProvider.cs) |
| **Cơ Chế Bọc Chống Sập** | `ResilientStateStagingService` | [`ResilientStateStagingService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Core/Staging/ResilientStateStagingService.cs) |
| **Bảo Vệ Ngắt Mạch** | `CircuitBreakerPolicy` | [`CircuitBreakerPolicy.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Core/Resilience/CircuitBreakerPolicy.cs) |
| **Bảo Mật Dự Án** | `ProjectScopeGuard` | [`ProjectScopeGuard.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Core/Security/ProjectScopeGuard.cs) |
| **MCP Spec Hub** | `SpecHubTools` | [`SpecHubTools.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/McpServers/SpecHubTools.cs) |
| **MCP Backlog** | `BacklogTools` | [`BacklogTools.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/McpServers/BacklogTools.cs) |
| **MCP Sprint Velocity** | `SprintTools` | [`SprintTools.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/McpServers/SprintTools.cs) |
| **MCP Team Capacity** | `TeamTools` | [`TeamTools.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/McpServers/TeamTools.cs) |
| **Standalone MCP Server** | `ProjectMgmt.McpServer` | [`Program.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.McpServer/Program.cs) |
| **Angular SSE Client** | `AiAgentService` | [`ai-agent.service.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/core/services/ai-agent.service.ts) |
| **Angular Modal Component** | `AiDecomposeModalComponent` | [`ai-decompose-modal.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/backlog/components/ai-decompose-modal/ai-decompose-modal.ts) |
| **Angular Excel OpenXML Engine** | `ExcelDataService` | [`excel-data.service.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/core/services/excel-data.service.ts) |
| **Angular Modal Template** | `AiDecomposeModal Template` | [`ai-decompose-modal.html`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/backlog/components/ai-decompose-modal/ai-decompose-modal.html) |
| **Dashboard Token Profile** | `ProfilePageComponent` | [`profile-page.html`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/identity/profile-page.html) |
| **API Issue & AI Metadata** | `WorkItemsController` | [`WorkItemsController.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Controllers/WorkItemsController.cs) |
| **Ngăn Kéo Chi Tiết Công Việc** | `TaskDetailDrawerComponent` | [`task-detail-drawer.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/shared/components/task-detail-drawer/task-detail-drawer.ts) |
| **Trang Chi Tiết Công Việc** | `IssueDetailPageComponent` | [`issue-detail-page.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/issue-detail/issue-detail-page.ts) |
| **Tiện Ích Trích Dẫn Spec Hub** | `SpecCitationUtil` | [`spec-citation.util.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/core/utils/spec-citation.util.ts) |
| **Deep Link Spec Hub** | `SpecHubPageComponent` | [`spec-hub-page.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/spec-hub/spec-hub-page.ts) |
| **Store Tác Vụ Nhanh AI Ngầm** | `AiQuickActionService` | [`ai-quick-action.service.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/core/services/ai-quick-action.service.ts) |
| **Khay Xác Nhận Tác Vụ Nhanh Toàn Cục** | `AiQuickActionTrayComponent` | [`ai-quick-action-tray.component.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/shared/components/ai-quick-action-tray/ai-quick-action-tray.component.ts) |

