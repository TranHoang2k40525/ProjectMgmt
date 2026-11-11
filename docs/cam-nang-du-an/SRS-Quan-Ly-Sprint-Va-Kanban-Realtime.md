# BẢN ĐẶC TẢ YÊU CẦU PHẦN MỀM (SRS): QUẢN LÝ SPRINT VÀ BẢNG KANBAN REALTIME

## PHẦN I: TỔNG QUAN HỆ THỐNG VÀ BỐI CẢNH DỰ ÁN
### 1.1. Mục tiêu sản phẩm
Hệ thống Quản lý Dự án Doanh nghiệp cung cấp giải pháp hoạch định Sprint linh hoạt theo phương pháp Agile/Scrum, kết hợp bảng theo dõi tiến độ Kanban kéo thả trực quan và đồng bộ dữ liệu hai chiều thời gian thực (Realtime Two-way Synchronization).

### 1.2. Đối tượng thụ hưởng
- **Giám đốc dự án / Lead**: Giám sát tiến độ tổng thể, quản lý phân quyền và phê duyệt kế hoạch Sprint.
- **Product Owner (PO)**: Xác lập Sprint Goal, quản lý danh sách Backlog, đánh giá giá trị nghiệp vụ và nghiệm thu tiêu chí DoD.
- **Scrum Master**: Điều phối phiên lập kế hoạch Sprint Planning, phân bổ Story Points cân đối, tối ưu hóa Velocity của đội ngũ.
- **Kỹ sư Phát triển (Frontend & Backend)**: Tiếp nhận đầu việc cụ thể, cập nhật trạng thái công việc và thời gian thực hiện.
- **Kỹ sư Kiểm thử QA**: Xây dựng kịch bản kiểm thử tự động E2E, nghiệm thu tiêu chí Acceptance Criteria cho từng công việc.

---

## PHẦN II: ĐẶC TẢ CHỨC NĂNG VÀ QUY TẮC NGHIỆP VỤ

### 2.1. Quy trình Phân rã Sprint và Hoạch định Năng lực
1. **Tiếp nhận Yêu cầu & Biên bản cuộc họp**:
   - Hệ thống cho phép nạp biên bản cuộc họp Sprint Planning bằng văn bản hoặc tệp đính kèm (.docx, .pdf, .txt, .md, .xlsx).
   - Module AI phân tích ngữ cảnh, đối chiếu năng lực thực tế của thành viên (số giờ khả dụng, kỹ năng chuyên môn) để đề xuất danh sách công việc.
2. **Kế thừa Công việc Dở dang (Sprint Rollover)**:
   - Các Bug nghiêm trọng từ Sprint trước chưa giải quyết xong bắt buộc phải được Rollover sang Sprint mới với mức ưu tiên cao nhất ('Highest').
   - Các công việc đang làm dở dang ('In Progress') được kéo sang Sprint mới để bảo toàn nỗ lực thực hiện (WIP).
   - Các công việc chưa từng bắt đầu ('To Do') nhưng đã quá hạn (Overdue) sẽ được đề xuất chuyển về Product Backlog để Product Owner thẩm định lại độ cần thiết.
3. **Định cỡ Story Points theo chuỗi Fibonacci**:
   - Mọi công việc phải được gán điểm Story Points theo dãy Fibonacci: 1, 2, 3, 5, 8.
   - Nghiêm cấm gán cùng một điểm số cho toàn bộ các công việc.
   - Công việc từ 5 Story Points trở lên bắt buộc phải phân rã thành các công việc con (Subtasks) theo các chuyên môn Frontend, Backend, QA.

### 2.2. Bảng Kanban Kéo Thả Thời Gian Thực (Realtime SignalR)
1. **Luồng kéo thả thẻ công việc**:
   - Người dùng có thể kéo thả thẻ công việc giữa các cột trạng thái: To Do -> In Progress -> Code Review -> Done.
   - Khi vị trí thẻ thay đổi, client phát sự kiện cập nhật trạng thái lên server qua API RESTful hoặc WebSocket Hub.
2. **Đồng bộ thời gian thực qua SignalR**:
   - Server phát tín hiệu cập nhật đến toàn bộ các client đang xem cùng một dự án.
   - Độ trễ phản hồi (latency) trên client phải dưới 200ms trong điều kiện mạng nội bộ.
   - Khi có xung đột đồng bộ (Optimistic Concurrency), hệ thống hiển thị thông báo trạng thái mà không làm mất dữ liệu nhập dở của người dùng.

### 2.3. Cảnh báo Quá hạn và Quản lý Thời hạn (Due Date Management)
1. **Trạng thái quá hạn**:
   - Một công việc được xác định là quá hạn khi `DueDate < Ngày hiện tại` và trạng thái công việc khác 'Done' hoặc 'Closed'.
   - Thẻ quá hạn hiển thị viền đỏ cảnh báo và nhãn cảnh báo thời gian trễ.
2. **Xử lý công việc quá hạn trong Sprint mới**:
   - Nếu là Bug tồn đọng: Gán tag `#CriticalFix`, thiết lập lại hạn chót mới và ưu tiên xử lý trong 24 giờ đầu của Sprint.
   - Nếu là User Story: Đội ngũ Scrum xem xét giảm phạm vi hoặc trả về Backlog.

---

## PHẦN III: YÊU CẦU PHI CHỨC NĂNG VÀ KIẾN TRÚC KỸ THUẬT

### 3.1. Kiến trúc Giao diện Người dùng (Frontend Architecture)
- **Framework**: Angular v19+ Standalone Components.
- **Quản lý Trạng thái**: Angular Signals (`signal`, `computed`, `effect`) đảm bảo không phát sinh re-render thừa.
- **Biểu tượng**: 100% sử dụng Google Material Symbols Outlined. Tuyệt đối không dùng raw keyboard emojis.
- **Responsive**: Hỗ trợ hiển thị mượt mà trên Desktop, Laptop chia đôi màn hình (half-screen split), và Tablet.

### 3.2. Kiến trúc Dịch vụ Nền tảng (Backend Architecture)
- **Nền tảng**: .NET 10 Web API, kiến trúc Modular Monolith phân tầng (Domain, Application, Infrastructure).
- **Cơ sở Dữ liệu**: MySQL 8.0 với Entity Framework Core, kết hợp Redis bộ nhớ đệm cho phiên làm việc AI.
- **Bảo mật**: Xác thực JWT Token, tuân thủ nguyên tắc Zero-Secret-Leakage, không để lộ thông tin nhạy cảm trong log hoặc commit.

### 3.3. Tiêu chuẩn Kiểm thử và Nghiệm thu (Definition of Done)
- **Unit Testing**: Bộ kiểm thử xUnit bao phủ tối thiểu 80% logic nghiệp vụ cốt lõi.
- **Integration Testing**: Xác thực toàn bộ các luồng giao dịch cơ sở dữ liệu và đồng bộ dữ liệu.
- **E2E Testing**: Kịch bản kiểm thử tự động Playwright chạy ổn định trong chế độ headless.
