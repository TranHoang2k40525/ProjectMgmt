# CẨM NANG TOÀN DIỆN VỀ MODULE AI AGENT & MCP SERVER
> **Hệ thống Quản lý Dự án Scrum Agile Enterprise tích hợp AI**  
> *Dành cho Kỹ sư Phát triển, Kiến trúc sư Hệ thống, Scrum Master, Giảng viên và Hội đồng Phản biện*

---

## 1. Lời nói đầu & Mục đích tài liệu

Bộ tài liệu này được biên soạn nhằm cung cấp **bức tranh toàn cảnh chi tiết từ cấp độ kiến trúc hệ thống (High-Level Architecture) đến từng dòng mã nguồn cụ thể (Low-Level Implementation)** của toàn bộ **Module AI Agent** trong hệ thống Quản lý Dự án.

Tài liệu được thiết kế theo tiêu chí **"Đọc một lần - Nắm trọn toàn bộ"**:
- **Người quản lý / Khách hàng**: Hiểu được AI mang lại giá trị gì cho Scrum Sprint, độ chính xác đến đâu và dữ liệu được bảo vệ thế nào.
- **Kỹ sư phát triển / Bảo trì**: Biết chính xác code nằm ở tệp nào, hàm nào xử lý, luồng dữ liệu đi qua đâu, cách debug và mở rộng model mới.
- **Hội đồng chấm thi / Phản biện**: Có đầy đủ chứng minh thực tế (Proof-of-Concept), sơ đồ chuẩn UML (Use Case, Sequence, Activity), cơ chế lưu trữ phân tán Redis, cơ chế RAG đa định dạng và chuẩn giao thức Model Context Protocol (MCP).

---

## 2. Bản đồ Cấu trúc Tài liệu (Documentation Index)

Bộ tài liệu được tổ chức thành 7 chuyên đề chuyên sâu, liền mạch và có liên kết chéo:

```mermaid
flowchart TD
    Index["Mục lục & Bản đồ Điều hướng"] --> D1["Chuyên đề 1: Kiến trúc Hệ thống & Sơ đồ Use Case"]
    Index --> D2["Chuyên đề 2: Sơ đồ Tuần tự & Sơ đồ Hoạt động"]
    Index --> D3["Chuyên đề 3: Cơ chế Trí nhớ Ngữ cảnh & Bộ nhớ đệm Phân tán"]
    Index --> D4["Chuyên đề 4: Tạo sinh Tăng cường Truy xuất (RAG) & Kho Đặc tả"]
    Index --> D5["Chuyên đề 5: Kiến trúc Giao thức Mở rộng & Danh mục Công cụ"]
    Index --> D6["Chuyên đề 6: Cẩm nang Tra cứu & Trả lời Phản biện Chuyên sâu"]
```

### Danh mục các chuyên đề chi tiết:

1. [**Chuyên đề 1: Kiến trúc Tổng quan & Sơ đồ Use Case**](file:///c:/Users/hoang/Downloads/ProjectMgmt/docs/ai/01-kien-truc-tong-quan-va-use-case.md)
   - Khái niệm & Bài toán cốt lõi trong Scrum Agile; nâng cấp thành **Trợ Lý AI** hội thoại có trạng thái đa tầng (**Redis + MySQL Session Persistence**).
   - Clean Architecture & Modular Monolith kết hợp Module AI; **Rào chắn Phạm vi Nghiệp vụ** (từ chối yêu cầu ngoài luồng) & **Định tuyến Ngữ cảnh Chọn lọc (Selective MCP Routing)**.
   - Sơ đồ Use Case tổng quát (Mermaid) gồm lập kế hoạch Sprint/Backlog, hỏi đáp dự án, truy xuất trí nhớ đánh chỉ mục `#mem-X`, xuất Excel qua hội thoại, đẩy Kho Backlog, **Tạo mới (không xóa phiên cũ)** và **Xóa phiên cũ có kiểm soát**.
   - Kiến trúc Đa nhà cung cấp (Multi-Provider Model Client): Gemini 3.6/3.5 Flash, Claude 3.7 Sonnet, OpenAI GPT-4o, Local Ollama.
   - Quy chuẩn Bảo mật Tuyệt đối: Zero-Secret-Leakage & Khóa chặt Phạm vi Tổ chức - Dự án - Người dùng (`OrganizationId`, `ProjectId`, `UserId`).

2. [**Chuyên đề 2: Sơ đồ Tuần tự & Sơ đồ Hoạt động**](file:///c:/Users/hoang/Downloads/ProjectMgmt/docs/ai/02-so-do-tuan-tu-va-hoat-dong.md)
   - Bộ định tuyến Ý định Hội thoại & **Selective MCP Context Router**: chỉ gọi MCP Tools khi input + ngữ cảnh thực sự cần thiết; trả lời trực tiếp câu hỏi thông thường hoặc chỉ gọi `memory_recall` cho câu hỏi nối tiếp.
   - Giao diện **Thinking Log (Nhật ký Suy nghĩ & Thao tác Agent)** tông sáng chuẩn hệ thống, **mặc định luôn đóng (collapsed)** ở mọi trạng thái (đang chạy, hoàn thành, khôi phục phiên), chỉ hiển thị chi tiết từng hành động và `#mem-X` khi người dùng chủ động mở rộng.
   - Sơ đồ Tuần tự (Sequence Diagram) & Sơ đồ Hoạt động (Activity Diagram) quy trình Agentic Workflow đa lượt gắn bền vững với MySQL & Redis.
   - Luồng truyền dữ liệu thời gian thực Server-Sent Events (SSE) kèm `sessionId` phiên hội thoại và metadata thao tác (`actionVerb`, `targetLabel`, `detailPreview`).

3. [**Chuyên đề 3: Cơ chế Trí nhớ Đa tầng, Chỉ mục Ngữ cảnh & Lưu trữ Bền vững**](file:///c:/Users/hoang/Downloads/ProjectMgmt/docs/ai/03-co-che-tri-nho-redis-memory.md)
   - Kiến trúc Lưu trữ Kép (**Redis Hot Cache + MySQL Durable Session Persistence**): khôi phục phiên cũ kể cả khi Redis hết hạn hoặc khởi động lại (**Cold-Redis DB Fallback & Re-hydration**).
   - **Phân trang Ngược Kiểu Messenger & Tự Cuộn Xuống Mới Nhất**: Khi mở lại phiên làm việc, chỉ nạp trước cửa sổ 12 lượt mới nhất (`tailLimit=12`), tự động cuộn tức thì xuống cuối đoạn chat, tải nối tiếp tin cũ hơn qua `GET /api/ai/agent/sessions/{sessionId}/messages` kèm giữ nguyên vị trí đọc và giới hạn RAM/cache trình duyệt.
   - **Hệ thống Đánh Chỉ Mục Trí Nhớ (`#mem-1`, `#mem-2`, ...)**: tách và đánh chỉ mục từng khối ngữ cảnh hội thoại để Agent trỏ thẳng vào mảnh trí nhớ liên quan thay vì nạp toàn bộ context thô.
   - **Khóa chặt Phạm vi Đa chiều (`OrganizationId`, `ProjectId`, `UserId`)**: cô lập hoàn toàn danh sách phiên, xem/tiếp tục phiên, cập nhật staging, commit và xóa phiên (`DELETE /api/ai/agent/sessions/{sessionId}`).
   - Chuyển đổi nút **"Tạo mới"** không phá hủy (chỉ khởi tạo khung hội thoại trắng trên UI, bảo toàn toàn bộ phiên làm việc cũ trong DB).

4. [**Chuyên đề 4: Cơ chế RAG & Grounding với Spec Hub**](file:///c:/Users/hoang/Downloads/ProjectMgmt/docs/ai/04-rag-va-spec-hub-grounding.md)
   - Khái niệm RAG (Retrieval-Augmented Generation) & Giải quyết triệt để Hallucination.
   - Bộ trích xuất văn bản vạn năng `AiDocumentTextExtractor` (.docx, .pdf, .xlsx, .md, .txt).
   - Vector Search, Embedding (`gemini-embedding-001`, Qdrant) và Fallback Keyword Search.
   - Trích dẫn chính xác nguồn gốc (`documentCitations`) & Ngăn kéo xem tài liệu không làm mất đoạn chat.

5. [**Chuyên đề 5: Kiến trúc MCP Server & Hệ thống MCP Tools**](file:///c:/Users/hoang/Downloads/ProjectMgmt/docs/ai/05-mcp-server-va-tools.md)
   - Khái niệm chuẩn Model Context Protocol (MCP) JSON-RPC 2.0 & **Cơ chế Gọi MCP Có Chọn Lọc (Selective MCP Invocation)**.
   - 2 hình thái MCP trong dự án: In-Process Tools vs Standalone Stdio Server.
   - Danh mục đầy đủ 8 công cụ (`spec_hub_list_documents`, `spec_hub_read_document`, `spec_hub_search`, `project_get_active_config`, `backlog_get_stories`, `sprint_get_history_and_velocity`, `team_get_dev_capacities`, `ai_sprint_plan_propose`).
   - Cấu hình kết nối cho Claude Desktop, Cursor, Roo Code, Gemini CLI.

6. [**Chuyên đề 6: Cẩm nang Tra cứu & Trả lời Phản biện (Q&A Master Guide)**](file:///c:/Users/hoang/Downloads/ProjectMgmt/docs/ai/06-huong-dan-tra-cuu-chung-minh-phan-bien.md)
   - "Cái này là gì? Làm thế nào? Nằm ở file nào?"
   - "Phiên hội thoại được lưu trữ ở đâu? Cách khôi phục khi Redis lạnh và cách khóa phạm vi Dự án - Tổ chức - Người dùng?"
   - "Tại sao Trợ Lý AI không gọi MCP tràn lan và dùng chỉ mục `#mem-X` thế nào?"
   - "Làm thế nào chứng minh hệ thống đang gọi AI thật 100% & Redis + MySQL hoạt động đồng bộ?"

---

## 3. Bảng Tra cứu Nhanh Thành phần Mã nguồn (Quick Code Locator)

| Tên Thành Phần | Vai Trò Chuyên Trách | Đường Dẫn Mã Nguồn |
| :--- | :--- | :--- |
| **AiAgentController** | API Controller phục vụ SSE stream theo `sessionId`, staging, khôi phục phiên, xóa phiên cũ & commit Sprint/Backlog | [`AiAgentController.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Controllers/AiAgentController.cs) |
| **AiSprintDecompositionService** | Trợ Lý AI hội thoại có trạng thái: phân loại ý định, rào chắn ngoài luồng, Selective MCP Routing, trace suy nghĩ & RAG | [`AiSprintDecompositionService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiSprintDecompositionService.cs) |
| **AiIndexedMemoryEngine** | Động cơ lập kế hoạch thực thi chọn lọc (Selective MCP) và truy xuất chỉ mục trí nhớ `#mem-1..#mem-n` | [`AiIndexedMemoryEngine.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiIndexedMemoryEngine.cs) |
| **AiConversationSessionStore** | Kho lưu trữ phiên hội thoại bền vững trên MySQL + khóa chặt `(OrganizationId, ProjectId, UserId)` + bù lạnh Redis | [`AiConversationSessionStore.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiConversationSessionStore.cs) |
| **AiConversationSession & MemoryEntry** | Thực thể miền lưu trữ phiên chat hoàn chỉnh và các mảnh trí nhớ đánh chỉ mục trong MySQL | [`AiConversationSession.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Modules.DeliveryIntelligence/Domain/Entities/AiConversationSession.cs) |
| **ProjectActiveConfigResolver** | Phân giải cấu hình thực tế đang áp dụng của dự án (Workflow, Priorities, IssueTypes, Labels) | [`ProjectActiveConfigResolver.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/ProjectActiveConfigResolver.cs) |
| **AiSprintCommitCoordinator** | Ghi nhận Sprint, **đẩy trực tiếp vào Kho Backlog (`SprintId = null`)**, di chuyển Backlog không trùng lặp & xử lý Rollover | [`AiSprintCommitCoordinator.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiSprintCommitCoordinator.cs) |
| **AiTokenUsageTracker** | Theo dõi tiêu thụ Token AI, thống kê chi phí & lịch sử phiên | [`AiTokenUsageTracker.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiTokenUsageTracker.cs) |
| **MultiProviderAiModelClient** | Router gọi đa mô hình (Gemini, Claude, OpenAI, Ollama) | [`MultiProviderAiModelClient.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/MultiProviderAiModelClient.cs) |
| **AiDocumentTextExtractor** | Trích xuất văn bản từ file Word, Excel, PDF, Markdown, Text | [`AiDocumentTextExtractor.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiDocumentTextExtractor.cs) |
| **StackExchangeRedisStagingProvider** | Bộ nhớ đệm phân tán Redis lưu trạng thái đề xuất staging & lịch sử hội thoại đa lượt | [`StackExchangeRedisStagingProvider.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/Adapters/StackExchangeRedisStagingProvider.cs) |
| **ResilientStateStagingService** | Cơ chế bọc chống sập bộ nhớ: ưu tiên Redis, fallback In-Memory | [`ResilientStateStagingService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Core/Staging/ResilientStateStagingService.cs) |
| **ProjectScopeGuard** | Rào chắn bảo mật cách ly dữ liệu giữa các Project & Org | [`ProjectScopeGuard.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Core/Security/ProjectScopeGuard.cs) |
| **SpecHubTools** | MCP Tool truy xuất kho tài liệu đặc tả Spec Hub | [`SpecHubTools.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/McpServers/SpecHubTools.cs) |
| **BacklogTools** | MCP Tool đọc danh sách User Story và độ ưu tiên Backlog | [`BacklogTools.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/McpServers/BacklogTools.cs) |
| **SprintTools** | MCP Tool tính toán vận tốc (Velocity) và rollover items | [`SprintTools.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/McpServers/SprintTools.cs) |
| **TeamTools** | MCP Tool thống kê kỹ năng và giờ làm việc khả dụng | [`TeamTools.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/McpServers/TeamTools.cs) |
| **ProjectMgmt.McpServer** | Ứng dụng Console Stdio JSON-RPC phục vụ Agent ngoài | [`Program.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.McpServer/Program.cs) |
| **ai-agent.service.ts** | Service Angular kết nối SSE stream (`sessionId`), áp dụng Sprint / đẩy Backlog, **xóa phiên cũ (`deleteSession`)** & 401 refresh | [`ai-agent.service.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/core/services/ai-agent.service.ts) |
| **ai-decompose-modal.ts** | Component giao diện **Trợ Lý AI** hội thoại 2 phía, **Thinking Accordion mặc định luôn đóng**, nút **Tạo mới**, khôi phục & xóa phiên cũ | [`ai-decompose-modal.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/backlog/components/ai-decompose-modal/ai-decompose-modal.ts) |
| **ai-decompose-modal.html** | Template đoạn hội thoại Trợ Lý AI (trái) & Tôi (phải) liền mạch, khối Thinking tông sáng tối giản & danh sách lịch sử có nút xóa | [`ai-decompose-modal.html`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/backlog/components/ai-decompose-modal/ai-decompose-modal.html) |
| **profile-page.html** | Dashboard quản lý mức tiêu thụ Token AI và chi phí vận hành | [`profile-page.html`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/identity/profile-page.html) |
| **spec-hub-page.ts** | Giao diện Spec Hub hỗ trợ mở đúng vị trí phân đoạn trích dẫn | [`spec-hub-page.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/spec-hub/spec-hub-page.ts) |

