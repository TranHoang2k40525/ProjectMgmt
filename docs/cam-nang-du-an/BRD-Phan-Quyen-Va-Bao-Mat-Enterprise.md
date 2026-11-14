# BẢN ĐẶC TẢ YÊU CẦU NGHIỆP VỤ (BRD): PHÂN QUYỀN SCRUM PHÂN CẤP VÀ BẢO MẬT DOANH NGHIỆP

## PHẦN I: TỔNG QUAN PHÂN CẤP VAI TRÒ VÀ BẢO MẬT
### 1.1. Mô hình Phân quyền Phân cấp (Hierarchical Scrum RBAC)
Hệ thống thiết lập nguyên tắc phân quyền từ trên xuống nghiêm ngặt, đảm bảo mỗi thành viên chỉ nhìn thấy và thực hiện các quyền hạn tương xứng với trách nhiệm:
1. **Chủ tổ chức (Org Owner)**: Toàn quyền quản trị tổ chức, thành viên và các dự án trực thuộc.
2. **Giám đốc dự án / Người tạo (Project Director / Lead)**: Người phụ trách cao nhất của dự án. Vai trò bất biến, không thể bị hạ cấp hay xóa bởi người khác trong dự án.
3. **Quản lý dự án / Scrum Master**: Lập kế hoạch Sprint, điều phối Backlog, phân công công việc cho kỹ sư thực thi.
4. **Kỹ sư Phát triển (Developer)**: Thực hiện công việc, chuyển trạng thái tiến độ, bình luận và tạo công việc con. Không có quyền cài đặt dự án hay phân quyền vượt cấp.
5. **Kiểm thử Chất lượng (QA / Tester)**: Quản lý kịch bản kiểm thử, xác nhận tiêu chí nghiệm thu (AC) và ghi nhận lỗi (Bug).
6. **Người quan sát (Viewer)**: Quyền chỉ đọc dữ liệu báo cáo và tiến độ.

---

## PHẦN II: QUY TẮC BẢO MẬT VÀ CHỐNG NÂNG QUYỀN TRÁI PHÉP

### 2.1. Cơ chế Chống Leo thang Đặc quyền (Anti-Privilege Escalation)
- Cấp dưới tuyệt đối không được phép xem hoặc kích hoạt các nút chức năng thuộc thẩm quyền cấp trên.
- Các thành viên vai trò Developer, QA, Viewer bị ẩn nút "Cài đặt" dự án trên thanh điều hướng và bị chặn truy cập route `/projects/:id/settings` bởi `projectManagerGuard`.
- Menu quản trị định danh hệ thống (`/admin/identity`) chỉ khả dụng cho Quản trị viên cấp cao (System Admin).

### 2.2. Tiêu chuẩn Bảo mật Tuyệt đối (Zero-Secret-Leakage Rule)
- Nghiêm cấm hardcode bất kỳ secret, mật khẩu cơ sở dữ liệu, API keys (Gemini, Claude, OpenAI), SMTP password hay token bí mật vào repository.
- Toàn bộ thông tin nhạy cảm ở môi trường cục bộ lưu trữ trong `appsettings.Local.json` hoặc `.env` (được bảo vệ bởi `.gitignore`).
- Các phản hồi và log hệ thống phải chủ động che giấu (masking) thông tin mật khẩu và mã token.

