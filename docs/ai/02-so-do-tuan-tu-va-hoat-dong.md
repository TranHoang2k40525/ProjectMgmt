# CHUYÊN ĐỀ 2: SƠ ĐỒ TUẦN TỰ & SƠ ĐỒ HOẠT ĐỘNG CỦA MODULE AI AGENT

---

## 1. Tổng quan Kiến trúc Vận hành & Luồng Dữ liệu
Trong hệ thống Quản lý Dự án Scrum Agile, quá trình phân rã Sprint bằng trí tuệ nhân tạo không phải là một lệnh gọi API đơn lẻ, mà là một **Agentic Workflow tuần tự gồm 5 giai đoạn được kiểm soát chặt chẽ (Human-in-the-loop Pipeline)**.

Module này đảm bảo 3 tiêu chí cốt lõi:
1. **Phản hồi tức thì thời gian thực (Realtime Transparency)**: Người dùng nhìn thấy rõ ràng AI đang chạy công cụ nào, đọc tài liệu gì, mất bao nhiêu mili-giây, thay vì nhìn màn hình chờ thụ động.
2. **Ngăn chặn triệt để ảo giác (Zero Hallucination Grounding)**: AI buộc phải truy vấn dữ liệu thực tế thông qua các MCP Tools trước khi suy luận.
3. **Phê duyệt Staging an toàn trước khi ghi vào Database**: Kết quả sinh ra được lưu trên bộ nhớ đệm phân tán Redis để người dùng review, chỉnh sửa, yêu cầu sinh lại từng task độc lập.

---

## 2. Sơ đồ Tuần tự Toàn diện (Sequence Diagram)

Sơ đồ tuần tự dưới đây mô tả chi tiết từ thời điểm Scrum Master mở modal, gửi yêu cầu/tài liệu cho đến khi Sprint và các Task chính thức được tạo trong cơ sở dữ liệu:

