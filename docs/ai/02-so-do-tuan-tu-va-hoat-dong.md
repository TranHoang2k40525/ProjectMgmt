# CHUYÊN ĐỀ 2: SƠ ĐỒ TUẦN TỰ & SƠ ĐỒ HOẠT ĐỘNG CỦA MODULE AI AGENT

---

## 1. Tổng quan Kiến trúc Vận hành & Luồng Dữ liệu
Trong hệ thống Quản lý Dự án Scrum Agile, **Trợ Lý AI** không chỉ chạy một lệnh phân rã đơn lẻ không trạng thái, mà vận hành theo mô hình **Đoạn Hội thoại Có Trạng thái kết hợp Định tuyến Ý định & Quy trình Agentic 5 Giai đoạn (Stateful Hermes Agentic Workflow)**:

Module này đảm bảo 4 tiêu chí cốt lõi:
1. **Phản hồi tức thì thời gian thực (Realtime Transparency)**: Người dùng nhìn thấy rõ ràng AI đang chạy công cụ nào, tra cứu tài liệu nào, mất bao nhiêu mili-giây thông qua bảng công cụ thu gọn ngay phía trên câu trả lời.
2. **Định tuyến Ý định Thông minh & Rào chắn Ngoài luồng (Intent Classification & Domain Guardrail)**: Tự động phân biệt 8 nhóm ý định (*lập kế hoạch Sprint*, *lập kế hoạch kho Backlog*, *tinh chỉnh kế hoạch đang lưu*, *hỏi đáp số liệu dự án*, *xuất tệp Excel*, *đẩy vào Kho Backlog*, *áp dụng vào Sprint*, và *từ chối yêu cầu ngoài phạm vi quản lý dự án*).
3. **Ngăn chặn triệt để ảo giác (Zero Hallucination Grounding)**: AI buộc phải truy vấn dữ liệu thực tế thông qua các MCP Tools trước khi suy luận.
4. **Phê duyệt Staging Có Trạng thái (Stateful Redis Staging Memory)**: Cả bản đề xuất công việc lẫn toàn bộ lịch sử hội thoại nhiều lượt (`ConversationHistory`) được lưu bền vững theo `sessionId` trên Redis để người dùng thoải mái trao đổi tiếp, tinh chỉnh, xuất Excel, hoặc chọn **Đẩy vào Kho Backlog** / **Áp dụng vào Sprint**.

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

    %% Giai đoạn 1: Mở Modal & Tiếp nhận Hội thoại Có Trạng thái
    Note over SM,UI: GIAI ĐOẠN 1: KHỞI TẠO HOẶC TIẾP TỤC ĐOẠN HỘI THOẠI
    SM->>UI: Nhập yêu cầu hội thoại / Dán biên bản họp / Đính kèm tài liệu đặc tả
    SM->>UI: Gửi lượt trao đổi mới tới Trợ Lý AI
    UI->>ClientSvc: Khởi tạo luồng sự kiện kèm mã phiên hiện tại (nếu có)
    ClientSvc->>Gateway: Gửi yêu cầu hội thoại kèm mã phiên và chứng thực phiên
    
    %% Giai đoạn 2: Guardrail, Tải Trí nhớ Phiên & Định tuyến Ý định
    Note over Gateway,Pipeline: GIAI ĐOẠN 2: BẢO MẬT, ĐỌC TRÍ NHỚ REDIS & ĐỊNH TUYẾN Ý ĐỊNH
    Gateway->>Guard: Kiểm tra phân quyền truy cập và ranh giới dự án
    Guard-->>Gateway: Xác thực hợp lệ
    Gateway->>Staging: Tải lịch sử hội thoại và bản đề xuất cũ theo mã phiên
    Staging-->>Gateway: Trả về ngữ cảnh phiên hội thoại (nếu đã tồn tại)
    Gateway-->>ClientSvc: Mở luồng dữ liệu thời gian thực
    Gateway->>Pipeline: Chuyển giao yêu cầu kèm trí nhớ phiên cho Trợ Lý AI

    alt Yêu cầu ngoài phạm vi Quản lý Dự án
        Pipeline->>Staging: Bảo toàn bản đề xuất cũ và ghi nhận lượt từ chối
        Pipeline-->>UI: Trả lời từ chối lịch sự ngoài phạm vi công cụ
    else Yêu cầu Xuất tệp Excel Dự án
        Pipeline->>Staging: Ghi nhận lượt yêu cầu và gắn cờ kích hoạt xuất tệp
        Pipeline-->>UI: Trả lời xác nhận và kích hoạt tải tệp Excel dự án
    else Yêu cầu Lập kế hoạch / Tinh chỉnh / Hỏi đáp Dự án
        %% Giai đoạn 3: Tra cứu dữ liệu thực tế
        Note over Pipeline,Tools: GIAI ĐOẠN 3: THU THẬP DỮ LIỆU THỰC TẾ & QUY CHUẨN
        Pipeline-->>Gateway: Cập nhật bước 1: Tiếp nhận và phân loại ý định
        Gateway-->>ClientSvc: Báo cáo tiến độ bước 1
        ClientSvc-->>UI: Cập nhật trạng thái trên giao diện hội thoại

        Pipeline-->>Gateway: Cập nhật bước 2: Tra cứu tài liệu đặc tả & lịch sử dự án
        Gateway-->>ClientSvc: Báo cáo tiến độ bước 2
        
        Pipeline->>Tools: Tìm kiếm các phân đoạn đặc tả phù hợp
        Tools-->>Pipeline: Trả về trích đoạn yêu cầu kỹ thuật
        Pipeline-->>Gateway: Báo cáo kết quả tra cứu đặc tả

        Pipeline->>Tools: Lấy danh sách hạng mục tồn đọng trong Backlog
        Tools-->>Pipeline: Trả về danh sách hạng mục công việc
        Pipeline-->>Gateway: Báo cáo kết quả tra cứu Backlog

        Pipeline->>Tools: Lấy dữ liệu vận tốc và cấu hình đang áp dụng của dự án
        Tools-->>Pipeline: Trả về vận tốc nhóm, quy trình, mức ưu tiên, loại việc, nhãn
        Pipeline-->>Gateway: Báo cáo kết quả tính toán vận tốc & quy chuẩn

        %% Giai đoạn 4: Phân tích Năng lực Đội ngũ
        Pipeline-->>Gateway: Cập nhật bước 3: Đánh giá năng lực đội ngũ
        Gateway-->>ClientSvc: Báo cáo tiến độ bước 3
        Pipeline->>Tools: Phân tích kỹ năng và số giờ làm việc khả dụng
        Tools-->>Pipeline: Trả về cơ cấu chuyên môn và định mức giờ
        Pipeline-->>Gateway: Báo cáo kết quả phân tích năng lực

        %% Giai đoạn 5: Gọi Mô hình AI kèm Lịch sử Hội thoại
        Note over Pipeline,AI: GIAI ĐOẠN 4: SUY LUẬN ĐA LƯỢT VÀ PHÂN RÃ KẾ HOẠCH
        Pipeline-->>Gateway: Cập nhật bước 4: Suy luận câu trả lời và công việc
        Gateway-->>ClientSvc: Báo cáo tiến độ bước 4
        Pipeline->>ModelHub: Gửi yêu cầu kèm lịch sử hội thoại và bản đề xuất cũ
        ModelHub->>AI: Suy luận giữ nguyên ngữ cảnh hoặc phân rã công việc
        AI-->>ModelHub: Trả về câu trả lời tự nhiên và cấu trúc đề xuất
        ModelHub-->>Pipeline: Chuyển giao dữ liệu đề xuất đã chuẩn hóa

        %% Giai đoạn 6: Self-Check Critique & Lưu Staging
        Note over Pipeline,Staging: GIAI ĐOẠN 5: TỰ KIỂM TRA & CẬP NHẬT TRÍ NHỚ PHIÊN
        Pipeline-->>Gateway: Cập nhật bước 5: Tự kiểm tra & Lưu trữ bộ nhớ phiên
        Gateway-->>ClientSvc: Báo cáo tiến độ bước 5
        Pipeline->>Pipeline: Kiểm tra tiêu chuẩn nghiệm thu và cân bằng khối lượng
        Pipeline->>Staging: Cập nhật lịch sử hội thoại và bản đề xuất vào bộ nhớ phiên
        Staging-->>Pipeline: Xác nhận lưu trữ thành công
        Pipeline-->>Gateway: Báo cáo hoàn tất lượt hội thoại
        Gateway->>Tracker: Ghi nhận mức tiêu thụ tài nguyên và lịch sử phiên
        Tracker-->>Gateway: Xác nhận lưu vết thành công
        Gateway-->>ClientSvc: Gửi câu trả lời Trợ Lý AI, lịch sử và bản đề xuất cập nhật
        ClientSvc-->>UI: Hiển thị đoạn chat 2 phía (Trợ Lý AI bên trái, Tôi bên phải) và bảng công việc
    end

    %% Giai đoạn 7: Người dùng chỉnh sửa, Đẩy Kho Backlog hoặc Áp dụng Sprint
    Note over SM,DB: GIAI ĐOẠN 6: PHÊ DUYỆT VÀO KHO BACKLOG HOẶC SPRINT
    opt Người dùng xem trước tài liệu trích dẫn
        SM->>UI: Nhấp vào nguồn trích dẫn tài liệu
        UI->>UI: Mở ngăn kéo hiển thị nội dung đặc tả liên quan
    end

    opt Người dùng yêu cầu làm lại hoặc tinh chỉnh qua hội thoại
        SM->>UI: Gửi tin nhắn chỉnh sửa nối tiếp hoặc yêu cầu làm lại từng việc
        UI->>Gateway: Gửi yêu cầu tái cấu trúc dựa trên trí nhớ phiên
        Gateway->>Pipeline: Cập nhật công việc tương ứng và giữ nguyên các việc còn lại
        Pipeline->>Staging: Lưu bản cập nhật vào bộ nhớ phiên
        Pipeline-->>UI: Cập nhật công việc hiển thị ngay trong hội thoại
    end

    alt Người dùng chọn Đẩy vào Kho Backlog (qua nút hoặc hội thoại)
        SM->>UI: Chọn đẩy công việc đề xuất vào Kho Product Backlog
        UI->>Gateway: Gửi lệnh ghi nhận không gắn vào Sprint
        Gateway->>DB: Ghi nhận Epics, Tasks và Subtasks vào Kho Backlog (Sprint rỗng)
        DB-->>Gateway: Xác nhận ghi nhận thành công
        Gateway->>Staging: Dọn danh sách việc đã ghi và giữ lại lịch sử hội thoại
        Gateway-->>UI: Thông báo đẩy Kho Backlog thành công và giữ nguyên cuộc hội thoại
    else Người dùng chọn Áp dụng kế hoạch vào Sprint
        SM->>UI: Xác nhận áp dụng kế hoạch vào Sprint
        UI->>Gateway: Gửi lệnh phê duyệt kế hoạch Sprint chính thức
        Gateway->>Staging: Truy xuất bản kế hoạch đã qua chỉnh sửa
        Staging-->>Gateway: Trả về dữ liệu kế hoạch hoàn chỉnh
        Gateway->>DB: Ghi nhận chu kỳ Sprint, Epics, Tasks, Subtasks và di chuyển việc Backlog
        DB-->>Gateway: Xác nhận ghi nhận thành công
        Gateway->>Staging: Dọn danh sách việc đã ghi và lưu lịch sử xác nhận trong phiên
        Gateway-->>UI: Thông báo hoàn tất và chuyển hướng đến bảng điều phối Sprint
    end
