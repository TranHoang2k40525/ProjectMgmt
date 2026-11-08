# Quy tắc Dự án & Tiêu chuẩn Phát triển Toàn diện (Project Standards & Agent Rules)
> [!IMPORTANT]
> Đây là bộ quy chuẩn TỐI CAO và BẮT BUỘC cho mọi AI Agent (Antigravity, Gemini, Cursor, Copilot, Cline, v.v.) và Lập trình viên khi tham gia phát triển dự án này. Mọi hành vi vi phạm quy chuẩn đều bị từ chối sáp nhập (reject).

---

## 1. BẢO MẬT TUYỆT ĐỐI & CHỐNG RÒ RỈ SECRET/KEY (STRICT ZERO-SECRET-LEAKAGE RULE)
- **CẤM TUYỆT ĐỐI HARDCODE HOẶC COMMIT SECRET VÀO GIT**:
  - Nghiêm cấm đưa bất kỳ thông tin nhạy cảm nào vào các tệp được Git theo dõi (source code `.cs`, `.ts`, `.html`, `.json`, `.yml`, cấu hình public):
    - Mật khẩu Database, Connection Strings thực tế.
    - Mật khẩu ứng dụng Gmail (SMTP App Password), API Keys, OAuth Secrets.
    - Khóa ký JWT (`SigningKey`), Khóa băm OTP (`HashKey`), Private Keys (`.pem`, `.pfx`, `.key`).
    - Personal Access Tokens (PAT), Cloud credentials.
- **MỌI SECRET PHÁT TRIỂN CỤC BỘ BẮT BUỘC LƯU TẠI TỆP ĐƯỢC BẢO VỆ**:
  - Thông tin cấu hình thực tế ở môi trường local của lập trình viên BẮT BUỘC đặt trong `ProjectMgmt.Solution/appsettings.Local.json` hoặc `.env`.
  - Hai tệp này đã được cấu hình trong `.gitignore` và tuyệt đối KHÔNG BAO GIỜ được xóa khỏi `.gitignore` hay commit lên remote.
- **TỆP CẤU HÌNH CÔNG KHAI CHỈ DÙNG PLACEHOLDER AN TOÀN**:
  - Các tệp `appsettings.json`, `appsettings.Development.json`, `.env.example`, `appsettings.*.example.json` chỉ được chứa giá trị mẫu an toàn (ví dụ: `YOUR_DATABASE_PASSWORD`, `YOUR_SMTP_APP_PASSWORD`, `GENERATE_YOUR_SECURE_JWT_SIGNING_KEY_MINIMUM_32_BYTES_BASE64`).
- **CHE DẤU (MASKING) THÔNG TIN BÍ MẬT TRONG PHẢN HỒI & LOG**:
  - Agent khi in log, hiển thị mẫu hoặc trao đổi với người dùng phải chủ động che giấu (masking) thông tin mật khẩu, token và email cá nhân (ví dụ: `hoang***@gmail.com`, `Pass: "******"`).
- **CẤM COMMIT FILE BUILD OUTPUT / BINARIES**:
  - Tuyệt đối không commit các thư mục đầu ra như `publish_output/`, `bin/`, `obj/`, `dist/`, các tệp `.dll`, `.exe`, `.pdb`.
- **PRE-COMMIT SECURITY AUDIT**:
  - Trước khi thực hiện `git add` và `git commit`, Agent BẮT BUỘC phải chạy `git diff` rà soát toàn bộ thay đổi, đảm bảo 100% không có bất kỳ secret/key thực tế nào bị đưa vào commit.

---

## 2. TỐI ƯU TỐC ĐỘ PHẢN HỒI & HIỆU SUẤT AGENT (HIGH RESPONSE SPEED & PERFORMANCE RULE)
- **ĐI THẲNG VÀO TRỌNG TÂM (ZERO-FLUFF PRINCIPLE)**:
  - Bỏ qua mọi lời chào hỏi xã giao, thủ tục giới thiệu rườm rà.
  - Phản hồi ngắn gọn, súc tích, đi thẳng vào bản chất kỹ thuật, file đã sửa đổi và kết quả kiểm thử.
- **TỐI ƯU HÓA TOOL CALL & TIẾT KIỆM BĂNG THÔNG**:
  - Xác định chính xác tệp tin và dải dòng cần sửa đổi trước khi thao tác, không đọc lan man toàn bộ thư mục.
  - Sử dụng `edit_file` dứt điểm trong một lần gọi công cụ duy nhất; tránh sửa đi sửa lại nhiều lần cùng một vị trí.
  - Không chạy các câu lệnh terminal lặp đi lặp lại hoặc kiểm tra thông tin thừa thãi.
- **KHÔNG CHẠY LỆNH SLEEP THỤ ĐỘNG**:
  - Tuyệt đối không dùng lệnh `Start-Sleep` hoặc `sleep` trong shell terminal. Dựa vào cơ chế Reactive Wakeup tự động khi xử lý tiến trình ngầm.
- **QUY TRÌNH BIÊN DỊCH KIỂM THỬ AN TOÀN**:
  - **Backend .NET**: Luôn tạo `ProjectMgmt.Solution/app_offline.htm` trước khi build (`dotnet build`) và xóa ngay sau khi hoàn tất để giải phóng file lock của IIS.
  - **Frontend Angular**: Chạy `npm run build` để kiểm tra compile TypeScript 0 lỗi trước khi bàn giao.