```mermaid
sequenceDiagram
    autonumber
    actor SM as Scrum Master / PM
    participant FE as Frontend Angular<br/>(ai-decompose-modal)
    participant Svc as Angular Service<br/>(ai-agent.service.ts)
    participant Ctrl as AiAgentController<br/>(.NET 8 Web API)
    participant Pipeline as AiSprintDecompositionService<br/>(Agent Pipeline 5 bước)
    participant Guard as ProjectScopeGuard &<br/>InputGuardrail
    participant Mcp as In-Process MCP Tools<br/>(SpecHub, Backlog, Sprint, Team)
    participant Router as MultiProviderAiModelClient<br/>(Gemini / Claude / OpenAI)
    participant LLM as Mô hình AI Cung cấp<br/>(Google Gemini API)
    participant Redis as ResilientStateStagingService<br/>(Redis 127.0.0.1:6379)
    participant DB as MySQL Database<br/>(DbContext Transaction)

    %% Giai đoạn 1: Mở Modal & Tiếp nhận
    Note over SM,FE: GIAI ĐOẠN 1: KHỞI TẠO VÀ GỬI YÊU CẦU
    SM->>FE: Nhập mục tiêu / Dán biên bản họp / Đính kèm tài liệu (.docx, .pdf, .xlsx, .txt)
    SM->>FE: Bấm "Phân rã Sprint với AI"
    FE->>Svc: decomposeSprintStreaming(projectId, sprintId, request)
    Svc->>Ctrl: HTTP POST /api/ai/agent/decompose/stream<br/>Accept: text/event-stream (Bearer JWT)
    
    %% Giai đoạn 2: Guardrail & Khởi tạo SSE
    Note over Ctrl,Pipeline: GIAI ĐOẠN 2: BẢO MẬT & GUARDRAIL
    Ctrl->>Guard: ValidateContext(userId, projectId, orgId)
    Guard-->>Ctrl: OK (Người dùng có quyền trong Project)
    Ctrl-->>Svc: HTTP 200 OK (Content-Type: text/event-stream)
    Ctrl->>Pipeline: DecomposeSprintStreamingAsync(request, cancellationToken)

    %% Giai đoạn 3: Chạy MCP Tools lấy ngữ cảnh dự án
    Note over Pipeline,Mcp: GIAI ĐOẠN 3: GROUNDING DỮ LIỆU THỰC TẾ (MCP TOOLS)
    Pipeline-->>Ctrl: Emit Event: Step 1 ("Tiếp nhận & Guardrail tài liệu")
    Ctrl-->>Svc: data: {"type":"step","step":1}
    Svc-->>FE: Cập nhật thanh tiến trình Bước 1 hoàn thành

    Pipeline-->>Ctrl: Emit Event: Step 2 ("Truy vấn MCP Spec Hub & Lịch sử Sprint")
    Ctrl-->>Svc: data: {"type":"step","step":2}
    
    Pipeline->>Mcp: SpecHubTools.SearchRelevantSpecs(query, projectId)
    Mcp-->>Pipeline: Trả về các đoạn trích đặc tả yêu cầu & tên file
    Pipeline-->>Ctrl: Emit Event: ToolCall ("Tìm kiếm tài liệu đặc tả trong Spec Hub", 7ms)
    Ctrl-->>Svc: data: {"type":"tool","name":"SpecHub","latencyMs":7}

    Pipeline->>Mcp: BacklogTools.GetBacklogStories(projectId)
    Mcp-->>Pipeline: Trả về danh sách User Stories & độ ưu tiên
    Pipeline-->>Ctrl: Emit Event: ToolCall ("Lấy danh sách User Story Backlog", 13ms)
    Ctrl-->>Svc: data: {"type":"tool","name":"Backlog","latencyMs":13}

    Pipeline->>Mcp: SprintTools.GetSprintHistoryAndVelocity(projectId)
    Mcp-->>Pipeline: Vận tốc trung bình (VD: 32 SP) & các việc dở dang
    Pipeline-->>Ctrl: Emit Event: ToolCall ("Truy xuất vận tốc Scrum", 37ms)
    Ctrl-->>Svc: data: {"type":"tool","name":"SprintVelocity","latencyMs":37}

    %% Giai đoạn 4: Phân tích Năng lực Đội ngũ
    Pipeline-->>Ctrl: Emit Event: Step 3 ("Đánh giá Năng lực & Tải công việc Đội ngũ")
    Ctrl-->>Svc: data: {"type":"step","step":3}
    Pipeline->>Mcp: TeamTools.GetTeamDevCapacities(projectId)
    Mcp-->>Pipeline: Kỹ năng thành viên (Frontend, Backend, QA) & số giờ khả dụng
    Pipeline-->>Ctrl: Emit Event: ToolCall ("Phân tích kỹ năng thành viên Scrum", 25ms)
    Ctrl-->>Svc: data: {"type":"tool","name":"TeamCapacity","latencyMs":25}

    %% Giai đoạn 5: Gọi Mô hình AI
    Note over Pipeline,LLM: GIAI ĐOẠN 4: SUY LUẬN AI (MULTI-PROVIDER CLIENT)
    Pipeline-->>Ctrl: Emit Event: Step 4 ("Phân rã Sprint Goal, Epics, Tasks & Subtasks")
    Ctrl-->>Svc: data: {"type":"step","step":4}
    Pipeline->>Router: SendChatAsync(systemPrompt, userPrompt, model="gemini-3.6-flash")
    Router->>LLM: POST https://generativelanguage.googleapis.com/... (Payload)
    LLM-->>Router: JSON Stream / Output (Sprint Goal, Epics, Tasks, SP, Hours, Citations)
    Router-->>Pipeline: Structured JSON String

    %% Giai đoạn 6: Self-Check & Lưu Staging Redis
    Note over Pipeline,Redis: GIAI ĐOẠN 5: TỰ KIỂM TRA (SELF-CHECK) & LƯU STAGING
    Pipeline-->>Ctrl: Emit Event: Step 5 ("Tự kiểm tra (Self-Check) & Lưu Staging")
    Ctrl-->>Svc: data: {"type":"step","step":5}
    Pipeline->>Pipeline: ValidateCapacityAndStoryPoints(totalSp <= teamVelocity)
    Pipeline->>Redis: SaveStagedProposalAsync(stagingId, proposalJson, ttl=24h)
    Redis-->>Pipeline: OK (Key: ProjectMgmt:Ai:staging:{id})
    Pipeline-->>Ctrl: Emit Event: ProposalPayload (Full Sprint Proposal)
    Ctrl-->>Svc: data: {"type":"proposal","payload":{...}}
    Svc-->>FE: Hiển thị kết quả đề xuất (Sprint Goal, Epics, Tasks, Badges, Citations)

    %% Giai đoạn 7: Người dùng chỉnh sửa & Áp dụng
    Note over SM,DB: GIAI ĐOẠN 6: PHÊ DUYỆT & GHI DỮ LIỆU CHÍNH THỨC
    opt Người dùng xem trước tài liệu trích dẫn
        SM->>FE: Bấm vào trích dẫn tài liệu (Citations)
        FE->>FE: Mở Drawer xem tài liệu nội tuyến (không mất hội thoại)
    end

    opt Người dùng yêu cầu sinh lại riêng 1 task
        SM->>FE: Bấm "Sinh lại" tại Task #2 với ghi chú "Chia nhỏ thêm tiêu chí kiểm thử"
        FE->>Ctrl: POST /api/ai/agent/regenerate-task (stagingId, taskId, prompt)
        Ctrl->>Pipeline: RegenerateSingleTaskAsync(...)
        Pipeline->>Redis: UpdateTaskInStaging(stagingId, updatedTask)
        Pipeline-->>FE: Trả về Task đã được cập nhật độc lập
    end

    SM->>FE: Bấm "Áp dụng vào Sprint"
    FE->>Ctrl: POST /api/ai/agent/apply-staging (stagingId, targetSprintId)
    Ctrl->>Redis: GetStagedProposalAsync(stagingId)
    Redis-->>Ctrl: Trả về JSON đề xuất đã được người dùng chỉnh sửa
    Ctrl->>DB: Bắt đầu Transaction: Tạo Epic, Tasks, Subtasks, Gán thành viên
    DB-->>Ctrl: Commit Transaction thành công
    Ctrl->>Redis: RemoveStagedProposalAsync(stagingId)
    Ctrl-->>FE: HTTP 200 OK (Số lượng task đã tạo)
    FE-->>SM: Thông báo thành công & Chuyển hướng về Bảng Sprint Backlog
```

