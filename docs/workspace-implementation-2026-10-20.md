# Các phần đã hoàn thiện — 20/10/2026

Phạm vi: các mục được chọn trong báo cáo hiện trạng: quyền dự án, lưu công việc/Sprint, thành viên, trang chi tiết issue/attachment và quản lý phiên.

## Thay đổi

- API dự án kiểm tra quyền theo project/organization ở các thao tác đọc, sửa, xóa và cấu hình board/workflow. Danh sách dự án được lọc theo quyền; danh sách tổ chức chỉ cung cấp tổ chức có quyền tạo dự án hoặc do người hiện tại sở hữu.
- Sửa FE dùng đúng `/api/v1/projects` và `/api/v1/organizations`; các API board-column/workflow cũ vẫn dùng đúng route riêng của chúng.
- Thêm API workspace và CRUD issue trên các bảng hiện có. Task/Story/Bug/Epic/Subtask, mô tả, trạng thái, priority, story points, assignee, Epic, Sprint và labels được lưu vào MySQL. Việc tạo issue cấp số trong transaction để tránh trùng số khi nhiều request cùng tạo.
- Sprint lưu tên, mục tiêu, ngày và trạng thái. Backend kiểm tra khoảng ngày, quyền bắt đầu/kết thúc, giới hạn một Sprint đang chạy và không mở lại Sprint đã hoàn thành.
- Bình luận, log thay đổi và lịch sử chuyển trạng thái được lưu DB. Sắp xếp công việc được lưu bằng API riêng có kiểm tra quyền `backlog.rank`.
- Nối các nhóm kéo thả CDK; chuyển cột Kanban/chuyển Sprint và sắp xếp cùng danh sách có persistence. Trạng thái được kiểm tra theo workflow tại backend.
- FE có hàng đợi lưu tuần tự để thao tác phụ thuộc nhau không chạy trước request tạo ban đầu. Có chỉ báo đang lưu, cảnh báo khi rời trang đang lưu, phản hồi lỗi và tải lại trạng thái server khi thao tác thất bại. Dự án đang chọn được giữ qua reload trong cùng tab.
- Form thêm thành viên gọi API thật theo email tài khoản đã xác minh và vai trò dự án. Form chờ phản hồi trước khi đóng, giữ dữ liệu khi có lỗi; hỗ trợ gỡ thành viên. Danh sách tài khoản chọn người dùng lấy dữ liệu thật thay vì ba tài khoản fallback.
- Trang `/issues/:id` có đọc/sửa thông tin, subtask, comment, lịch sử, tiêu chí nghiệm thu và attachment. Drawer có liên kết tới trang chi tiết này.
- Attachment: upload/download/delete, kiểm tra quyền theo issue, giới hạn 10 MB, cho phép PDF/ảnh/TXT/CSV/DOCX/XLSX/ZIP. File lưu ngoài webroot tại `ProjectMgmt.Solution/App_Data/attachments`; download đi qua API có xác thực và trả file đính kèm. Thư mục này được Git bỏ qua và cần nằm trong bản sao lưu dữ liệu khi triển khai.
- Trang hồ sơ hiển thị phiên đang còn hiệu lực và thiết bị hiện tại. Thu hồi phiên chỉ tác động phiên thuộc chính tài khoản; cả refresh token và access token của phiên đó đều bị từ chối sau khi thu hồi. JWT mới gắn với bản ghi refresh token qua hash; phiên cũ có thể cần refresh hoặc đăng nhập lại.

Các chức năng trên sử dụng bảng/domain hiện có; thay đổi lần này không yêu cầu migration mới.

## Kiểm chứng

| Kiểm tra | Kết quả |
|---|---|
| Test backend hiện có | 63 đạt |
| Test frontend, gồm test hàng đợi và form thành viên mới | 71 đạt / 16 file |
| Backend build | Thành công |
| Frontend production build | Thành công; còn cảnh báo optional chaining đã tồn tại |
| Frontend lint | Thành công |
| HTTP regression với MySQL thật trên DB bản sao | 51 kiểm tra đạt |
| Chrome headless | 6 luồng qua giao diện đạt |

HTTP regression bao gồm người ngoài bị chặn GET/PUT/DELETE project; đọc/sửa issue và download/delete attachment trái quyền; Viewer đọc được nhưng không ghi được; persistence Epic/task/subtask/comment/criteria; sắp xếp; Sprint Active/Completed và chống hai Sprint Active; mời/gỡ thành viên; người khác không thu hồi được phiên của chủ tài khoản; thu hồi access/refresh token; xóa task và ẩn subtask.

Chrome kiểm tra tạo Sprint và task qua giao diện, reload còn dữ liệu, kéo thả Kanban bằng chuột thật rồi reload, chi tiết issue/comment/criteria/attachment còn sau reload, mời thành viên qua form và liệt kê phiên thật. Không có uncaught page error trong lượt kiểm tra.

Bằng chứng đã loại token/password/OTP: [workspace-verification-2026-10-20.json](workspace-verification-2026-10-20.json).

## Chạy lại kiểm thử

Backend độc lập trên `localhost:5084` phải dùng **database bản sao** và hai tài khoản đã xác minh: một tài khoản tạo tổ chức/dự án và một tài khoản thường không có quyền admin hệ thống. SMTP kiểm thử phải giữ thư trên máy; không dùng cấu hình gửi thư ra ngoài khi chạy các test này.

1. Đặt `WORKSPACE_TEST_EMAIL`, `WORKSPACE_TEST_PASSWORD`, `WORKSPACE_OTHER_EMAIL`, `WORKSPACE_OTHER_PASSWORD` trong môi trường shell.
2. Từ root: `python tests/workspace-integration.py`.
3. Từ `frontend/projectmgmt-web`: `node e2e/workspace.cjs` khi FE đang chạy ở 4200. Script chuyển request sang backend kiểm thử 5084 trong browser riêng.
4. Test thường: `dotnet test ProjectMgmt.Tests`; FE: `npm test -- --watch=false`, `npm run lint`, `npm run build`. Máy này chạy Vitest ổn định khi đặt `VITEST_MAX_WORKERS=1`.

## Dùng trên máy hiện tại

- FE: `http://localhost:4200/auth`.
- BE/Swagger: `http://localhost:5083/swagger`.
- MySQL local: `127.0.0.1:3307`, database `projectmgmt`; connection string nằm trong User Secrets local.
- Hộp thư local: `http://localhost:8026`. Đây là môi trường nhận email thử trên máy, không gửi tới Gmail/HUCE thật.
- Nếu máy đã khởi động lại, bật Docker Desktop rồi chạy `powershell -ExecutionPolicy Bypass -File .tmp/start-local.ps1` từ root. Script và SMTP local trong `.tmp` là công cụ riêng trên máy, không được Git theo dõi.
- Tài khoản đã xác minh từ bước trước có thể đăng nhập bằng mật khẩu đã đăng ký. Khi chưa có dự án, nút tạo việc hướng tới màn hình tạo dự án; ở đó có thể tạo tổ chức trước.

Kết quả này kiểm chứng các luồng nêu trên, không thay thế kiểm thử tải lớn hoặc kiểm thử toàn bộ tùy biến workflow/role. Các thay đổi đang ở working tree để tiếp tục xem xét; dữ liệu kiểm thử nằm trong database bản sao riêng.
