# CHUYÊN ĐỀ 4: CƠ CHẾ RAG & GROUNDING VỚI KHO TÀI LIỆU ĐẶC TẢ SPEC HUB

---

## 1. Khái niệm RAG & Giải quyết Triệt để Ảo giác (Hallucination)

### 1.1. Hiện tượng Ảo giác trong Mô hình Ngôn ngữ Lớn (LLM)
Khi yêu cầu một mô hình AI thông thường (Zero-shot Prompting) lập kế hoạch cho một Sprint, AI thường mắc phải các lỗi nghiêm trọng:
- Tự "bịa" ra các thư viện công nghệ không có trong dự án (ví dụ: dự án dùng Angular và .NET 8, nhưng AI lại sinh task cấu hình React và Django).
- Đặt tiêu chí nghiệm thu chung chung, không gắn liền với các ràng buộc bảo mật hay quy tắc nghiệp vụ nội bộ của tổ chức.
- Không biết được tiến độ thực tế: việc nào đã làm ở Sprint trước, việc nào còn dở dang.

### 1.2. Giải pháp RAG (Retrieval-Augmented Generation)
**RAG (Tạo sinh có Tăng cường Truy xuất)** là kỹ thuật kết hợp giữa:
1. **Truy xuất thông tin (Information Retrieval)**: Tìm kiếm các đoạn tài liệu đặc tả thực tế (SRS, BRD, ghi chú cuộc họp, bảng phân công) từ kho lưu trữ dự án.
2. **Tăng cường ngữ cảnh (Augmented Context)**: Nạp các đoạn trích này vào câu lệnh chỉ đạo (System/User Prompt) của AI.
3. **Tạo sinh có căn cứ (Grounded Generation)**: Buộc mô hình AI chỉ được phép lập luận và phân rã công việc dựa trên các sự thật đã được kiểm chứng.

```mermaid
flowchart LR
    Doc["Kho Tài liệu Đặc tả Kỹ thuật\n(Đa dạng định dạng tệp lưu trữ)"] --> Extractor["Bộ Trích xuất Văn bản Đa Định dạng\n(Bóc tách cấu trúc nội dung và bảng)"]
    MeetingNotes["Ghi chú Cuộc họp & Yêu cầu Người dùng\n(Văn bản dán trực tiếp hoặc tải lên)"] --> Extractor
    
    Extractor --> VectorStore["Cơ chế Tìm kiếm Ngữ nghĩa & Toàn văn\n(Tra cứu theo độ tương đồng và từ khóa)"]
    VectorStore --> ContextPrompt["Ngữ cảnh Thực tế Đã Xác thực\n(Đầy đủ trích dẫn nguồn gốc tài liệu)"]
    ContextPrompt --> LLM["Mô hình Trí tuệ Nhân tạo Suy luận\n(Phân tích và đối chiếu yêu cầu)"]
    LLM --> VerifiedTasks["Mục tiêu, Danh mục Công việc\n(Đính kèm trích dẫn tài liệu 100%)"]
```

---

## 2. Bộ Trích xuất Văn bản Vạn năng (AiDocumentTextExtractor)