---

## 3. Sơ đồ Hoạt động (Activity Diagram) của Pipeline 5 bước

Quy trình xử lý nội bộ của [`AiSprintDecompositionService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiSprintDecompositionService.cs) được mô hình hóa theo sơ đồ hoạt động chặt chẽ dưới đây:

```mermaid
flowchart TD
    Start(["Bắt đầu: Nhận Yêu cầu Phân rã Sprint"]) --> Step1["BƯỚC 1: TIẾP NHẬN & GUARDRAIL TÀI LIỆU\n- Kiểm tra phân quyền ProjectScopeGuard (Org & Project)\n- Trích xuất văn bản từ tệp (.docx, .pdf, .xlsx, .txt)\n- Chuẩn hóa text ghi chú cuộc họp & User input\n- Phát SSE Event: Step 1 hoàn tất"]

    Step1 --> Step2["BƯỚC 2: TRUY VẤN MCP SPEC HUB & LỊCH SỬ SPRINT\n- Gọi SpecHubTools: Tìm các đoạn đặc tả yêu cầu khớp ngữ cảnh\n- Gọi BacklogTools: Lấy các User Story đang tồn đọng\n- Gọi SprintTools: Lấy vận tốc (Velocity) trung bình các Sprint trước\n- Phát SSE Event: Step 2 & danh sách ToolCall latency"]

    Step2 --> Step3["BƯỚC 3: ĐÁNH GIÁ NĂNG LỰC & TẢI CÔNG VIỆC ĐỘI NGŨ\n- Gọi TeamTools: Quét danh sách thành viên trong dự án\n- Lấy profile kỹ năng: Frontend, Backend, Tester, DevOps\n- Tính tổng số giờ làm việc khả dụng trong chu kỳ Sprint\n- Phát SSE Event: Step 3 & danh sách ToolCall latency"]

    Step3 --> Step4["BƯỚC 4: SUY LUẬN MÔ HÌNH AI (PROMPT ENGINEERING)\n- Ghép toàn bộ ngữ cảnh thực tế vào System Prompt nghiêm ngặt\n- Áp dụng nguyên tắc Thực tế: Task 1-3 SP độc lập; chỉ $\ge 5$ SP mới chia subtask\n- Gọi MultiProviderAiModelClient (Gemini 3.6/3.5 Flash, Claude, OpenAI)\n- Phân tích cú pháp JSON phản hồi từ LLM\n- Phát SSE Event: Step 4 hoàn tất"]

    Step4 --> Check{"BƯỚC 5: TỰ KIỂM TRA (SELF-CHECK)\n- Tổng Story Points có vượt quá Velocity nhóm không?\n- Đã phân bổ đúng người theo chuyên môn kỹ năng chưa?\n- Có việc nào thiếu trích dẫn tài liệu không?"}

    Check -- "Có vi phạm nghiêm trọng" --> AutoFix["Tự động điều chỉnh cân bằng lại Story Points\n& gắn nhãn cảnh báo (Warning flag)"]
    AutoFix --> SaveStaging
    Check -- "Hợp lệ chuẩn Scrum" --> SaveStaging["LƯU TRỮ VÀO BỘ NHỚ ĐỆM REDIS (STAGING)\n- Tạo StagingId ngẫu nhiên an toàn (Guid)\n- Serialize JSON đề xuất vào Redis Key với TTL 24 giờ\n- Fallback In-Memory nếu kết nối Redis gián đoạn\n- Phát SSE Event: Step 5 & Gửi toàn bộ Proposal Payload"]

    SaveStaging --> DisplayUI["GIAO DIỆN HIỂN THỊ ĐỀ XUẤT CHO NGƯỜI DÙNG\n- Hiển thị Sprint Goal & danh sách Epics, Tasks\n- Badges: Trạng thái, Số việc con, #Tags công nghệ\n- Trích dẫn tài liệu Spec Hub có liên kết mở Drawer"]

    DisplayUI --> UserAction{"Lựa chọn của Scrum Master"}

    UserAction -- "Xem tài liệu đặc tả" --> OpenDrawer["Mở Drawer nội tuyến xem chi tiết tài liệu\n(Không reload, không mất lịch sử hội thoại)"]
    OpenDrawer --> DisplayUI

    UserAction -- "Chỉnh sửa trực tiếp" --> EditItem["Sửa tiêu đề, số SP, người phụ trách trên bảng"]
    EditItem --> DisplayUI

    UserAction -- "Yêu cầu AI làm lại task riêng" --> RegenTask["Gửi yêu cầu Regenerate kèm lý do đến API\nAI cập nhật lại riêng task đó trong Staging"]
    RegenTask --> DisplayUI

    UserAction -- "Phê duyệt áp dụng" --> CommitDB["BẤM NÚT 'ÁP DỤNG VÀO SPRINT'\n- Thực thi DB Transaction tạo Issues, Tasks, Subtasks\n- Xóa Staging Key khỏi Redis\n- Cập nhật Sprint Backlog chính thức"]

    CommitDB --> Finish(["Kết thúc: Sprint sẵn sàng thực thi"])
```

---

## 4. Giao thức Server-Sent Events (SSE) & Cơ chế Truyền dữ liệu Thời gian thực

### 4.1. Tại sao lựa chọn Server-Sent Events (SSE) thay vì WebSocket hay Polling?
Trong tác vụ AI Agent suy luận dài (từ 5 đến 45 giây tùy quy mô tài liệu):
- **Short/Long Polling**: Tạo áp lực cực lớn lên Web Server do gửi hàng chục HTTP request thừa thãi, gây lãng phí tài nguyên và độ trễ cao.
- **WebSocket**: Quá phức tạp cho tác vụ một chiều (server truyền tiến trình về client), đòi hỏi duy trì kết nối TCP song công toàn phần (Full-duplex) và dễ bị proxy/firewall doanh nghiệp chặn.
- **Server-Sent Events (SSE - `text/event-stream`)**: Chuẩn HTTP/1.1 và HTTP/2 gốc của trình duyệt, truyền tải một chiều nhẹ nhàng, hỗ trợ streaming văn bản mượt mà, vượt qua tường lửa dễ dàng và tích hợp sẵn cơ chế auto-reconnect.

### 4.2. Cấu trúc Message SSE được chuẩn hóa trong API
Mỗi gói tin SSE phát đi từ [`AiAgentController.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Controllers/AiAgentController.cs) tuân theo định dạng:

```http
HTTP/1.1 200 OK
Content-Type: text/event-stream
Cache-Control: no-cache
Connection: keep-alive
Transfer-Encoding: chunked

data: {"type":"step","step":1,"title":"Tiếp nhận & Guardrail tài liệu","status":"completed"}

data: {"type":"tool","name":"SpecHubTools.SearchRelevantSpecs","latencyMs":7,"summary":"Tìm thấy 3 phân đoạn trong Cam-nang-ky-thuat.md"}

data: {"type":"tool","name":"BacklogTools.GetBacklogStories","latencyMs":13,"summary":"Lấy 8 User Stories đang chờ xử lý"}

data: {"type":"tool","name":"SprintTools.GetSprintHistoryAndVelocity","latencyMs":37,"summary":"Vận tốc trung bình 3 Sprint gần nhất: 32 SP"}

data: {"type":"tool","name":"TeamTools.GetTeamDevCapacities","latencyMs":25,"summary":"3 Lập trình viên, tổng dung lượng: 120 giờ"}

data: {"type":"step","step":4,"title":"Mô hình AI đang suy luận cấu trúc Sprint, chia nhỏ Epic và Task...","status":"running"}

data: {"type":"proposal","stagingId":"a8f3b2e1-4c5d-6e7f-8a9b-0c1d2e3f4a5b","payload":{...}}

data: {"type":"done"}
```

