# CHUYÊN ĐỀ 2: SƠ ĐỒ TUẦN TỰ & SƠ ĐỒ HOẠT ĐỘNG CỦA MODULE AI AGENT

---

## 1. Tổng quan Kiến trúc Vận hành & Luồng Dữ liệu
Trong hệ thống Quản lý Dự án Scrum Agile, **Trợ Lý AI** vận hành theo mô hình **Đoạn Hội thoại Có Trạng thái kết hợp Định tuyến Ngữ cảnh Chọn lọc & Quy trình Agentic (Stateful Hermes Agentic Workflow)**:

Module này đảm bảo 5 tiêu chí cốt lõi:
1. **Minh bạch Thời gian thực & Mặc định Đóng Gọn (Default-Collapsed Thinking Log)**: Người dùng nhìn thấy trạng thái AI đang suy nghĩ theo từng giây (`Đang suy nghĩ · X.Xs — ...`) và kết quả hoàn tất tông sáng gọn nhẹ (`Đã suy nghĩ xong trong X.Xs · N thao tác`). Mặc định khung suy nghĩ **luôn đóng** để giữ không gian hội thoại sạch sẽ; khi mở rộng sẽ hiện rõ từng bước thực thi (`actionVerb`, `target`, `durationMs`, chi tiết mở/đóng từng dòng) và các mảnh trí nhớ `#mem-X` vừa nạp.
2. **Định tuyến Ý định Thông minh & Gọi MCP Có Chọn Lọc (Selective MCP Routing)**: Tự động phân loại 8 nhóm ý định và chỉ kích hoạt các công cụ MCP thực sự cần thiết cho nội dung câu hỏi (0 MCP cho lời chào/hỏi khả năng; chỉ `memory_recall` cho câu hỏi nối tiếp `#mem-X`; tập công cụ khu trú cho hỏi đáp dự án hẹp; đầy đủ công cụ khi lập kế hoạch Sprint/Backlog).
3. **Ngăn chặn triệt để ảo giác (Zero Hallucination Grounding)**: Khi cần số liệu dự án, AI buộc phải truy vấn dữ liệu thực tế từ hệ thống trước khi suy luận.
4. **Phê duyệt Staging & Lưu trữ Bền vững Kết hợp Redis + MySQL**: Bản đề xuất và toàn bộ lịch sử hội thoại, log thao tác, các mảnh trí nhớ đánh chỉ mục (`#mem-1..#mem-n`) được giữ nóng trên Redis đồng thời ghi bền vững vào cơ sở dữ liệu theo khóa phạm vi **`(OrganizationId, ProjectId, UserId)`**.
5. **Tạo mới Không Phá Hủy & Xóa Phiên Có Kiểm Soát**: Nút **"Tạo mới"** bắt đầu đoạn chat mới mà không xóa phiên cũ; người dùng có thể mở lại phiên cũ (kể cả khi Redis đã hết hạn nhờ tự động nạp lại từ MySQL) hoặc xóa chủ động phiên cũ khi cần.

---

## 2. Sơ đồ Tuần tự Toàn diện (Sequence Diagram)

Sơ đồ tuần tự dưới đây mô tả chi tiết luồng xử lý từ thời điểm người dùng gửi thông điệp trong Trợ Lý AI, định tuyến ngữ cảnh chọn lọc, nạp trí nhớ đánh chỉ mục, hiển thị nhật ký suy nghĩ mặc định đóng cho đến khi lưu phiên bền vững hoặc ghi nhận vào Sprint/Backlog (tuân thủ chuẩn kiến trúc không lộ tên thành phần mã nguồn):

