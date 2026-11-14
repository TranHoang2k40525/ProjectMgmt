# CHUYÊN ĐỀ 3: CƠ CHẾ TRÍ NHỚ & BỘ NHỚ ĐỆM PHÂN TÁN REDIS (REDIS MEMORY ENGINE)

---

## 1. Bản chất & Phân loại Trí nhớ AI trong Hệ thống
Trong kỹ thuật phần mềm tích hợp AI, một Agent thông minh không thể chỉ tồn tại dưới dạng "Stateless" (không lưu trạng thái). Nếu không có cơ chế trí nhớ, Agent sẽ quên sạch ngữ cảnh ngay sau khi hoàn thành một lượt suy luận, không thể duy trì phiên hội thoại để người dùng chỉnh sửa, và có nguy cơ ghi dữ liệu rác trực tiếp vào cơ sở dữ liệu sản xuất.

Dự án thiết kế một **Kiến trúc Trí nhớ 4 Tầng Đa Cấp (Four-Tier AI Memory Architecture)**:

```mermaid
flowchart TD
    subgraph T1["TẦNG 1: TRÍ NHỚ LÀM VIỆC TỨC THỜI (WORKING MEMORY)"]
        WM1["Cửa sổ Ngữ cảnh của Mô hình Trí tuệ Nhân tạo"]
        WM2["Vùng nhớ Tạm thời lưu Kết quả Tra cứu Công cụ"]
        WM3["Thời gian tồn tại: Trong suốt tiến trình phân rã (vài giây đến vài chục giây)"]
    end

    subgraph T2["TẦNG 2: TRÍ NHỚ HỘI THOẠI & STAGING CÓ TRẠNG THÁI (STATEFUL CONVERSATION & STAGING MEMORY)"]
        SM1["Lưu trữ trên Hệ thống Bộ nhớ đệm Phân tán Đàn hồi"]
        SM2["Chứa bản đề xuất (Mục tiêu, Epics, Tasks, Subtasks) KÈM lịch sử hội thoại đa lượt"]
        SM3["Hỗ trợ định tuyến đích đến (Sprint hoặc Kho Backlog), cờ hành động & ngữ cảnh nối tiếp"]
        SM4["Cho phép Người dùng Trao đổi tiếp, Hiệu chỉnh, Xuất Excel hoặc Phê duyệt vào Backlog/Sprint"]
    end

    subgraph T3["TẦNG 3: BỘ NHỚ LỊCH SỬ PHIÊN DỰ ÁN (PROJECT SESSION MEMORY)"]
        PM1["Quản lý bởi Bộ Giám sát Tài nguyên & Lịch sử Phiên"]
        PM2["Lưu vết các phiên phân rã Sprint của từng dự án theo thời gian"]
        PM3["Lưu vết chỉ số tài nguyên, độ trễ và tỷ lệ hoàn tất thành công"]
        PM4["Hỗ trợ khôi phục phiên làm việc trước đó trực tiếp từ giao diện"]
    end

    subgraph T4["TẦNG 4: TRÍ NHỚ DÀI HẠN CỐ ĐỊNH (LONG-TERM GROUNDING MEMORY)"]
        LM1["Kho tài liệu đặc tả kỹ thuật và biên bản họp dự án"]
        LM2["Lịch sử chu kỳ, vận tốc thực tế và danh mục công việc tồn đọng"]
        LM3["Bộ quy chuẩn dự án: Trạng thái quy trình, loại công việc, mức ưu tiên"]
        LM4["Hồ sơ năng lực, cơ cấu chuyên môn và định mức giờ của đội ngũ"]
        LM5["Không gian lưu trữ Vector phục vụ tìm kiếm ngữ nghĩa chuyên sâu"]
    end

    T1 -->|Khi suy luận hoàn tất, lưu bộ nhớ tạm| T2
    T2 -->|Ghi nhận tóm tắt phiên & chỉ số tài nguyên| T3
    T2 -->|Khi người điều phối phê duyệt áp dụng| T4
    T3 -.->|Khôi phục lại dữ liệu phiên trước đó| T2
    T4 -.->|Cung cấp ngữ cảnh thực tế & quy chuẩn vào| T1
```

---

## 2. Sơ đồ Hoạt động của Chức năng Trí nhớ Redis (Memory Context Flowchart)

Sơ đồ dưới đây minh họa chi tiết cách dữ liệu đề xuất AI được ghi, đọc, cập nhật cục bộ và giải phóng trên Redis thông qua cơ chế bọc đàn hồi (Resilient Wrapper):

