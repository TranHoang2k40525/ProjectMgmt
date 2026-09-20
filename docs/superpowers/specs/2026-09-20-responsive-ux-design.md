# Thiết kế Responsive UX toàn bộ ProjectMgmt

Ngày: 2026-09-20  
Trạng thái: Đã được duyệt — phương án Adaptive Reflow  
Phạm vi: Angular frontend `frontend/projectmgmt-web`

## 1. Mục tiêu

- Giữ nguyên giao diện và bố cục desktop hiện tại từ `1280px` trở lên.
- Không để nội dung hoặc thao tác bị che, cắt, tràn khỏi viewport trên điện thoại và iPad.
- Không loại bỏ dữ liệu hay chức năng. Nội dung phụ có thể chuyển sang vùng cuộn có chủ đích, accordion hoặc bố cục xếp dọc.
- Hỗ trợ đủ bốn tư thế: điện thoại dọc, điện thoại ngang, iPad dọc và iPad ngang.
- Duy trì ngôn ngữ thiết kế Jira–Stitch/Material 3 hiện có; đây là tối ưu responsive, không phải redesign desktop.

## 2. Design Read và mức điều chỉnh

Đọc thiết kế: ứng dụng quản lý dự án dạng B2B có mật độ thông tin cao; ưu tiên khả năng đọc, thao tác nhanh và giữ ngữ cảnh công việc.

- `DESIGN_VARIANCE = 4`: thay đổi vừa phải, tập trung vào reflow và khả năng dùng.
- `MOTION_INTENSITY = 2`: không thêm chuyển động trang trí; chỉ giữ phản hồi tương tác hiện có.
- `VISUAL_DENSITY = 7`: desktop giữ mật độ cao; mobile giảm mật độ bằng xếp dọc và progressive disclosure.

## 3. Hệ breakpoint và xoay màn hình

| Chế độ | Điều kiện | Hành vi chính |
|---|---|---|
| Điện thoại dọc | `max-width: 767px` và portrait | Một cột, hành động chính full-width khi cần, nội dung dài được wrap |
| Điện thoại ngang | `max-width: 932px`, landscape và chiều cao ngắn | Một hoặc hai cột tùy loại nội dung; modal cuộn dọc; giảm khoảng trắng theo chiều cao |
| iPad dọc | `768px–1023px` | Một hoặc hai cột cân đối; bảng/timeline có vùng cuộn riêng |
| iPad ngang | `1024px–1279px` | Hai cột khi đủ chỗ; các lưới desktop quá rộng phải reflow |
| Desktop | `min-width: 1280px` | Không thay đổi bố cục, kích thước hoặc mật độ hiện tại |

Các media query responsive mới phải giới hạn bằng `max-width: 1279px`. Quy tắc xoay ngang dùng thêm `orientation: landscape` và giới hạn chiều cao khi cần.

## 4. Nguyên tắc responsive dùng chung

1. `min-width: 0` cho phần tử con của flex/grid để text có thể wrap đúng.
2. Tiêu đề quan trọng trên mobile được phép xuống tối đa hai dòng; không dùng ellipsis làm mất ý nghĩa công việc.
3. Nút và vùng bấm thường xuyên trên thiết bị cảm ứng có kích thước mục tiêu tối thiểu khoảng 40–44px.
4. Bảng dữ liệu rộng dùng container cuộn ngang rõ ràng; header hoặc cột định danh được sticky khi phù hợp.
5. Modal/drawer không cao hơn viewport; phần nội dung bên trong cuộn, footer hành động vẫn truy cập được.
6. Biểu đồ SVG/canvas co theo container, không kéo rộng document.
7. Không dùng `overflow-x: clip/hidden` để che một lỗi bố cục. Vùng rộng có chủ đích phải có container cuộn riêng.
8. Các tab dài trên mobile có vùng cuộn ngang, snap nhẹ và dấu hiệu còn nội dung ở phía bên phải.

## 5. Thiết kế theo nhóm màn hình

### App shell và điều hướng

- Giữ nguyên header/sidebar desktop.
- Mobile/tablet giữ sidebar dạng drawer.
- Thanh điều hướng dự án cuộn ngang độc lập, không làm rộng document; mục đang chọn luôn nhìn thấy được.
- Header điện thoại ngang giảm khoảng trống dọc nhưng không ẩn hành động chính.
- Toast và flyout giới hạn theo chiều rộng viewport.

### Auth

- Portrait xếp nội dung giới thiệu và form theo chiều dọc.
- Landscape chiều cao thấp cho phép trang cuộn, card đăng nhập không bị cắt.

### For You, Project Summary, Members, Projects

- Grid tự chuyển 4 → 2 → 1 cột theo vùng hiển thị.
- Header và nhóm hành động wrap có trật tự.
- Card không dùng chiều rộng tối thiểu lớn hơn vùng nội dung.
- Modal thành viên cuộn được trong chế độ ngang.