```mermaid
sequenceDiagram
    autonumber
    actor SM as Người điều phối (Scrum Master / PM)
    participant UI as Giao diện Hội thoại Trợ Lý AI
    participant ClientSvc as Dịch vụ Kết nối Phía Client
    participant Gateway as Bộ Điều phối Phiên & Bảo mật AI
    participant MemEngine as Bộ Lập kế hoạch Ngữ cảnh & Chỉ mục Trí nhớ
    participant Pipeline as Bộ Suy luận Đa lượt Trợ Lý AI
    participant Guard as Rào chắn Khóa Phạm vi Tổ chức - Dự án - User
    participant Tools as Bộ Công cụ Dữ liệu Dự án Chọn lọc
    participant AI as Dịch vụ Trí tuệ Nhân tạo
    participant HotCache as Bộ nhớ đệm Tốc độ cao
    participant DB as Kho Dữ liệu & Lịch sử Phiên Bền vững

    %% Giai đoạn 1: Tiếp nhận & Khóa phạm vi
    Note over SM,UI: GIAI ĐOẠN 1: KHỞI TẠO HOẶC TIẾP TỤC ĐOẠN HỘI THOẠI
    SM->>UI: Nhập câu hỏi / Yêu cầu / Đính kèm tài liệu (hoặc bấm "Tạo mới")
    UI->>ClientSvc: Mở luồng sự kiện kèm mã phiên hiện có (mặc định khối Suy nghĩ đóng)
    ClientSvc->>Gateway: Gửi yêu cầu hội thoại kèm mã phiên và thẻ xác thực

    %% Giai đoạn 2: Khóa phạm vi & Nạp trí nhớ kép Hot Cache + DB
    Note over Gateway,DB: GIAI ĐOẠN 2: KHÓA PHẠM VI & KHÔI PHỤC TRÍ NHỚ KÉP
    Gateway->>Guard: Kiểm tra bộ ba (Tổ chức, Dự án, Người dùng) và lọc tiêm nhiễm
    Guard-->>Gateway: Hợp lệ (Từ chối ngay nếu sai lệch phạm vi)
    Gateway->>HotCache: Đọc ngữ cảnh phiên đang nóng theo mã phiên
    alt Bộ nhớ đệm nóng đã hết hạn hoặc trống (Cold Cache)
        Gateway->>DB: Truy vấn phiên hội thoại & chỉ mục trí nhớ đã khóa phạm vi
        DB-->>Gateway: Trả về lịch sử hội thoại, đề xuất và danh mục mảnh trí nhớ
        Gateway->>HotCache: Nạp bù ngữ cảnh phiên lên bộ nhớ đệm nóng
    end
    Gateway-->>ClientSvc: Mở luồng sự kiện thời gian thực

    %% Giai đoạn 3: Định tuyến ý định & Selective MCP
    Note over Gateway,Tools: GIAI ĐOẠN 3: ĐỊNH TUYẾN Ý ĐỊNH & GỌI CÔNG CỤ CÓ CHỌN LỌC
    Gateway->>Pipeline: Chuyển yêu cầu kèm lịch sử phiên và danh mục mảnh trí nhớ
    Pipeline->>MemEngine: Phân loại ý định & xác định tập công cụ cần thiết + mảnh trí nhớ phù hợp
    MemEngine-->>Pipeline: Trả về kế hoạch thực thi (0 công cụ / Chỉ nạp trí nhớ / Tập công cụ mục tiêu)

    alt Chào hỏi hoặc hỏi khả năng trợ lý
        Note over Pipeline,AI: Không gọi bất kỳ công cụ dữ liệu ngoài nào
        Pipeline-->>UI: Phát sự kiện bỏ qua tra cứu thừa & Suy luận trực tiếp
    else Câu hỏi nối tiếp hoặc nhắc lại nội dung phiên (#mem-X)
        Pipeline->>MemEngine: Truy xuất đúng các mảnh trí nhớ được đánh chỉ mục liên quan
        MemEngine-->>Pipeline: Trả về tóm tắt & nội dung chi tiết mảnh trí nhớ (#mem-X)
        Pipeline-->>UI: Ghi nhận thao tác nạp trí nhớ vào nhật ký suy nghĩ (đóng mặc định)
    else Yêu cầu ngoài phạm vi Quản lý Dự án
        Pipeline-->>UI: Từ chối lịch sự ngoài phạm vi công cụ và bảo toàn đề xuất cũ
    else Hỏi đáp dự án hẹp hoặc Lập kế hoạch / Tinh chỉnh Sprint - Backlog
        loop Chỉ gọi đúng các công cụ nằm trong tập mục tiêu đã chọn
            Pipeline->>Tools: Truy vấn công cụ dữ liệu dự án cần thiết
            Tools-->>Pipeline: Trả về dữ liệu thực tế & quy chuẩn đang áp dụng
            Pipeline-->>UI: Cập nhật dòng thao tác chi tiết vào Nhật ký Suy nghĩ
        end
    end

    %% Giai đoạn 4: Suy luận, đánh chỉ mục trí nhớ mới & lưu bền vững
    Note over Pipeline,DB: GIAI ĐOẠN 4: SUY LUẬN, ĐÁNH CHỈ MỤC TRÍ NHỚ & LƯU BỀN VỮNG
    Pipeline->>AI: Suy luận câu trả lời tập trung đúng trọng tâm câu hỏi
    AI-->>Pipeline: Trả về câu trả lời kèm kế hoạch đề xuất (nếu là luồng lập kế hoạch)
    Pipeline->>MemEngine: Đóng gói và đánh chỉ mục mảnh trí nhớ mới (#mem-k) kèm từ khóa tìm kiếm
    Pipeline->>HotCache: Lưu trạng thái phiên mới nhất vào bộ nhớ đệm nóng
    Pipeline->>DB: Lưu bền vững phiên hội thoại, lịch sử thao tác suy nghĩ & mảnh trí nhớ mới
    Pipeline-->>UI: Phát sự kiện hoàn tất kèm tóm tắt suy nghĩ (mặc định thu gọn, nhấp để mở)

    %% Giai đoạn 5: Hậu tương tác
    opt Người dùng mở Lịch sử phiên để xem lại, cuộn tin cũ hoặc xóa phiên cũ
        SM->>UI: Chọn phiên cũ từ danh sách (hoặc bấm Xóa phiên)
        UI->>Gateway: Yêu cầu nạp 12 tin mới nhất (hoặc tải thêm trang cũ hơn / xóa phiên)
        Gateway->>DB: Nạp phân trang ngược hoặc xóa bản ghi phiên thuộc đúng phạm vi sở hữu
        DB-->>UI: Trả về trang tin nhắn & tự động cuộn ngay xuống cuối đoạn chat (Thinking luôn đóng)
    end

    %% Giai đoạn 6: Người dùng chỉnh sửa, Đẩy Kho Backlog hoặc Áp dụng Sprint
    Note over SM,DB: GIAI ĐOẠN 6: PHÊ DUYỆT VÀO KHO BACKLOG HOẶC SPRINT
    opt Người dùng xem trước tài liệu trích dẫn
        SM->>UI: Nhấp vào nguồn trích dẫn tài liệu
        UI->>UI: Mở ngăn kéo hiển thị nội dung đặc tả liên quan
    end

    opt Người dùng yêu cầu làm lại hoặc tinh chỉnh qua hội thoại
        SM->>UI: Gửi tin nhắn chỉnh sửa nối tiếp hoặc yêu cầu làm lại từng việc
        UI->>Gateway: Gửi yêu cầu tái cấu trúc dựa trên trí nhớ phiên
        Gateway->>Pipeline: Cập nhật công việc tương ứng và giữ nguyên các việc còn lại
        Pipeline->>HotCache: Cập nhật bản mới vào bộ nhớ đệm nóng
        Pipeline->>DB: Lưu bền vững phiên hội thoại và mảnh trí nhớ mới
        Pipeline-->>UI: Cập nhật công việc hiển thị ngay trong hội thoại
    end

    alt Người dùng chọn Đẩy vào Kho Backlog (qua nút hoặc hội thoại)
        SM->>UI: Chọn đẩy công việc đề xuất vào Kho Product Backlog
        UI->>Gateway: Gửi lệnh ghi nhận không gắn vào Sprint
        Gateway->>DB: Ghi nhận Epics, Tasks và Subtasks vào Kho Backlog (Sprint rỗng)
        DB-->>Gateway: Xác nhận ghi nhận thành công
        Gateway->>HotCache: Dọn danh sách việc đã ghi và giữ lại lịch sử hội thoại
        Gateway->>DB: Lưu tin nhắn xác nhận vào lịch sử phiên hội thoại
        Gateway-->>UI: Thông báo đẩy Kho Backlog thành công và giữ nguyên cuộc hội thoại
    else Người dùng chọn Áp dụng kế hoạch vào Sprint
        SM->>UI: Xác nhận áp dụng kế hoạch vào Sprint
        UI->>Gateway: Gửi lệnh phê duyệt kế hoạch Sprint chính thức
        Gateway->>HotCache: Truy xuất bản kế hoạch đã qua chỉnh sửa (hoặc khôi phục từ DB)
        HotCache-->>Gateway: Trả về dữ liệu kế hoạch hoàn chỉnh
        Gateway->>DB: Ghi nhận chu kỳ Sprint, Epics, Tasks, Subtasks và di chuyển việc Backlog
        DB-->>Gateway: Xác nhận ghi nhận thành công
        Gateway->>HotCache: Dọn danh sách việc đã ghi và lưu lịch sử xác nhận trong phiên
        Gateway->>DB: Cập nhật trạng thái phiên hội thoại đã commit trong CSDL
        Gateway-->>UI: Thông báo hoàn tất và chuyển hướng đến bảng điều phối Sprint
    end
```

