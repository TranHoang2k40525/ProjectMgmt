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
    actor SM as Người điều phối (Scrum Master / PM)
    participant UI as Giao diện Phân rã Sprint
    participant ClientSvc as Dịch vụ Kết nối Phía Client
    participant Gateway as Bộ Điều phối API AI
    participant Pipeline as Bộ Xử lý Phân rã Sprint
    participant Guard as Rào chắn Bảo mật & Phân quyền
    participant Tools as Bộ Công cụ Dữ liệu Dự án
    participant ModelHub as Bộ Chuyển tiếp Mô hình AI
    participant AI as Dịch vụ Trí tuệ Nhân tạo
    participant Staging as Bộ nhớ đệm Phiên Tạm thời
    participant Tracker as Bộ Giám sát Tài nguyên & Lịch sử
    participant DB as Cơ sở Dữ liệu Dự án

    %% Giai đoạn 1: Mở Modal & Tiếp nhận
    Note over SM,UI: GIAI ĐOẠN 1: KHỞI TẠO VÀ GỬI YÊU CẦU
    SM->>UI: Nhập mục tiêu / Dán biên bản họp / Đính kèm tài liệu đặc tả
    SM->>UI: Xác nhận yêu cầu phân rã Sprint
    UI->>ClientSvc: Khởi tạo luồng phân rã kế hoạch
    ClientSvc->>Gateway: Gửi yêu cầu phân rã kèm chứng thực phiên
    
    %% Giai đoạn 2: Guardrail & Khởi tạo luồng
    Note over Gateway,Pipeline: GIAI ĐOẠN 2: BẢO MẬT & XÁC THỰC
    Gateway->>Guard: Kiểm tra phân quyền truy cập và ranh giới dự án
    Guard-->>Gateway: Xác thực hợp lệ
    Gateway-->>ClientSvc: Mở luồng dữ liệu thời gian thực
    Gateway->>Pipeline: Bắt đầu tiến trình phân rã

    %% Giai đoạn 3: Tra cứu dữ liệu thực tế
    Note over Pipeline,Tools: GIAI ĐOẠN 3: THU THẬP DỮ LIỆU THỰC TẾ
    Pipeline-->>Gateway: Cập nhật bước 1: Tiếp nhận và chuẩn hóa tài liệu
    Gateway-->>ClientSvc: Báo cáo tiến độ bước 1
    ClientSvc-->>UI: Cập nhật trạng thái trên giao diện

    Pipeline-->>Gateway: Cập nhật bước 2: Tra cứu tài liệu đặc tả & lịch sử dự án
    Gateway-->>ClientSvc: Báo cáo tiến độ bước 2
    
    Pipeline->>Tools: Tìm kiếm các phân đoạn đặc tả phù hợp
    Tools-->>Pipeline: Trả về trích đoạn yêu cầu kỹ thuật
    Pipeline-->>Gateway: Báo cáo kết quả tra cứu đặc tả

    Pipeline->>Tools: Lấy danh sách hạng mục tồn đọng trong Backlog
    Tools-->>Pipeline: Trả về danh sách hạng mục công việc
    Pipeline-->>Gateway: Báo cáo kết quả tra cứu Backlog

    Pipeline->>Tools: Lấy dữ liệu vận tốc các chu kỳ trước
    Tools-->>Pipeline: Trả về vận tốc trung bình của nhóm
    Pipeline-->>Gateway: Báo cáo kết quả tính toán vận tốc

    %% Giai đoạn 4: Phân tích Năng lực Đội ngũ
    Pipeline-->>Gateway: Cập nhật bước 3: Đánh giá năng lực đội ngũ
    Gateway-->>ClientSvc: Báo cáo tiến độ bước 3
    Pipeline->>Tools: Phân tích kỹ năng và số giờ làm việc khả dụng
    Tools-->>Pipeline: Trả về cơ cấu chuyên môn và định mức giờ
    Pipeline-->>Gateway: Báo cáo kết quả phân tích năng lực

    %% Giai đoạn 5: Gọi Mô hình AI
    Note over Pipeline,AI: GIAI ĐOẠN 4: SUY LUẬN VÀ PHÂN RÃ KẾ HOẠCH
    Pipeline-->>Gateway: Cập nhật bước 4: Suy luận phân rã công việc
    Gateway-->>ClientSvc: Báo cáo tiến độ bước 4
    Pipeline->>ModelHub: Điều phối yêu cầu suy luận theo mô hình đã chọn
    ModelHub->>AI: Gửi toàn bộ ngữ cảnh và quy chuẩn dự án
    AI-->>ModelHub: Trả về luồng suy luận và cấu trúc đề xuất
    ModelHub-->>Pipeline: Chuyển giao dữ liệu đề xuất đã chuẩn hóa

    %% Giai đoạn 6: Self-Check Critique & Lưu Staging
    Note over Pipeline,Staging: GIAI ĐOẠN 5: TỰ KIỂM TRA & LƯU BỘ NHỚ ĐỆM PHIÊN
    Pipeline-->>Gateway: Cập nhật bước 5: Tự kiểm tra & Lưu trữ tạm thời
    Gateway-->>ClientSvc: Báo cáo tiến độ bước 5
    Pipeline->>Pipeline: Kiểm tra tiêu chuẩn nghiệm thu và cân bằng khối lượng
    Pipeline->>Staging: Lưu trữ bản đề xuất vào bộ nhớ đệm phiên
    Staging-->>Pipeline: Xác nhận lưu trữ tạm thời thành công
    Pipeline-->>Gateway: Báo cáo hoàn tất phân rã
    Gateway->>Tracker: Ghi nhận mức tiêu thụ tài nguyên và lịch sử phiên
    Tracker-->>Gateway: Xác nhận lưu vết thành công
    Gateway-->>ClientSvc: Gửi bản đề xuất kế hoạch Sprint hoàn chỉnh
    ClientSvc-->>UI: Hiển thị Thẻ Tóm tắt Điều hành, Epics, Tasks và Subtasks

    %% Giai đoạn 7: Người dùng chỉnh sửa & Áp dụng
    Note over SM,DB: GIAI ĐOẠN 6: PHÊ DUYỆT & GHI DỮ LIỆU CHÍNH THỨC
    opt Người dùng xem trước tài liệu trích dẫn
        SM->>UI: Nhấp vào nguồn trích dẫn tài liệu
        UI->>UI: Mở ngăn kéo hiển thị nội dung đặc tả liên quan
    end

    opt Người dùng yêu cầu làm lại một việc cụ thể
        SM->>UI: Yêu cầu tinh chỉnh lại công việc được chọn
        UI->>Gateway: Gửi yêu cầu tái tạo công việc kèm phản hồi
        Gateway->>Pipeline: Tái cấu trúc công việc độc lập
        Pipeline->>Staging: Cập nhật công việc mới vào bộ nhớ đệm phiên
        Pipeline-->>UI: Cập nhật công việc hiển thị trên giao diện
    end

    SM->>UI: Xác nhận áp dụng kế hoạch vào Sprint
    UI->>Gateway: Gửi lệnh phê duyệt kế hoạch chính thức
    Gateway->>Staging: Truy xuất bản kế hoạch đã qua chỉnh sửa
    Staging-->>Gateway: Trả về dữ liệu kế hoạch hoàn chỉnh
    Gateway->>DB: Ghi nhận Epics, Tasks, Subtasks và phân công nhân sự
    DB-->>Gateway: Xác nhận ghi nhận thành công
    Gateway->>Staging: Dọn dẹp dữ liệu phiên tạm thời
    Gateway-->>UI: Thông báo hoàn tất và chuyển hướng đến bảng điều phối Sprint