```mermaid
flowchart TD
    StartInput["Hoàn tất lượt hội thoại hoặc Người dùng gửi yêu cầu nối tiếp"] --> LoadPrior["Đọc trí nhớ hội thoại và bản đề xuất cũ theo mã phiên\n(nếu tiếp tục cuộc hội thoại trước đó)"]
    LoadPrior --> CheckConfig{"Kiểm tra Cấu hình Bộ nhớ đệm Phân tán"}

    CheckConfig -- "Chưa bật" --> MemoryFallback["Chuyển hướng lưu vào Bộ nhớ đệm Nội bộ an toàn"]
    CheckConfig -- "Đã bật" --> CheckCircuit{"Kiểm tra Trạng thái Ngắt mạch Tự động"}

    CheckCircuit -- "Mạch Đang Mở (Hệ thống gặp sự cố)" --> MemoryFallback
    CheckCircuit -- "Mạch Đang Đóng (Hoạt động bình thường)" --> TryDistributed["Ghi cả lịch sử hội thoại & bản đề xuất vào Bộ nhớ đệm Phân tán\nkèm thời hạn tồn tại an toàn"]

    TryDistributed --> CheckResult{"Giao tiếp thành công?"}
    CheckResult -- "Thành công" --> ResetFailures["Khởi tạo lại bộ đếm lỗi ngắt mạch\nTrả về mã định danh phiên cho Client"]
    CheckResult -- "Gián đoạn / Timeout" --> RecordFailure["Ghi nhận lỗi vào bộ ngắt mạch\n(Đạt ngưỡng quy định -> Mở mạch tự động)"]
    RecordFailure --> MemoryFallback

    MemoryFallback --> ReturnClient["Trả về mã định danh phiên cho Client\n(Đảm bảo trải nghiệm hội thoại liền mạch không ngắt quãng)"]

    ReturnClient --> UserAction{"Tương tác tiếp theo của Người điều phối"}

    UserAction -- "Hiệu chỉnh trực tiếp thông tin" --> PatchMemory["Gửi dữ liệu cập nhật trực tiếp vào bộ nhớ phiên"]
    PatchMemory --> ReturnClient

    UserAction -- "Nhắn tin tiếp / Yêu cầu sửa / Hỏi đáp / Xuất Excel" --> StartInput

    UserAction -- "Đẩy Kho Backlog hoặc Áp dụng vào Sprint" --> CommitPhase["1. Đọc toàn bộ kế hoạch từ bộ nhớ phiên\n2. Mở giao dịch cơ sở dữ liệu: Ghi nhận vào Kho Backlog (Sprint rỗng) hoặc Sprint mới\n3. Xóa danh sách việc đã ghi và bổ sung tin nhắn xác nhận vào lịch sử hội thoại phiên\n4. Cho phép tiếp tục hội thoại trong cùng phiên"]

    CommitPhase --> Done(["Hoàn tất: Công việc đã ghi nhận & Phiên hội thoại vẫn liền mạch"])
```

---

## 3. Cấu trúc Khóa, Định dạng Lưu trữ & Thời gian Sống (TTL)

### 3.1. Cấu trúc Khóa Redis (Keyspace Convention)
Khóa được định danh theo tiền tố cấu hình (`InstanceName = "ProjectMgmt:Ai:"`) kết hợp định danh phiên ngẫu nhiên (Session Guid):

```text
ProjectMgmt:Ai:staging:{stagingGuid}
```
*Ví dụ thực tế:*
`ProjectMgmt:Ai:staging:e7c4f1a2-8b3d-4e9a-9c12-3456789abcde`

### 3.2. Cấu trúc Payload JSON Lưu trữ
Toàn bộ đề xuất phân rã kèm trạng thái đoạn hội thoại đa lượt được tuần tự hóa (serialize) thành chuỗi JSON tối ưu:

```json
{
  "stagingId": "sprint_ai_e7c4f1a28b3d4e9a9c123456789abcde",
  "projectId": 12,
  "sprintId": 5,
  "intent": "sprint_plan",
  "assistantReply": "Tôi đã phân tích tài liệu đặc tả cùng kho Backlog và đề xuất kế hoạch gồm 2 công việc chính (8 SP).",
  "recommendedTarget": "sprint",
  "triggerAction": null,
  "sprintGoal": "Xây dựng lõi xác thực đa yếu tố và đồng bộ dữ liệu thời gian thực cho hệ thống",
  "suggestedSprintName": "Sprint 5 - Bảo mật & Realtime Engine",
  "targetVelocity": 32,
  "totalStoryPoints": 28,
  "conversationHistory": [
    {
      "id": "turn-1-u",
      "role": "user",
      "content": "Phân rã tính năng xác thực RBAC và realtime SSE",
      "attachmentName": "Kien-truc-realtime.docx",
      "intent": "sprint_plan",
      "toolSteps": [],
      "timestamp": "2026-11-14T08:15:20Z"
    },
    {
      "id": "turn-1-a",
      "role": "assistant",
      "content": "Tôi đã phân tích tài liệu đặc tả cùng kho Backlog và đề xuất kế hoạch gồm 2 công việc chính.",
      "intent": "sprint_plan",
      "toolSteps": ["spec_hub_search: 7ms", "backlog_get_stories: 13ms"],
      "timestamp": "2026-11-14T08:15:30Z"
    }
  ],
  "epics": [
    {
      "id": "epic-1",
      "title": "Hệ thống Xác thực Nâng cao & Phân quyền RBAC",
      "description": "Bao gồm đăng nhập, cấp JWT và phân quyền theo cấp bậc Scrum",
      "color": "#3B82F6"
    }
  ],
  "tasks": [
    {
      "id": "task-1",
      "epicId": "epic-1",
      "title": "Thiết kế Middleware kiểm tra quyền theo cấp bậc Scrum",
      "description": "Ngăn chặn vượt cấp từ Developer/QA lên cấp quản lý",
      "storyPoints": 3,
      "estimatedHours": 8,
      "assignedUserId": "u-dev-01",
      "assignedUserName": "Lê Văn A (Backend)",
      "status": "Ready",
      "tags": ["Security", "Middleware", "RBAC"],
      "citations": [
        "Tài liệu Spec Hub: Cam-nang-ky-thuat.md - Mục 4.1",
        "Ghi chú cuộc họp: Biên bản họp Sprint Planning - Đoạn 2"
      ],
      "subtasks": []
    },
    {
      "id": "task-2",
      "epicId": "epic-1",
      "title": "Tích hợp giao thức Server-Sent Events cho luồng AI Stream",
      "description": "Xử lý kết nối streaming một chiều, reconnect và tự động refresh token khi nhận 401",
      "storyPoints": 5,
      "estimatedHours": 16,
      "assignedUserId": "u-dev-02",
      "assignedUserName": "Nguyễn Thị B (Frontend)",
      "status": "Ready",
      "tags": ["SSE", "Realtime", "Auth"],
      "citations": [
        "Tài liệu Spec Hub: Kien-truc-realtime.docx - Trang 12"
      ],
      "subtasks": [
        {
          "id": "subtask-2-1",
          "title": "Cấu hình Controller phát event-stream trong .NET 8",
          "role": "Backend",
          "estimatedHours": 6,
          "storyPoints": 2,
          "priority": "High",
          "status": "To Do",
          "acceptanceCriteria": [
            "Controller trả về text/event-stream chuẩn SSE",
            "Đóng kết nối an toàn khi Client disconnect"
          ],
          "documentCitations": [
            "Tài liệu Spec Hub: Kien-truc-realtime.docx - Trang 12"
          ]
        },
        {
          "id": "subtask-2-2",
          "title": "Xây dựng Angular Service bắt lỗi 401 và gọi Refresh Token",
          "role": "Frontend",
          "estimatedHours": 6,
          "storyPoints": 2,
          "priority": "High",
          "status": "To Do",
          "acceptanceCriteria": [
            "Interceptor bắt lỗi 401 và gọi Refresh Token tự động",
            "Kết nối stream được mở lại không làm mất phiên"
          ],
          "documentCitations": [
            "Tài liệu Spec Hub: Kien-truc-realtime.docx - Trang 12"
          ]
        },
        {
          "id": "subtask-2-3",
          "title": "Viết kiểm thử tự động giả lập mất mạng đột ngột",
          "role": "QA",
          "estimatedHours": 4,
          "storyPoints": 1,
          "priority": "Medium",
          "status": "To Do",
          "acceptanceCriteria": [
            "Kịch bản kiểm thử giả lập ngắt mạng và tự kết nối lại",
            "Không phát sinh ngoại lệ chưa được xử lý"
          ],
          "documentCitations": [
            "Tài liệu Spec Hub: Kien-truc-realtime.docx - Trang 12"
          ]
        }
      ]
    }
  ],
  "warnings": [
    "Tổng Story Points (28 SP) nằm trong ngưỡng an toàn so với vận tốc trung bình (32 SP)."
  ],
  "createdAt": "2026-11-11T00:15:30Z",
  "expiresAt": "2026-11-12T00:15:30Z"
}
```