```

---

## 3. Sơ đồ Hoạt động (Activity Diagram) của Pipeline Đa lượt Có Trạng thái

Quy trình xử lý nội bộ của [`AiSprintDecompositionService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiSprintDecompositionService.cs) được mô hình hóa theo sơ đồ hoạt động chặt chẽ dưới đây:

```mermaid
flowchart TD
    Start(["Bắt đầu: Tiếp nhận lượt hội thoại từ Người dùng"]) --> Step1["BƯỚC 1: TIẾP NHẬN, ĐỌC TRÍ NHỚ & ĐỊNH TUYẾN Ý ĐỊNH\n- Xác thực quyền hạn và ranh giới an toàn dự án\n- Tải lịch sử hội thoại và bản đề xuất cũ theo mã phiên\n- Phân loại ý định người dùng (Kế hoạch / Hỏi đáp / Xuất Excel / Ngoài luồng)\n- Trích xuất tài liệu tải lên (nếu có)"]

    Step1 --> GuardIntent{"Kiểm tra Ý định\nNghiệp vụ"}

    GuardIntent -- "Ngoài phạm vi Quản lý Dự án" --> RefuseOut["RÀO CHẮN NGOÀI LUỒNG\n- Từ chối lịch sự ngoài phạm vi công cụ\n- Bảo toàn nguyên vẹn bản đề xuất cũ trong bộ nhớ phiên"]
    RefuseOut --> DisplayUI

    GuardIntent -- "Xuất tệp Excel Dự án" --> ExportExcel["KÍCH HOẠT XUẤT EXCEL\n- Ghi nhận lượt xác nhận vào lịch sử hội thoại\n- Phát tín hiệu tải tệp Excel dự án về giao diện"]
    ExportExcel --> DisplayUI

    GuardIntent -- "Đẩy Kho Backlog hoặc Áp dụng Sprint qua Lệnh Chat" --> AutoTrigger["PHÁT LỆNH THỰC THI TRỰC TIẾP\n- Kiểm tra danh sách công việc đã có trong bộ nhớ phiên\n- Gắn cờ kích hoạt thao tác tương ứng cho giao diện"]
    AutoTrigger --> DisplayUI

    GuardIntent -- "Lập kế hoạch / Tinh chỉnh / Hỏi đáp Dự án" --> Step2["BƯỚC 2: TRA CỨU ĐẶC TẢ, BACKLOG & QUY CHUẨN\n- Tìm kiếm các đoạn tài liệu đặc tả kỹ thuật phù hợp\n- Quét các hạng mục tồn đọng trong Backlog\n- Đọc quy trình workflow, mức ưu tiên, loại việc, nhãn đang áp dụng\n- Tính toán vận tốc trung bình các chu kỳ trước"]

    Step2 --> Step3["BƯỚC 3: ĐÁNH GIÁ NĂNG LỰC ĐỘI NGŨ\n- Rà soát hồ sơ chuyên môn các thành viên\n- Xác định phân bổ kỹ năng và quỹ giờ làm việc khả dụng"]

    Step3 --> Step4["BƯỚC 4: SUY LUẬN ĐA LƯỢT & PHÂN RÃ KẾ HOẠCH\n- Nạp lịch sử hội thoại, bản đề xuất đang lưu và dữ liệu dự án\n- Sinh câu trả lời tự nhiên kèm cấu trúc công việc đề xuất hoặc giải đáp số liệu"]

    Step4 --> Check{"BƯỚC 5: TỰ KIỂM TRA CHẤT LƯỢNG\n- Khối lượng, nhân sự, nghiệm thu và trích dẫn hợp chuẩn?"}

    Check -- "Chưa tối ưu" --> AutoFix["Cân bằng lại khối lượng & gắn ghi chú lưu ý"]
    AutoFix --> SaveStaging
    Check -- "Đạt chuẩn" --> SaveStaging["CẬP NHẬT TRÍ NHỚ PHIÊN TRÊN BỘ NHỚ ĐỆM\n- Lưu cả câu trả lời hội thoại, lịch sử trao đổi & bản đề xuất\n- Ghi nhận chỉ số tài nguyên và phát bản đề xuất về giao diện"]

    SaveStaging --> DisplayUI["GIAO DIỆN HỘI THOẠI 2 PHÍA TRỢ LÝ AI (TRÁI) & TÔI (PHẢI)\n- Câu trả lời liền mạch, không tách thẻ vụn, có bảng công việc tích hợp\n- Cho phép chỉnh sửa trực tiếp hoặc chat tiếp trong cùng phiên"]

    DisplayUI --> UserAction{"Thao tác tiếp theo của Người điều phối"}

    UserAction -- "Tiếp tục nhắn tin trao đổi / yêu cầu sửa" --> Start
    UserAction -- "Xem tài liệu đặc tả" --> OpenDrawer["Mở ngăn kéo xem phân đoạn tài liệu trích dẫn\n(Không làm gián đoạn đoạn hội thoại)"]
    OpenDrawer --> DisplayUI

    UserAction -- "Chỉnh sửa trực tiếp" --> EditItem["Hiệu chỉnh tiêu đề, điểm ước lượng, người phụ trách"]
    EditItem --> DisplayUI

    UserAction -- "Đẩy vào Kho Backlog" --> CommitBacklog["GHI VÀO KHO PRODUCT BACKLOG\n- Tạo Epics, Tasks, Subtasks với Sprint rỗng\n- Xóa danh sách việc vừa ghi, giữ nguyên lịch sử hội thoại"]
    CommitBacklog --> DisplayUI

    UserAction -- "Áp dụng vào Sprint" --> CommitDB["XÁC NHẬN ÁP DỤNG KẾ HOẠCH VÀO SPRINT\n- Tạo Sprint mới, di chuyển Backlog không trùng lặp\n- Xóa danh sách việc vừa ghi và hoàn tất phiên lập kế hoạch"]

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
| **AiAgentController** | Tiếp nhận SSE kèm `sessionId`, điều phối stream, tái tạo task và commit Sprint/Kho Backlog | [`AiAgentController.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Controllers/AiAgentController.cs#L25-L160) |
| **AiSprintDecompositionService** | Trợ Lý AI có trạng thái Redis: phân loại ý định, rào chắn ngoài luồng, pipeline 5 bước & prompt engineering | [`AiSprintDecompositionService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiSprintDecompositionService.cs#L45-L250) |
| **AiTokenUsageTracker** | Ghi nhận token, latency và lịch sử các phiên Trợ Lý AI | [`AiTokenUsageTracker.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiTokenUsageTracker.cs) |
| **MultiProviderAiModelClient** | Router gọi API mô hình ngoài (Gemini, Claude, OpenAI) | [`MultiProviderAiModelClient.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/MultiProviderAiModelClient.cs#L25-L120) |
| **SpecHubTools** | Công cụ trích xuất đặc tả yêu cầu phục vụ Grounding | [`SpecHubTools.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/McpServers/SpecHubTools.cs#L15-L80) |
| **ai-agent.service.ts** | Xử lý SSE theo `sessionId`, đẩy Kho Backlog, áp dụng Sprint và 401 refresh token | [`ai-agent.service.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/core/services/ai-agent.service.ts#L40-L150) |
| **ai-decompose-modal.ts** | Giao diện **Trợ Lý AI** hội thoại 2 phía, bảng công việc tích hợp, xuất Excel & khôi phục Redis | [`ai-decompose-modal.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/backlog/components/ai-decompose-modal/ai-decompose-modal.ts#L80-L280) |