```

---

## 3. Sơ đồ Hoạt động (Activity Diagram) của Pipeline 5 bước

Quy trình xử lý nội bộ của [`AiSprintDecompositionService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiSprintDecompositionService.cs) được mô hình hóa theo sơ đồ hoạt động chặt chẽ dưới đây:

```mermaid
flowchart TD
    Start(["Bắt đầu: Tiếp nhận yêu cầu phân rã kế hoạch"]) --> Step1["BƯỚC 1: TIẾP NHẬN & XÁC THỰC BẢO MẬT\n- Xác thực quyền hạn và ranh giới an toàn của dự án\n- Trích xuất nội dung văn bản từ tài liệu tải lên\n- Chuẩn hóa ghi chú cuộc họp và yêu cầu người dùng\n- Phát tín hiệu tiến trình bước 1"]

    Step1 --> Step2["BƯỚC 2: TRA CỨU ĐẶC TẢ & DỮ LIỆU LỊCH SỬ\n- Tìm kiếm các đoạn tài liệu đặc tả kỹ thuật phù hợp\n- Quét danh sách các hạng mục tồn đọng trong Backlog\n- Tính toán vận tốc trung bình từ các chu kỳ trước\n- Phát tín hiệu tiến trình bước 2"]

    Step2 --> Step3["BƯỚC 3: ĐÁNH GIÁ NĂNG LỰC ĐỘI NGŨ\n- Rà soát hồ sơ chuyên môn của các thành viên\n- Xác định phân bổ kỹ năng: Giao diện, Xử lý phía sau, Kiểm thử\n- Xác định tổng quỹ giờ làm việc khả dụng trong chu kỳ\n- Phát tín hiệu tiến trình bước 3"]

    Step3 --> Step4["BƯỚC 4: SUY LUẬN & PHÂN RÃ KẾ HOẠCH\n- Nạp toàn bộ dữ liệu thực tế và quy chuẩn dự án vào mô hình\n- Áp dụng nguyên tắc: Việc nhỏ độc lập; việc phức tạp chia việc con\n- Điều phối yêu cầu suy luận đến Mô hình AI\n- Chuẩn hóa cấu trúc dữ liệu đề xuất từ phản hồi\n- Phát tín hiệu tiến trình bước 4"]

    Step4 --> Check{"BƯỚC 5: TỰ KIỂM TRA CHẤT LƯỢNG\n- Tổng khối lượng có vượt quá vận tốc đội ngũ?\n- Phân bổ nhân sự đã đúng chuyên môn kỹ thuật chưa?\n- Đã đầy đủ tiêu chí nghiệm thu và trích dẫn đặc tả chưa?"}

    Check -- "Chưa tối ưu" --> AutoFix["Tự động điều chỉnh cân bằng lại khối lượng\n& gắn nhãn lưu ý để người dùng xem xét"]
    AutoFix --> SaveStaging
    Check -- "Đạt chuẩn" --> SaveStaging["LƯU TRỮ VÀO BỘ NHỚ ĐỆM PHIÊN TẠM THỜI\n- Tạo mã phiên ngẫu nhiên an toàn\n- Lưu trữ bản đề xuất vào bộ nhớ đệm với thời hạn an toàn\n- Ghi nhận chỉ số tài nguyên và lịch sử phiên\n- Phát tín hiệu hoàn tất và gửi bản đề xuất đầy đủ"]

    SaveStaging --> DisplayUI["GIAO DIỆN HIỂN THỊ KẾ HOẠCH CHO NGƯỜI DÙNG\n- Hiển thị Thẻ Tóm tắt Điều hành, mục tiêu chu kỳ và danh sách công việc\n- Huy hiệu phân loại: Vai trò kỹ thuật, độ ưu tiên, việc con\n- Nguồn trích dẫn tài liệu cho phép mở xem trực tiếp"]

    DisplayUI --> UserAction{"Lựa chọn của Người điều phối"}

    UserAction -- "Xem tài liệu đặc tả" --> OpenDrawer["Mở ngăn kéo xem phân đoạn tài liệu trích dẫn\n(Không làm gián đoạn hay mất phiên làm việc)"]
    OpenDrawer --> DisplayUI

    UserAction -- "Chỉnh sửa trực tiếp" --> EditItem["Hiệu chỉnh tiêu đề, điểm ước lượng, người phụ trách"]
    EditItem --> DisplayUI

    UserAction -- "Yêu cầu AI làm lại việc cụ thể" --> RegenTask["Gửi yêu cầu tái tạo kèm góp ý chi tiết\nCập nhật công việc mới vào bộ nhớ đệm phiên"]
    RegenTask --> DisplayUI

    UserAction -- "Phê duyệt áp dụng" --> CommitDB["XÁC NHẬN ÁP DỤNG KẾ HOẠCH VÀO SPRINT\n- Ghi nhận chính thức danh mục công việc vào cơ sở dữ liệu\n- Dọn dẹp dữ liệu phiên tạm thời trong bộ nhớ đệm\n- Cập nhật bảng công việc của chu kỳ Sprint"]

    CommitDB --> Finish(["Kết thúc: Chu kỳ Sprint sẵn sàng thực thi"])
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

### 4.4. Phân Tách Tuyệt Đối Luồng Tư Duy (Native Thoughts) và Dữ Liệu Nội Dung (Clean Content Streaming)
Một vấn đề thường gặp ở các hệ thống AI phân rã công việc là luồng streaming trả về cấu trúc JSON bị "xả thô" (raw dump) các đoạn ngoặc nhọn `{ "storyPoints": 2, ... }` vào hộp hiển thị suy luận tư duy (Chain-of-Thought) của người dùng, gây rối mắt và làm giảm chất lượng trải nghiệm.

Hệ thống giải quyết triệt để thông qua bộ phân tách loại chunk trong [`AiSprintDecompositionService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiSprintDecompositionService.cs):
- **Chunk suy luận (`AiStreamChunkType.Thought`)**: Được chuyển tiếp trực tiếp vào `onThoughtDelta(chunk.DeltaThought)` để người dùng đọc được từng bước suy nghĩ tự nhiên của mô hình.
- **Chunk nội dung (`AiStreamChunkType.Content`)**: Chỉ được nối vào bộ đệm nội bộ `responseContentBuilder.Append(chunk.DeltaContent)`, **tuyệt đối không gửi vào `onThoughtDelta`**.
- Nếu mô hình không hỗ trợ Native Thinking, hệ thống tự động phát luồng suy luận tổng hợp (Synthesized Thought Milestones) bằng tiếng Việt mạch lạc, giúp người dùng nắm bắt tiến trình mà không thấy code thô.