### 3.3. Thời gian Sống (TTL) & Chính sách Cập nhật / Giải phóng (Eviction & Lifecycle Policy)
- **TTL Mặc định**: `24 giờ` (`TimeSpan.FromHours(24)`).
- **Lý do thiết kế**: Đủ dài để Scrum Master đóng laptop, trao đổi nội bộ với nhóm và quay lại tiếp tục đoạn hội thoại vào ngày hôm sau mà không bị mất ngữ cảnh; đồng thời tự động biến mất sau 24h để giải phóng dung lượng RAM của Redis.
- **Bảo toàn Hội thoại Sau Khi Commit (`ApplyProposal` / `PushToBacklog`)**: Khi người dùng bấm **"Áp dụng vào Sprint"** hoặc **"Đẩy vào Kho Backlog"**, `AiAgentController.ApplyProposal` ghi nhận công việc vào MySQL, xóa danh sách `Tasks` đã commit trong bản Staging (tránh áp dụng trùng lặp nhiều lần), đồng thời **bổ sung tin nhắn xác nhận của Trợ Lý AI vào `ConversationHistory` và lưu lại vào Redis** để đoạn hội thoại được giữ nguyên mạch tiếp tục.
- **Xóa sạch Chủ động (Explicit Full Reset)**: Khi người dùng bấm nút **"Làm mới & Phiên mới"** (`POST /api/ai/agent/clear-cache`), hệ thống gọi `RemoveAsync(key)` để thu hồi toàn bộ bộ nhớ phiên cũ trên Redis và tạo `sessionId` mới.

---

## 4. Kiến trúc Chống Sập & Khả năng Phục hồi Cao (Resilience & Circuit Breaker)

Dự án áp dụng mẫu thiết kế **Composite Provider kết hợp Circuit Breaker** trong [`ResilientStateStagingService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Core/Staging/ResilientStateStagingService.cs):

```csharp
// ProjectMgmt.Core/Staging/ResilientStateStagingService.cs
public async Task SaveProposalAsync<T>(string sessionId, T proposal, TimeSpan ttl, CancellationToken ct = default)
{
    if (!_redisEnabled || _primaryProvider == null)
    {
        await _memoryFallback.SaveProposalAsync(sessionId, proposal, ttl, ct).ConfigureAwait(false);
        return;
    }

    var json = JsonSerializer.Serialize(proposal, _jsonOptions);

    await _circuitBreaker.ExecuteWithFallbackAsync(
        primaryAction: async token =>
        {
            await _primaryProvider.SetAsync(sessionId, json, ttl, token).ConfigureAwait(false);
        },
        fallbackAction: async token =>
        {
            await _memoryFallback.SaveProposalAsync(sessionId, proposal, ttl, token).ConfigureAwait(false);
        },
        ct: ct,
        onFallback: (reason, ex) =>
        {
            _logger.LogWarning(ex, "[ResilientStaging] {Reason} SessionId={SessionId}", reason, sessionId);
        }).ConfigureAwait(false);
}
```

### Các trạng thái của Circuit Breaker:
1. **Closed (Bình thường)**: Mọi yêu cầu ghi/đọc đều đi vào Redis.
2. **Open (Ngắt mạch)**: Nếu Redis gặp lỗi 3 lần liên tiếp (do mạng chậm quá 2000ms hoặc server Redis tắt), mạch tự động mở trong **60 giây**. Trong thời gian này, mọi thao tác chuyển hướng trực tiếp sang RAM (`MemoryStateStagingService`) mà **không hề gửi request đến Redis**, loại bỏ hoàn toàn độ trễ chờ đợi cho người dùng.
3. **Half-Open (Thử nghiệm hồi phục)**: Sau 60 giây, hệ thống cho phép 1 request đi thử vào Redis. Nếu thành công, mạch đóng lại bình thường; nếu thất bại, mạch tiếp tục mở thêm 60 giây.

---

## 5. Hướng dẫn Kiểm tra & Chứng minh Trí nhớ Redis (Verification Guide)

### 5.1. Kiểm tra Dịch vụ Redis Đang Chạy
Mở PowerShell trên máy tính chủ và thực hiện kiểm tra port kết nối:

```powershell
# Kiểm tra port 6379 đang lắng nghe
Test-NetConnection -ComputerName 127.0.0.1 -Port 6379
# Kết quả: TcpTestSucceeded : True

# Kiểm tra tiến trình redis-server trên Windows Task Manager
Get-Process -Name *redis*
```

### 5.2. Kiểm tra Dữ liệu Staging trong Redis CLI
Khi AI vừa phân rã xong một Sprint trên giao diện, mở terminal Redis CLI để tra cứu dữ liệu thực tế:

```bash
# 1. Kết nối vào Redis CLI cục bộ
redis-cli -h 127.0.0.1 -p 6379

# 2. Kiểm tra kết nối
127.0.0.1:6379> PING
PONG

# 3. Liệt kê các khóa Staging đang được AI lưu giữ
127.0.0.1:6379> KEYS ProjectMgmt:Ai:staging:*
1) "ProjectMgmt:Ai:staging:e7c4f1a2-8b3d-4e9a-9c12-3456789abcde"