Để phục vụ RAG đa định dạng, lớp [`AiDocumentTextExtractor.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiDocumentTextExtractor.cs) xử lý bóc tách nội dung từ tất cả các định dạng tài liệu phổ biến trong môi trường doanh nghiệp:

### 2.1. Sơ đồ Xử lý Đa Định dạng

```mermaid
flowchart TD
    InputFile["Tệp tải lên hoặc Luồng dữ liệu"] --> CheckSize{"Kiểm tra kích thước tệp quy định"}
    CheckSize -- "Vượt ngưỡng an toàn" --> ErrSize["Báo lỗi dung lượng vượt giới hạn"]
    CheckSize -- "Hợp lệ" --> DetectExt{"Phân loại Định dạng Tài liệu"}

    DetectExt -- "Tài liệu Văn bản Soạn thảo" --> ParseDocx["XỬ LÝ VĂN BẢN SOẠN THẢO\n- Phân tích cấu trúc đoạn văn bản và tiêu đề\n- Chuyển đổi bảng biểu thành dạng cột có cấu trúc"]
    
    DetectExt -- "Bảng tính Dữ liệu" --> ParseXlsx["XỬ LÝ BẢNG TÍNH DỮ LIỆU\n- Đọc danh mục dữ liệu ô và chuỗi dùng chung\n- Chuyển đổi bảng biểu thành dữ liệu hàng và cột có cấu trúc"]

    DetectExt -- "Tài liệu Kỹ thuật Định dạng Cố định" --> ParsePdf["XỬ LÝ TÀI LIỆU ĐỊNH DẠNG CỐ ĐỊNH\n- Quét các khối luồng nội dung tài liệu\n- Giải mã và bóc tách các dòng văn bản kỹ thuật"]

    DetectExt -- "Văn bản Thuần & Cấu trúc Mở" --> ParseText["XỬ LÝ VĂN BẢN CẤU TRÚC MỞ\n- Đọc chuẩn mã hóa quốc tế\n- Giữ nguyên cấu trúc phân cấp tiêu đề và danh mục"]

    ParseDocx --> Combine["Hợp nhất với Yêu cầu và Ghi chú cuộc họp"]
    ParseXlsx --> Combine
    ParsePdf --> Combine
    ParseText --> Combine

    Combine --> FinalText["Ngữ cảnh Hoàn chỉnh\n(Đưa vào Động cơ Phân rã Kế hoạch)"]
```

### 2.2. Điểm đặc sắc trong Kỹ thuật Trích xuất:
- **Xử lý tài liệu Word không phụ thuộc Office COM**: Sử dụng thư viện gốc `System.IO.Compression` và LINQ to XML (`System.Xml.Linq`), chạy mượt mà trên cả Linux container và Windows Server mà không cần cài đặt Microsoft Office.
- **Bảo toàn bảng biểu Excel**: Biến các sheet phân công nhân sự, ma trận trách nhiệm RACI thành văn bản có định dạng `Cột A | Cột B | Cột C`, giúp AI hiểu chính xác mối quan hệ giữa người làm và đầu việc.
- **An toàn bộ nhớ**: Giới hạn cứng 30MB và cơ chế stream một chiều ngăn chặn tấn công từ chối dịch vụ (Zip Bomb / OOM Denial of Service).

---

## 3. Kiến trúc Spec Hub & Giao thức Truy vấn MCP

Kho tài liệu đặc tả **Spec Hub** là "kho lưu trữ tri thức" của dự án. Lớp [`SpecHubTools.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/McpServers/SpecHubTools.cs) cung cấp 3 công cụ chuẩn MCP:

1. `spec_hub_list_documents(projectId)`: Quét toàn bộ cây thư mục tài liệu đặc tả của dự án.
2. `spec_hub_read_document(documentId)`: Đọc toàn văn nội dung một tài liệu đã chọn kèm tiêu chí nghiệm thu.
3. `spec_hub_search(projectId, keyword)`: Tìm kiếm toàn văn xuyên suốt mọi tài liệu trong dự án.

### Cơ chế Kết hợp Vector Search (Qdrant) và Fallback:
- Khi cấu hình `Qdrant:Enabled = true`: Hệ thống nhúng (Embed) các đoạn tài liệu thành vector 768 chiều bằng `gemini-embedding-001` và lưu trên Qdrant Vector Database để tìm kiếm theo độ tương đồng ngữ nghĩa (Cosine Similarity).
- Khi cấu hình `Qdrant:Enabled = false`: Hệ thống tự động chuyển sang cơ chế tìm kiếm toàn văn Full-Text Search trong SQL và lọc theo từ khóa, đảm bảo hệ thống hoạt động ổn định trong mọi điều kiện triển khai.

---

## 4. Cơ chế Trích dẫn Nguồn gốc (Citation Grounding) & Trải nghiệm Người dùng

Mỗi Task và Subtask do AI sinh ra bắt buộc phải có thuộc tính `documentCitations` chỉ rõ nguồn gốc xuất xứ từ tài liệu thực tế của dự án:
- Ví dụ: `Cam-nang-ky-thuat.md - Mục Phần II: Đặc tả chức năng và quy tắc nghiệp vụ`
- Ví dụ: `Bản đặc tả về quản lý dự án.txt - Mục 2`
- Ví dụ: `Ghi chú cuộc họp đã nạp - Mục 1: Yêu cầu chính`