### Profile và Notifications

- Hero và CTA của Profile xếp dọc trên điện thoại; iPad giữ cân đối hai vùng khi đủ chỗ.
- Segmented tabs cuộn ngang, không cắt nhãn.
- Các form/grid về một cột trên mobile.
- Notification item cho phép nội dung wrap, giữ thao tác ở vị trí dễ chạm.

### RBAC Admin và AI Governance

- RBAC matrix giữ mô hình bảng vì quan hệ hàng/cột là thông tin chính; đặt trong vùng cuộn ngang có sticky cột quyền hạn.
- Toolbar RBAC xếp dọc trên điện thoại, nút không đè lên mô tả.
- Bảng log AI đặt trong vùng cuộn riêng; card model dùng `minmax(0, 1fr)` để không tràn.
- Không che cột hoặc giả vờ sửa bằng cách cắt overflow.

### Project Create và Settings

- Form giữ một cột trên mobile; hàng nút cuối form xếp dọc hoặc wrap.
- Spacing giảm có kiểm soát; label, validation và help text luôn hiển thị đầy đủ.

### Workflow Settings

- Grid `360px + 1fr` chuyển thành một cột cho toàn bộ tablet/mobile.
- Card áp dụng workflow: phần mô tả, select và CTA reflow theo chiều dọc trên phone; trên iPad dùng cột linh hoạt.
- Hai vùng tạo transition và danh sách transition xếp dọc dưới 1280px.
- Bảng transition cuộn riêng, không kéo rộng document.

### Board Settings

- Grid quản lý cột chuyển một cột trên điện thoại và điện thoại ngang.
- iPad chỉ giữ hai cột khi mỗi cột đạt chiều rộng đọc an toàn.
- Bộ chọn board và nút tạo board wrap, không ép nội dung.

### Backlog, Board và Task List

- Giữ cấu trúc desktop hiện có.
- Backlog dùng card item trên phone; tiêu đề issue được phép hai dòng thay vì mất nội dung.
- Mốc iPad ngang phải dùng layout tablet để sidebar/cột metadata không làm rộng document.
- Kanban giữ cuộn ngang theo cột vì đây là mô hình không gian; mỗi cột snap khi vuốt.
- Task table dùng container cuộn có sticky cột nhận diện; các bộ lọc xếp dọc trên phone.

### Roadmap và Reports

- Roadmap giữ trục thời gian trong vùng cuộn ngang có chủ đích và không làm rộng trang.
- Biểu đồ Reports dùng SVG responsive (`viewBox`, chiều rộng 100%) và nhãn thích nghi; không cắt đường biểu diễn.
- Các card báo cáo về một cột ở tablet/mobile.

### Placeholder pages

- Issue Detail và AI DataOps hiện là placeholder; chỉ bảo đảm padding, typography và empty state vừa viewport.

## 6. Không thay đổi

- Không sửa nghiệp vụ, API, model dữ liệu, router hoặc quyền truy cập.
- Không đổi màu sắc, typography hoặc component hierarchy desktop ngoài phần bắt buộc để responsive hoạt động dưới 1280px.
- Không thay đổi nội dung tiếng Việt/tiếng Anh hiện tại trừ nhãn hướng dẫn cuộn nếu thật sự cần.

## 7. Kiểm thử và tiêu chí chấp nhận

Kiểm tra toàn bộ route ở các viewport:

- `390 × 844`: điện thoại dọc.
- `844 × 390`: điện thoại ngang.
- `768 × 1024`: iPad dọc.
- `1024 × 768`: iPad ngang.
- `1440 × 900`: desktop baseline.

Một route đạt yêu cầu khi:

- `document.scrollWidth === document.clientWidth`, trừ vùng cuộn con có chủ đích.
- Không có nút, dialog, menu hoặc nội dung chính nằm ngoài viewport mà không thể cuộn tới.
- Không có text quan trọng bị cắt do `overflow: hidden` hoặc ellipsis một dòng.
- Các bảng/timeline/kanban rộng có thể cuộn bằng touch và vẫn giữ ngữ cảnh.
- Xoay dọc ↔ ngang không cần reload và không để lại overlay sai vị trí.
- Build, lint và test hiện có vẫn chạy thành công.
- Ảnh desktop sau sửa giữ nguyên bố cục so với baseline trước sửa.

## 8. Thứ tự triển khai

1. Shared responsive foundation và App Shell.
2. Các trang đang gây overflow đo được: Workflow, Board Settings, Reports, Profile, RBAC, AI Governance.
3. Backlog, Board, Task List, Roadmap.
4. Các trang Project, For You, Notifications, Auth và placeholder.
5. Chạy build/lint/test và audit trình duyệt ở cả năm viewport.
6. Chỉ sửa lỗi dưới 1280px; nếu desktop thay đổi thì rollback quy tắc gây ảnh hưởng.