# 4. Kiểm tra thời gian tồn tại còn lại (TTL - tính bằng giây)
127.0.0.1:6379> TTL ProjectMgmt:Ai:staging:e7c4f1a2-8b3d-4e9a-9c12-3456789abcde
(integer) 86340

# 5. Đọc nội dung JSON đề xuất đang được giữ trong trí nhớ
127.0.0.1:6379> GET ProjectMgmt:Ai:staging:e7c4f1a2-8b3d-4e9a-9c12-3456789abcde
"{\"stagingId\":\"e7c4f1a2...\",\"sprintGoal\":\"Xây dựng lõi xác thực...\"}"
```

### 5.3. Chứng minh Tính Độc lập với Cơ sở Dữ liệu & Hội thoại Liên tục
1. **Trước khi bấm "Đẩy vào Kho Backlog" hoặc "Áp dụng vào Sprint"**: Kiểm tra trong MySQL: bảng `Issues` chưa có bất kỳ bản ghi nào mới từ AI. Dữ liệu kế hoạch và `conversationHistory` chỉ tồn tại duy nhất trên Redis (`ProjectMgmt:Ai:staging:*`).
2. **Sau khi bấm "Đẩy vào Kho Backlog" hoặc "Áp dụng vào Sprint"**:
   - Nếu chọn **Đẩy vào Kho Backlog** (`PushToBacklog = true`): MySQL ghi nhận các Issue mới với `SprintId = null` (nằm trực tiếp trong kho Product Backlog) mà không tạo Sprint mới.
   - Nếu chọn **Áp dụng vào Sprint**: MySQL ghi nhận Sprint mới cùng các Issue tương ứng và di chuyển các Story từ kho Backlog (nếu có) không trùng lặp.
   - Kiểm tra lại Redis CLI: Trường `tasks` trong bản Staging đã được dọn sạch (`[]`) nhưng `conversationHistory` vẫn được cập nhật thêm tin nhắn xác nhận để tiếp tục cuộc hội thoại; khi bấm **"Làm mới & Phiên mới"**, khóa `ProjectMgmt:Ai:staging:...` được xóa hoàn toàn (`nil`).

---

## 6. Bộ Nhớ Lịch Sử Phiên & Quản Trị Tiêu Thụ Token (Cross-Session Project Memory & Token Analytics)

Bên cạnh bộ nhớ đề xuất tức thời (Staging Session), hệ thống được trang bị hệ thống quản trị phiên làm việc và đo lường tài nguyên AI độc lập thông qua giao diện [`IAiTokenUsageTracker`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/IAiTokenUsageTracker.cs) và lớp triển khai [`AiTokenUsageTracker`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiTokenUsageTracker.cs):

### 6.1. Quản lý Lịch sử Phiên Phân rã Dự án (Project Session Memory)
- **Cơ chế hoạt động**: Mỗi khi một lượt phân rã Sprint hoàn tất, hệ thống tự động ghi nhận bản tóm tắt phiên [`AiSessionSummary`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/IAiTokenUsageTracker.cs) gồm: `SessionId`, `ProjectId`, `SprintGoal`, `TaskCount`, `TotalStoryPoints`, `Model`, `CreatedByName`, `CreatedAtUtc`.
- **Khôi phục phiên làm việc cũ (Session History Restore)**:
  - Trên thanh tiêu đề của Modal AI có biểu tượng **Lịch sử phiên (đồng hồ)**.
  - Khi nhấp vào, dropdown hiển thị danh sách các phiên phân tách gần nhất trong dự án từ API `GET /api/ai/agent/sessions?projectId=...`.
  - Nhấp vào phiên bất kỳ để khôi phục toàn bộ bản kế hoạch đề xuất từ Staging Cache (`GET /api/ai/agent/proposal/{sessionId}`) mà không cần chạy lại mô hình LLM từ đầu.

### 6.2. Đo lường & Giám sát Tiêu thụ Token (Token Consumption Management)
- **Bộ ghi nhận mức tiêu thụ**: Ghi nhận `AiUsageRecord` theo từng phiên làm việc (Prompt Tokens, Completion Tokens, Latency mili-giây, Model, trạng thái Success/Failed).
- **Dashboard Quản trị trên Trang Cá nhân (Profile - Tab "Mức tiêu thụ Token AI")**:
  - **4 Thẻ Chỉ số (KPIs)**: Tổng Token tiêu thụ, Tổng cuộc gọi AI, Tỷ lệ hoàn tất thành công, Chi phí ước tính (USD).
  - **Phân bổ theo Mô hình**: Tỷ lệ phần trăm và số lượng token giữa `Gemini Flash`, `Gemini Pro`, `Gemini Flash-Lite`.
  - **Bảng Nhật ký Thực thi**: Hiển thị chi tiết 15 lượt gọi gần nhất với thời gian thực thi, số token vào/ra và trạng thái.

### 6.3. Cơ chế Giải phóng Bộ nhớ đệm Cache & Làm mới Tri thức (Staging Cache Invalidation & Session Reset)
- **Xóa sạch bộ đệm phân tán Redis**: Endpoint `POST /api/ai/agent/clear-cache` giải phóng tức thì các đối tượng đề xuất lưu tạm trong Redis (`ProjectMgmt:Ai:staging:...`), ngăn chặn tình trạng lưu vết kế hoạch cũ khi người dùng chuyển hướng hoặc thay đổi hoàn toàn yêu cầu.
- **Khởi tạo phiên làm việc tươi mới 100%**: Trên thanh tiêu đề của Modal **Trợ Lý AI**, nút **"Làm mới & Phiên mới"** cho phép người dùng chủ động xóa bỏ cache cục bộ (`localStorage`), hủy bỏ đề xuất/đoạn chat cũ và giải phóng bộ nhớ đệm server chỉ với một thao tác duy nhất.

---

## 7. Danh mục Tệp Mã Nguồn Tham Chiếu Chuyên Đề 3

| Thành phần | Vai trò kỹ thuật | Liên kết mã nguồn |
| :--- | :--- | :--- |
| **StackExchangeRedisStagingProvider** | Adapter kết nối Redis, quản lý connection pool & key operations | [`StackExchangeRedisStagingProvider.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/Adapters/StackExchangeRedisStagingProvider.cs#L8-L115) |
| **ResilientStateStagingService** | Wrapper chống sập, điều phối Redis và In-Memory fallback | [`ResilientStateStagingService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Core/Staging/ResilientStateStagingService.cs#L15-L110) |
| **MemoryStateStagingService** | Bộ nhớ đệm Fallback lưu trên ConcurrentDictionary của RAM | [`MemoryStateStagingService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Core/Staging/MemoryStateStagingService.cs#L10-L60) |
| **AiTokenUsageTracker** | Quản lý lịch sử phiên dự án và đo lường tiêu thụ token AI | [`AiTokenUsageTracker.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiTokenUsageTracker.cs) |
| **IAiTokenUsageTracker** | Hợp đồng interface theo dõi token và lịch sử phiên | [`IAiTokenUsageTracker.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/IAiTokenUsageTracker.cs) |
| **CircuitBreakerPolicy** | Thuật toán ngắt mạch bảo vệ hệ thống khi Redis gián đoạn | [`CircuitBreakerPolicy.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Core/Resilience/CircuitBreakerPolicy.cs#L10-L70) |
| **ProfilePageComponent** | Dashboard giám sát Token AI trên giao diện người dùng | [`profile-page.html`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/identity/profile-page.html) |
| **Cấu hình Redis** | Khai báo Host, Port, Database, InstanceName | [`appsettings.json`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/appsettings.json#L40-L55) |