---

## 3. Sơ đồ Hoạt động (Activity Diagram) của Pipeline Đa lượt Có Trạng thái & Định tuyến MCP Chọn lọc

Quy trình xử lý nội bộ của [`AiSprintDecompositionService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiSprintDecompositionService.cs) kết hợp [`AiIndexedMemoryEngine.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiIndexedMemoryEngine.cs) được mô hình hóa theo sơ đồ hoạt động dưới đây:

```mermaid
flowchart TD
    Start(["Bắt đầu: Tiếp nhận lượt hội thoại từ Người dùng (hoặc mở lại phiên cũ)"]) --> Step1["BƯỚC 1: KHÓA PHẠM VI, NẠP TRÍ NHỚ KÉP & PHÂN LOẠI Ý ĐỊNH\n- Xác thực bộ ba (Tổ chức, Dự án, Người dùng)\n- Tải phiên từ Bộ nhớ đệm Nóng; nếu hết hạn thì nạp bù từ CSDL\n- Trích xuất ràng buộc xuyên suốt phiên & kế thừa chủ đề lượt trước cho câu hỏi rút gọn\n- Phân loại ý định (nhận diện câu hỏi nối tiếp, hỏi đáp mệnh lệnh & rào chắn ngoài luồng)"]

    Step1 --> GuardIntent{"Kiểm tra Ý định\n& Kế hoạch Thực thi"}

    GuardIntent -- "Ngoài phạm vi Quản lý Dự án" --> RefuseOut["RÀO CHẮN NGOÀI LUỒNG\n- Từ chối lịch sự ngoài phạm vi công cụ\n- Bảo toàn nguyên vẹn đề xuất và lịch sử phiên"]
    RefuseOut --> SaveDual

    GuardIntent -- "Xuất tệp Excel Theo Ngữ Cảnh" --> ExportExcel["PHÂN GIẢI PHẠM VI & XUẤT EXCEL CHUẨN HỆ THỐNG\n- Đối chiếu đúng Sprint, Backlog, việc lẻ, chi tiết, bộ lọc hay đề xuất\n- Từ chối trung thực nếu dữ liệu không tồn tại/trống; gắn đặc tả xuất vào lượt chat"]
    ExportExcel --> SaveDual

    GuardIntent -- "Đẩy Kho Backlog hoặc Áp dụng Sprint qua Chat" --> AutoTrigger["PHÁT LỆNH THỰC THI TRỰC TIẾP\n- Kiểm tra danh sách công việc đã có trong phiên\n- Gắn cờ kích hoạt thao tác tương ứng"]
    AutoTrigger --> SaveDual

    GuardIntent -- "Chào hỏi / Hỏi khả năng" --> ZeroMcp["LUỒNG TRỰC TIẾP (0 CÔNG CỤ DỮ LIỆU)\n- Không gọi công cụ dự án thừa\n- Phản hồi súc tích đúng trọng tâm câu hỏi"]
    ZeroMcp --> SaveDual

    GuardIntent -- "Câu hỏi nối tiếp / Đại từ quy chiếu / Nhắc #mem-X" --> RecallOnly["TRUY XUẤT CHỈ MỤC TRÍ NHỚ & ĐỀ XUẤT ĐANG STAGING\n- Chỉ đọc đúng mảnh trí nhớ (#mem-X) + tóm tắt mô tả/tiêu chí việc đang staging\n- Xây dựng chuỗi hội thoại đa lượt thực thụ kèm thỏa thuận phiên trước đó"]
    RecallOnly --> Step4Focus["Suy luận câu trả lời tập trung, giải mã đúng đại từ theo mạch phiên"]
    Step4Focus --> SaveDual

    GuardIntent -- "Hỏi đáp Hẹp hoặc Lập kế hoạch Sprint / Backlog" --> Step2["BƯỚC 2: GỌI CÔNG CỤ DỮ LIỆU CÓ CHỌN LỌC\n- Hỏi đáp hẹp: Chỉ gọi 1-2 công cụ mục tiêu\n- Lập kế hoạch: Gọi đặc tả, Backlog, vận tốc, quy chuẩn & năng lực\n- Ghi nhật ký từng bước kèm thời gian thực thi"]

    Step2 --> Step4["BƯỚC 3: SUY LUẬN ĐÚNG TRỌNG TÂM TRÊN CHUỖI ĐA LƯỢT\n- Truyền chuỗi hội thoại luân phiên Người dùng - Trợ lý kèm ràng buộc đã chốt\n- Sửa đổi vi phân hoặc bóc tách kế hoạch & bảo toàn liên kết nguồn Backlog"]

    Step4 --> SaveDual["BƯỚC 4: ĐÁNH CHỈ MỤC TRÍ NHỚ MỚI & LƯU KÉP\n- Đóng gói ngữ cảnh lượt mới thành mảnh #mem-k\n- Lưu đồng thời vào Bộ nhớ đệm Nóng và CSDL (khóa 3 chiều)"]

    SaveDual --> DisplayUI["GIAO DIỆN TRỢ LÝ AI (TRÁI) & TÔI (PHẢI)\n- Khối Suy nghĩ tông sáng mặc định LUÔN ĐÓNG (nhấp để xem từng dòng thao tác)\n- Thanh tiêu đề hỗ trợ Tạo mới (không xóa phiên cũ) & Xóa phiên trong Lịch sử"]

    DisplayUI --> UserAction{"Thao tác tiếp theo của Người điều phối"}

    UserAction -- "Tiếp tục nhắn tin trong cùng phiên" --> Start
    UserAction -- "Bấm nút Tạo mới" --> NewBlank["Mở cuộc hội thoại trống mới (giữ nguyên toàn bộ phiên cũ trong CSDL)"]
    UserAction -- "Mở lại hoặc Xóa phiên cũ trong Lịch sử" --> ManageHist["Khôi phục ngữ cảnh phiên từ CSDL hoặc Xóa hẳn phiên thuộc quyền sở hữu"]
    UserAction -- "Đẩy Kho Backlog / Áp dụng Sprint" --> CommitDB["Ghi công việc vào CSDL chính & giữ mạch hội thoại liền mạch"]
    CommitDB --> Finish(["Kết thúc lượt làm việc"])
```