```mermaid
sequenceDiagram
    actor User as Người điều phối (Scrum Master / PM)
    participant UI as Giao diện Hội thoại Phân rã Kế hoạch
    participant Drawer as Ngăn kéo Xem trước Tài liệu
    participant Hub as Trang Quản trị Tài liệu Đặc tả

    User->>UI: Nhấp vào nguồn trích dẫn tài liệu kỹ thuật
    UI->>Drawer: Mở ngăn kéo nội tuyến từ cạnh phải màn hình
    Drawer->>Drawer: Tìm kiếm chính xác tệp tin và dòng đề mục được trích dẫn
    Drawer->>Drawer: Tự động cuộn mượt và đánh dấu nổi bật phân đoạn trích dẫn
    Note over UI,Drawer: Cuộc hội thoại và kết quả phân rã ở bên trái vẫn giữ nguyên trạng thái
    
    alt Người dùng muốn tiếp tục rà soát kế hoạch
        User->>Drawer: Xác nhận quay lại giao diện phân rã
        Drawer-->>UI: Đóng ngăn kéo, tiêu điểm trở về bảng phân rã
    else Người dùng muốn chuyển sang xem toàn văn tài liệu
        User->>Drawer: Xác nhận mở toàn văn tài liệu
        Drawer->>Hub: Điều hướng trực tiếp đến vị trí phân đoạn trích dẫn (mở thư mục cha và cuộn tâm)
    end
```

### Lợi ích Trải nghiệm Vượt trội & Thuật toán Xử lý Chính xác:
1. **Thuật toán Tìm kiếm Hai Lớp (Two-Pass Exact-First Traversal)**:
   - **Lớp 1 (Exact Match)**: Quét đệ quy toàn bộ cây thư mục để tìm tệp tin khớp chính xác tên tệp (có hoặc không có phần mở rộng như `.md`, `.txt`, `.docx`, `.xlsx`, `.pdf`). Thuật toán ngăn chặn triệt để hiện tượng tìm kiếm tham lam (greedy match) làm nhầm lẫn tệp tin gốc với các tệp tin chứa tên tương tự ở thư mục con.
   - **Lớp 2 (Partial Fallback)**: Chỉ kích hoạt khi toàn bộ cây thư mục không có tệp nào khớp chính xác, hỗ trợ tìm kiếm từ khóa nổi bật.
2. **Ưu tiên Khớp Dòng Tiêu đề (Deep Section Heading Prioritization)**:
   - Khi tìm kiếm đề mục (ví dụ: `Mục Phần II`), hệ thống phân tích các dòng bắt đầu bằng ký tự tiêu đề Markdown (`#`, `##`, `###`) hoặc in đậm (`**`), tách biệt hoàn toàn với các từ xuất hiện ngẫu nhiên trong nội dung văn bản.
   - Nhờ đó, đề mục `# Phần II. Đặc tả chức năng và quy tắc nghiệp vụ` luôn được ưu tiên chọn tuyệt đối, tránh cuộn nhầm lên các câu văn chứa từ tương tự ở đầu tài liệu.
3. **Đánh dấu Trực quan & Cuộn Mượt Tọa độ Tâm (Smooth Centering & Mark Glow)**:
   - Vị trí trích dẫn được bọc thẻ đánh dấu `<mark id="spec-target-highlight" class="spec-highlight-mark">` với viền sáng vàng hổ phách và hiệu ứng phát quang động.
   - Hàm `scrollIntoView({ behavior: 'smooth', block: 'center' })` đưa phân đoạn trích dẫn vào chính giữa tầm mắt người dùng, loại bỏ hoàn toàn việc ước tính số dòng sai lệch.
4. **Mở rộng Thư mục Cha Tự động (Ancestor Folder Auto-Expansion)**:
   - Khi chuyển hướng sang trang Spec Hub toàn màn hình, hệ thống tự động thu thập danh sách toàn bộ thư mục cha và kích hoạt mở rộng trên cây thư mục, giúp người dùng nhận diện ngay phân cấp tài liệu.