---

## 5. Danh mục Tệp Mã Nguồn Tham Chiếu Chuyên Đề 2

| Thành phần | Vai trò kỹ thuật | Liên kết mã nguồn |
| :--- | :--- | :--- |
| **AiAgentController** | Tiếp nhận SSE, điều phối stream, tái tạo task và commit staging | [`AiAgentController.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Controllers/AiAgentController.cs#L25-L160) |
| **AiSprintDecompositionService** | Pipeline 5 bước thực thi Agentic Workflow, prompt engineering | [`AiSprintDecompositionService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiSprintDecompositionService.cs#L45-L250) |
| **AiTokenUsageTracker** | Ghi nhận token, latency và lịch sử các phiên phân tách | [`AiTokenUsageTracker.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiTokenUsageTracker.cs) |
| **MultiProviderAiModelClient** | Router gọi API mô hình ngoài (Gemini, Claude, OpenAI) | [`MultiProviderAiModelClient.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/MultiProviderAiModelClient.cs#L25-L120) |
| **SpecHubTools** | Công cụ trích xuất đặc tả yêu cầu phục vụ Grounding | [`SpecHubTools.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/McpServers/SpecHubTools.cs#L15-L80) |
| **ai-agent.service.ts** | Xử lý EventSource, đọc SSE stream theo từng chunk JSON | [`ai-agent.service.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/core/services/ai-agent.service.ts#L40-L150) |
| **ai-decompose-modal.ts** | Render giao diện tiến trình 5 bước, bảng công việc đề xuất | [`ai-decompose-modal.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/backlog/components/ai-decompose-modal/ai-decompose-modal.ts#L80-L280) |