---

## 4. Giao thức Server-Sent Events (SSE) & Cơ chế Truyền dữ liệu Thời gian thực

### 4.1. Tại sao lựa chọn Server-Sent Events (SSE) thay vì WebSocket hay Polling?
Trong tác vụ AI Agent suy luận dài (từ 5 đến 45 giây tùy quy mô tài liệu):
- **Short/Long Polling**: Tạo áp lực cực lớn lên Web Server do gửi hàng chục HTTP request thừa thãi, gây lãng phí tài nguyên và độ trễ cao.
- **WebSocket**: Quá phức tạp cho tác vụ một chiều (server truyền tiến trình về client), đòi hỏi duy trì kết nối TCP song công toàn phần (Full-duplex) và dễ bị proxy/firewall doanh nghiệp chặn.
- **Server-Sent Events (SSE - `text/event-stream`)**: Chuẩn HTTP/1.1 và HTTP/2 gốc của trình duyệt, truyền tải một chiều nhẹ nhàng, hỗ trợ streaming văn bản mượt mà, vượt qua tường lửa dễ dàng và tích hợp sẵn cơ chế auto-reconnect.

### 4.2. Cấu trúc Message SSE & Metadata Thao tác Suy nghĩ
Mỗi gói tin SSE phát đi từ [`AiAgentController.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Controllers/AiAgentController.cs) được chuẩn hóa theo [`AiSsePayloads.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Dtos/Ai/AiSsePayloads.cs), trong đó sự kiện gọi công cụ bổ sung `ActionVerb`, `TargetLabel` và `DetailPreview` để hiển thị trực quan trong khung **Thinking** khi người dùng mở rộng:

```http
HTTP/1.1 200 OK
Content-Type: text/event-stream
Cache-Control: no-cache
Connection: keep-alive
Transfer-Encoding: chunked

data: {"type":"step","step":1,"title":"Phân loại ý định & lập kế hoạch ngữ cảnh","status":"completed"}

data: {"type":"tool_call","toolName":"memory_recall","actionVerb":"Recalled","targetLabel":"#mem-1","summary":"Nạp mảnh trí nhớ #mem-1 về quy trình Workflow","detailPreview":"Quy trình 4 bước: Open -> In Dev -> QA Testing -> Released","latencyMs":4}

data: {"type":"tool_call","toolName":"project_get_active_config","actionVerb":"Analyzed","targetLabel":"Cấu hình Workflow & Priority","summary":"Đọc cấu hình Workflow đang áp dụng của dự án QLDA","detailPreview":"Priorities: Urgent, High, Normal, Low | InitialStatus: Open","latencyMs":12}

data: {"type":"step","step":4,"title":"Đang tổng hợp câu trả lời tập trung...","status":"running"}

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
- **Chunk suy luận (`AiStreamChunkType.Thought`)**: Được chuyển tiếp trực tiếp vào `onThoughtDelta(chunk.DeltaThought)` để người dùng đọc được từng bước suy nghĩ tự nhiên của mô hình trong hộp Thinking khi mở rộng.
- **Chunk nội dung (`AiStreamChunkType.Content`)**: Chỉ được nối vào bộ đệm nội bộ `responseContentBuilder.Append(chunk.DeltaContent)`, **tuyệt đối không gửi vào `onThoughtDelta`**.
- **Nhật ký Trace & Tóm tắt Suy nghĩ (`TraceItems` & `ThinkingSummary`)**: Sau mỗi lượt, toàn bộ thao tác thực tế và tóm tắt được lưu kèm vào từng `AiConversationTurnDto` trong MySQL/Redis để dù khôi phục lại phiên cũ, người dùng vẫn xem lại được quá trình làm việc của Agent (mặc định luôn ở trạng thái đóng).

---

## 5. Danh mục Tệp Mã Nguồn Tham Chiếu Chuyên Đề 2

| Thành phần | Vai trò kỹ thuật | Liên kết mã nguồn |
| :--- | :--- | :--- |
| **AiAgentController** | Tiếp nhận SSE theo `sessionId`, khóa phạm vi 3 chiều, khôi phục/xóa phiên & commit Sprint/Kho Backlog | [`AiAgentController.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Controllers/AiAgentController.cs) |
| **AiSprintDecompositionService** | Trợ Lý AI có trạng thái: định tuyến ý định, Selective MCP, Thinking trace & prompt engineering | [`AiSprintDecompositionService.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiSprintDecompositionService.cs) |
| **AiExcelExportResolver** | Phân giải thông minh phạm vi xuất Excel (Sprint, Backlog, Việc lẻ, Chi tiết, Đề xuất, Bộ lọc) & kiểm tra tồn tại thực thể | [`AiExcelExportResolver.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiExcelExportResolver.cs) |
| **ExcelDataService** | Bộ sinh OpenXML `.xlsx` bảng sạch chuẩn ProjectMgmt (màu trạng thái động + bảng Chú thích + hồ sơ chi tiết) | [`excel-data.service.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/core/services/excel-data.service.ts) |
| **AiIndexedMemoryEngine** | Lập kế hoạch gọi MCP chọn lọc và truy xuất chỉ mục trí nhớ `#mem-1..#mem-n` | [`AiIndexedMemoryEngine.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiIndexedMemoryEngine.cs) |
| **AiConversationSessionStore** | Lưu trữ & khôi phục phiên đa lượt trong MySQL kèm khóa `(OrganizationId, ProjectId, UserId)` | [`AiConversationSessionStore.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiConversationSessionStore.cs) |
| **AiSsePayloads & Proposal DTOs** | Lược đồ SSE `tool_call` (`ActionVerb`, `TargetLabel`, `DetailPreview`) và `AiTurnTraceItemDto` | [`AiSsePayloads.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Dtos/Ai/AiSsePayloads.cs) |
| **AiTokenUsageTracker** | Ghi nhận token, latency và thống kê lịch sử các phiên Trợ Lý AI | [`AiTokenUsageTracker.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiTokenUsageTracker.cs) |
| **MultiProviderAiModelClient** | Router gọi API mô hình ngoài (Gemini, Claude, OpenAI) | [`MultiProviderAiModelClient.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/MultiProviderAiModelClient.cs#L25-L120) |
| **SpecHubTools** | Công cụ trích xuất đặc tả yêu cầu phục vụ Grounding | [`SpecHubTools.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/McpServers/SpecHubTools.cs#L15-L80) |
| **ai-agent.service.ts** | Xử lý SSE theo `sessionId`, đẩy Kho Backlog, áp dụng Sprint, **xóa phiên cũ (`deleteSession`)** & 401 refresh | [`ai-agent.service.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/core/services/ai-agent.service.ts) |
| **ai-decompose-modal.ts** | Giao diện **Trợ Lý AI** hội thoại 2 phía, **Thinking mặc định luôn đóng**, nút **Tạo mới**, khôi phục & xóa phiên | [`ai-decompose-modal.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/backlog/components/ai-decompose-modal/ai-decompose-modal.ts) |

