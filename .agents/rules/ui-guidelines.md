# Quy tắc Dự án & Thiết kế Giao diện (UI & Icon Standards)

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
