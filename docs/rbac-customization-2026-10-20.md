# Quản trị phân quyền và tùy chỉnh dự án — 20/10/2026

## Đã hoàn thiện

- Ma trận đọc các GUID quyền đã gán từ backend và hiển thị đúng ô chọn. Khi lưu, frontend chuyển mã quyền sang GUID, chỉ gửi vai trò có thay đổi và báo lỗi rõ ràng. Vai trò mới lưu các quyền đã chọn trong cùng yêu cầu tạo.
- Danh sách quản trị lấy tài khoản thật, gồm trạng thái hoạt động, xác minh email, đăng nhập cuối và các vai trò theo phạm vi. API `/api/v1/admin/users` yêu cầu quyền `identity.role.manage` ở phạm vi System. Không tự cấp quyền quản trị cho tài khoản đang đăng nhập.
- Bỏ dữ liệu vai trò/quyền giả khi API trả danh sách rỗng. Danh sách người dùng hiển thị nhiều vai trò có phạm vi; bỏ dropdown đổi một vai trò toàn cục và nút khóa tài khoản vốn chưa có API thực hiện. Việc gán/gỡ vai trò dự án vẫn thực hiện ở trang thành viên.
- Tạo loại issue trong hộp tạo công việc lưu vào bảng `IssueType`; tải lại danh sách theo dự án khi mở workspace. Tên, biểu tượng và màu được lưu; chặn tên trùng, dữ liệu sai và người không có quyền cấu hình.
- Trang cấu hình workflow có bốn mẫu: Scrum, Agile QA, Kanban và Enterprise. Áp dụng mẫu bổ sung trạng thái, đường chuyển tiếp còn thiếu và cột board; sắp xếp các bước theo mẫu. Giữ ID trạng thái, công việc, trạng thái ban đầu và các luật/quyền hiện có. Áp dụng lại không tạo trùng.
- Các thao tác cấu hình yêu cầu `project.settings.manage`. Dùng bảng hiện có, không cần migration.

## Cách dùng trên máy

- Frontend: http://localhost:4200
- Backend/Swagger: http://localhost:5083/swagger
- Quản trị: `/admin/identity`, dùng tài khoản được cấp quyền quản trị hệ thống.
- Loại issue: chọn dự án → **Tạo việc mới** → **Tạo Loại Mới**.
- Workflow: **Cấu hình dự án → Cấu hình Workflow → Áp dụng mẫu**.

Mẫu bổ sung vào cấu hình hiện tại, không xóa những trạng thái hoặc đường chuyển đã tồn tại. Muốn bắt buộc đi qua một bước QA, quản lý dự án cần xem lại và điều chỉnh các đường chuyển cũ tại cùng trang.

## Kiểm chứng

- Backend: 63 test hiện có đạt; build thành công.
- Frontend: 80 test đạt, gồm ánh xạ GUID quyền, tạo vai trò kèm quyền, lỗi lưu, dữ liệu theo dự án và lỗi áp dụng mẫu.
- 51 kiểm tra API/MySQL mới: quyền quản trị, lưu quyền, loại issue, bốn mẫu workflow, áp dụng lặp lại và chặn người ngoài/Viewer.
- 51 kiểm tra API/MySQL hồi quy các chức năng công việc trước đó.
- 5 luồng trình duyệt mới: danh sách người dùng, tạo vai trò kèm quyền, sửa ma trận, áp dụng workflow và tạo loại issue; có tải lại trang để kiểm tra lưu dữ liệu.
- Frontend build và lint đạt; còn cảnh báo optional chaining đã tồn tại.

Kiểm thử ghi dữ liệu chỉ chạy trên API `localhost:5084`, dùng database bản sao `projectmgmt_audit_20261019`. Tài khoản kiểm thử quản trị chỉ được cấp quyền trong bản sao.

Chạy lại: `tests/customization-integration.py` và `frontend/projectmgmt-web/e2e/customization.cjs`. Đặt `WORKSPACE_TEST_EMAIL/PASSWORD` cho tài khoản quản trị thử nghiệm; API test cần thêm `WORKSPACE_OTHER_EMAIL/PASSWORD` cho tài khoản thường. Không dùng database chính để chạy các script này.