5. **Bảo tồn Ngữ cảnh Ghi chú Phiên làm việc**:
   - Trường hợp trích dẫn bắt nguồn từ biên bản cuộc họp hoặc yêu cầu người dùng dán vào, ngăn kéo nội tuyến hiển thị văn bản ghi chú với đầy đủ tính năng tìm kiếm và cuộn mượt.

---

## 5. Hướng dẫn Thực nghiệm & Kiểm chứng RAG Grounding

### 5.1. Kiểm tra Độ trễ (Latency) & Trạng thái Công Cụ trên Giao diện
Khi AI bắt đầu suy luận, giao diện hiển thị danh sách các hành động với biểu tượng `check_circle` xanh lục chuẩn xác và thời gian thực thi (latency ms):
- `Tìm kiếm tài liệu đặc tả liên quan trong Spec Hub · 7ms`
- `Đọc phân đoạn tài liệu đặc tả: Cam-nang-ky-thuat.md · 14ms`
- `Lấy danh sách các User Story đang có trong Backlog · 13ms`
- `Truy xuất vận tốc Scrum và các hạng mục dở dang · 37ms`
- `Phân tích kỹ năng và số giờ khả dụng của thành viên đội Scrum · 25ms`

### 5.2. Kiểm tra Tính Xác thực của Nguồn Trích dẫn & Điều Hướng Phân Đoạn
1. Tải lên hoặc tạo một tệp tài liệu đặc tả trong Spec Hub (ví dụ: `Cam-nang-ky-thuat.md` chứa đề mục `Phần II: Xử lý ngữ cảnh AI Engine`).
2. Gõ yêu cầu phân rã Sprint hoặc phân tích kế hoạch.
3. Quan sát kết quả đề xuất:
   - Các task sinh ra có nhãn tài liệu trích dẫn đúng định dạng `Tài liệu Spec Hub: Cam-nang-ky-thuat.md - Phần II`.
   - Nhấp vào nhãn trích dẫn: Drawer nội tuyến mở ra, hiển thị tiêu đề, vị trí trích dẫn và nội dung đặc tả.
   - Nhấp vào nút "Mở trang Spec Hub": Trình duyệt mở tab Spec Hub với đầy đủ tham số `?docId=...&docName=...&section=...`. Hệ thống tự động mở tab tài liệu, cuộn mượt đến đề mục `Phần II` và kích hoạt hiệu ứng viền vàng nổi bật.
   - Nhấp "Quay lại đoạn chat": Hệ thống quay trở lại giao diện phân rã Sprint nguyên vẹn.

---

## 6. Danh mục Tệp Mã Nguồn Tham Chiếu Chuyên Đề 4

| Thành phần | Vai trò kỹ thuật | Liên kết mã nguồn |
| :--- | :--- | :--- |
| **AiDocumentTextExtractor** | Trích xuất toàn văn từ Word, Excel, PDF, Markdown, Text | [`AiDocumentTextExtractor.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/Services/Ai/AiDocumentTextExtractor.cs#L10-L120) |
| **SpecHubTools** | Các MCP tool tra cứu tài liệu đặc tả trong Spec Hub | [`SpecHubTools.cs`](file:///c:/Users/hoang/Downloads/ProjectMgmt/ProjectMgmt.Solution/McpServers/SpecHubTools.cs#L45-L100) |
| **ai-decompose-modal.html** | Template giao diện Drawer nội tuyến xem trước tài liệu trích dẫn | [`ai-decompose-modal.html`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/backlog/components/ai-decompose-modal/ai-decompose-modal.html#L380-L450) |
| **ai-decompose-modal.ts** | Điều khiển mở/đóng drawer xem tài liệu, deep link sang Spec Hub | [`ai-decompose-modal.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/backlog/components/ai-decompose-modal/ai-decompose-modal.ts#L450-L520) |
| **spec-hub-page.ts** | Đón nhận queryParams (docId, section) để cuộn đến đúng vị trí | [`spec-hub-page.ts`](file:///c:/Users/hoang/Downloads/ProjectMgmt/frontend/projectmgmt-web/src/app/features/spec-hub/spec-hub-page.ts#L35-L65) |