- **TỰ ĐỘNG COMMIT VÀ PUSH ĐÚNG NHÁNH**:
  - Mỗi khi hoàn thành yêu cầu, tự động commit theo chuẩn Conventional Commits (ví dụ: `fix: ...`, `feat: ...`, `docs: ...`) và push lên nhánh `hoangtv` với lệnh: `git push origin hoangtv`.

---

## 3. QUY TẮC BẮT BUỘC VỀ ICON & EMOJI (STRICT NO-KEYBOARD-EMOJI RULE)
- **TUYỆT ĐỐI KHÔNG DÙNG EMOJI TỪ BÀN PHÍM (RAW UNICODE EMOJIS)**:
  - Nghiêm cấm chèn emoji trực tiếp từ bàn phím (ví dụ: 👑, ⭐, 🎯, 💻, 🧪, 👁️, 🚀, 💡, 🔥, ⚡, 🛡️, 🎨, 📈, 📋, ✕, v.v.) vào bất kỳ tệp mã nguồn HTML, TS, SCSS, văn bản UI, nhãn, thông báo, modal, button hay dropdown nào.
  - Lý do: Emoji bàn phím hiển thị không đồng nhất trên các hệ điều hành (Windows, macOS, Linux, iOS, Android), làm giảm tính chuẩn mực enterprise và phá vỡ cấu trúc thiết kế giao diện doanh nghiệp.
- **BẮT BUỘC SỬ DỤNG ICON CHUẨN GOOGLE MATERIAL SYMBOLS**:
  - Mọi biểu tượng trên giao diện phải được hiển thị qua icon động từ thư viện **Google Material Symbols Outlined** (hoặc SVG vector).
  - Cú pháp chuẩn trong Angular template:
    ```html
    <span class="material-symbols-outlined text-[16px] text-primary" aria-hidden="true">icon_name</span>
    ```
  - Bảng đối chiếu các khái niệm sang icon Material Symbols:
    - Lãnh đạo tối cao / Chủ tổ chức: `stars` / `workspace_premium`
    - Giám đốc dự án / Lead: `verified` / `star`
    - Quản lý / Scrum Master: `track_changes` / `flag`
    - Kỹ sư phát triển (Developer): `terminal` / `code` / `developer_mode`
    - Kiểm thử chất lượng (QA / Tester): `biotech` / `bug_report` / `fact_check`
    - Người xem / Khách hàng (Viewer): `visibility` / `preview`
    - Tổ chức / Doanh nghiệp: `domain` / `corporate_fare`
    - Dự án: `folder` / `topic`
    - Epic / Đột phá: `bolt`
    - Bảo mật: `security` / `shield`
    - Thiết kế / UI: `palette`
    - Nghiên cứu: `description` / `menu_book`
    - Đóng / Hủy: `close`

---

## 4. QUY TRÌNH PHÂN QUYỀN SCRUM TỪ TRÊN XUỐNG (HIERARCHICAL SCRUM RBAC)
- Quyền hạn kế thừa và chỉ đạo nghiêm ngặt từ trên xuống:
  1. **Chủ tổ chức (Org Owner)**: Lãnh đạo tối cao của toàn tổ chức.
  2. **Giám đốc dự án (Project Director / Creator - `LeadUserId`)**: Người phụ trách cao nhất của dự án. Vai trò bất biến (không ai có quyền hạ cấp hay xóa khỏi dự án).
  3. **Scrum Master / Quản lý dự án**: Quản lý sprint, backlog, điều phối công việc và phân bổ vai trò cho đội ngũ thực thi.
  4. **Kỹ sư phát triển (Developer) & QA**: Thực thi nhiệm vụ, không có quyền phân quyền hay thao tác vượt cấp.
  5. **Khách hàng / Người quan sát (Viewer)**: Quyền chỉ đọc.
- Cấp dưới tuyệt đối không được phép thấy hoặc thực hiện các quyền hạn thuộc về cấp trên (Anti-Privilege Escalation).
- Không để menu quản trị phân quyền hệ thống (`/admin/identity`) lộ ra ngoài người dùng bình thường; chỉ hiển thị cho Quản trị viên hệ thống (Admin). Toàn bộ phân quyền dự án được gom gọn vào tab "Thành viên & Phân quyền" trong Cài đặt dự án.
- Developer, QA, Viewer không được thấy nút "Cài đặt" dự án trên navigation bar và bị chặn truy cập route `/projects/:id/settings` bằng `projectManagerGuard`.

---

## 5. TIÊU CHUẨN GIAO DIỆN & TRẢI NGHIỆM NGƯỜI DÙNG (RESPONSIVE & CLEAN UI)
- **THÍCH ỨNG ĐA MÀN HÌNH (RESPONSIVE LAYOUT)**:
  - Tất cả các drawer, modal, bảng dữ liệu, thanh điều hướng phải co dãn hợp lý khi màn hình bị thu nhỏ hoặc ở chế độ chia đôi màn hình (split-screen / half-screen).
  - Không để các trường dữ liệu bị chèn ép, che khuất hoặc tràn lề ngoài ý muốn.
- **TRẢI NGHIỆM THÔNG BÁO TINH GỌN**:
  - Không hiển thị toast popup nổi chiếm màn hình cho các hành vi thường xuyên lặp lại như bình luận công việc. Thay vào đó sử dụng đồng bộ dữ liệu thời gian thực (SignalR) và cập nhật trực tiếp tại giao diện.