### 4.3. Cơ chế Tự động Làm mới Access Token (JWT Token Auto-Refresh on 401)
Khi phiên làm việc kéo dài, JWT Access Token có thể hết hạn ngay trong lúc người dùng đang thao tác. Angular Service [`ai-agent.service.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/core/services/ai-agent.service.ts) được trang bị cơ chế tự phục hồi thông minh:
1. Khi request SSE nhận HTTP status `401 Unauthorized`, interceptor hoặc hàm streaming bắt lỗi ngay lập tức.
2. Tự động kích hoạt luồng gọi `AuthService.refreshToken()` bằng Refresh Token lưu an toàn.
3. Nhận Access Token mới và tự động mở lại kết nối stream mà không ngắt quãng trải nghiệm của người dùng.

---

## 5. Danh mục Tệp Mã Nguồn Tham Chiếu Chuyên Đề 2

| Thành phần | Vai trò kỹ thuật | Liên kết mã nguồn |
| :--- | :--- | :--- |
| **AiAgentController** | Tiếp nhận SSE, điều phối stream, tái tạo task và commit staging | [`AiAgentController.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Controllers/AiAgentController.cs#L25-L160) |
| **AiSprintDecompositionService** | Pipeline 5 bước thực thi Agentic Workflow, prompt engineering | [`AiSprintDecompositionService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiSprintDecompositionService.cs#L45-L250) |
| **MultiProviderAiModelClient** | Router gọi API mô hình ngoài (Gemini, Claude, OpenAI) | [`MultiProviderAiModelClient.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/MultiProviderAiModelClient.cs#L25-L120) |
| **SpecHubTools** | Công cụ trích xuất đặc tả yêu cầu phục vụ Grounding | [`SpecHubTools.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/McpServers/SpecHubTools.cs#L15-L80) |
| **ai-agent.service.ts** | Xử lý EventSource, đọc SSE stream theo từng chunk JSON | [`ai-agent.service.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/core/services/ai-agent.service.ts#L40-L150) |
| **ai-decompose-modal.ts** | Render giao diện tiến trình 5 bước, bảng công việc đề xuất | [`ai-decompose-modal.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/backlog/components/ai-decompose-modal/ai-decompose-modal.ts#L80-L280) |
