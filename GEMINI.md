# Quy tắc Dự án & Thiết kế Giao diện (Project Standards & Guidelines)

## 1. QUY TẮC BẮT BUỘC VỀ ICON & EMOJI (STRICT NO-KEYBOARD-EMOJI RULE)
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

## 2. QUY TRÌNH PHÂN QUYỀN SCRUM TỪ TRÊN XUỐNG (HIERARCHICAL SCRUM RBAC)
- Quyền hạn kế thừa và chỉ đạo nghiêm ngặt từ trên xuống:
  1. **Chủ tổ chức (Org Owner)**: Lãnh đạo tối cao của toàn tổ chức.
  2. **Giám đốc dự án (Project Director / Creator - `LeadUserId`)**: Người phụ trách cao nhất của dự án. Vai trò bất biến (không ai có quyền hạ cấp hay xóa khỏi dự án).
  3. **Scrum Master / Quản lý dự án**: Quản lý sprint, backlog, điều phối công việc và phân bổ vai trò cho đội ngũ thực thi.
  4. **Kỹ sư phát triển (Developer) & QA**: Thực thi nhiệm vụ, không có quyền phân quyền hay thao tác vượt cấp.
  5. **Khách hàng / Người quan sát (Viewer)**: Quyền chỉ đọc.
- Cấp dưới tuyệt đối không được phép thấy hoặc thực hiện các quyền hạn thuộc về cấp trên (Anti-Privilege Escalation).
- Không để menu quản trị phân quyền hệ thống (`/admin/identity`) lộ ra ngoài người dùng bình thường; chỉ hiển thị cho Quản trị viên hệ thống (Admin). Toàn bộ phân quyền dự án được gom gọn vào tab "Thành viên & Phân quyền" trong Cài đặt dự án.
