# Bàn giao phần Project/Settings của Hoài

## Bổ sung: sửa tên cột và WIP limit

Ngày 18/10/2026 bổ sung `PUT /api/boards/columns/{columnId}` và form **Sửa** trên Board Settings. Request chỉ nhận `name` và `wipLimit`, không đổi ánh xạ trạng thái hoặc thứ tự cột. Tên được trim, tối đa 80 ký tự; tên trống/null dùng tên trạng thái. WIP phải là số nguyên dương hoặc null (không giới hạn).

Giao diện chỉ cập nhật giá trị đã lưu khi API thành công, giữ bản nháp khi lỗi, có nút Hủy và chặn gửi lặp/thao tác xung đột trong lúc lưu. Đây là cấu hình WIP, chưa phải chức năng chặn chuyển Issue khi quá tải.

Đã xác minh: **20 backend tests, 44 frontend tests**, lint thành công; production build backend/frontend trong Docker thành công và bản local đã cập nhật. Kiểm thử HTTP qua reverse proxy với MySQL thật xác nhận lưu/đọc lại tên và WIP, không thay mapping/order, xóa override tên/bỏ giới hạn, HTTP 400 cho WIP 0/âm/số lẻ/tên quá dài, HTTP 404 cho column không tồn tại. Dữ liệu organization/project tạm của phép thử đã được dọn theo đúng ID. Không cần migration; không thay đổi authentication/phân quyền.

Phép thử trên chỉ nghiệm thu chức năng sửa cột mới. Checklist tích hợp toàn bộ Project/Settings bên dưới vẫn cần thực hiện riêng.

Ngày thực hiện: 18/10/2026. Tài liệu ánh xạ các gói việc đã lên kế hoạch cho tuần 3–8; không xác nhận rằng các công việc đã được thực hiện trong những tuần lịch sử đó.

## Phạm vi triển khai

| Gói tuần | Thay đổi |
|---|---|
| 3 | Đọc project theo ID từ API; bỏ fallback sang project khác. Cập nhật cache đúng ID, không dùng project hiện hành làm dữ liệu nền cho project đang sửa. |
| 4 | Danh sách/tạo Project qua HTTP. Form đọc Organization và user từ API, có tạo Organization bằng POST rõ ràng. GET Organization không ghi dữ liệu. |
| 5 | Sửa/xóa mềm Project qua HTTP; chỉ cập nhật state sau thành công. Validation tên, key, organization active và lead không rỗng. Key của project xóa mềm vẫn được giữ theo unique index hiện có. |
| 6 | Đọc trạng thái, đọc/tạo/xóa transition qua HTTP; kiểm self-transition, status ngoài project và duplicate. Bỏ các nút áp dụng template/thêm/xóa status chỉ cập nhật mock trên trang Settings. |
| 7 | Đọc/tạo board, đọc/thêm/xóa column qua HTTP. Kiểm WIP nguyên dương hoặc null, status trùng và độ dài field; trả lỗi constraint dưới dạng ProblemDetails. |
| 8 | Reorder nhận đúng toàn bộ column ID, không trùng/ngoài board; sửa EF tracking để lưu OrderIndex. Giao diện chờ server thành công, chặn reorder chồng nhau, giữ thứ tự cũ khi lỗi. |

## Điểm tích hợp với hai thành viên còn lại

- Endpoint user lookup phục vụ chọn project lead đọc user active/profile thật, không sinh ID mẫu. Skill lookup vẫn chưa hoàn thiện.
- Database mới chỉ có reference seed có thể chưa có tài khoản. Form báo thiếu user; không tự tạo tài khoản hoặc tự gán admin. Người phụ trách Identity cung cấp tài khoản hợp lệ qua luồng của họ.
- Authentication, kiểm quyền/membership theo organization/project và xác minh các user ID đầu vào vẫn cần người phụ trách Identity hoàn thiện. Những kiểm tra project tồn tại trong bản sửa này không thay thế authorization.
- Issue/Backlog/Sprint/AI và dữ liệu demo của các màn hình đó chưa được nối trong đợt này. Việc Settings đọc/ghi API không đồng nghĩa các màn hình đó đã sử dụng cùng nguồn dữ liệu.
- Tạo Project từ shell chuyển tới form đầy đủ để chọn tổ chức và lead, tránh luồng modal cũ tạo project trong bộ nhớ.

## Kiểm tra tự động

```powershell
dotnet test ProjectMgmt.Tests/ProjectMgmt.Tests.csproj --no-restore
```

Tại `frontend/projectmgmt-web`:

```powershell
npm ci
npm run lint
npm test -- --watch=false
npm run build
```

Backend tests kiểm tra luật nghiệp vụ bằng repository giả: Organization GET không ghi, seed cấu hình Project, organization inactive, key đã xóa mềm, WIP, column status, workflow và permutation reorder. Frontend dùng HttpTestingController để kiểm contract HTTP, cập nhật cache theo đúng ID, 404, lỗi xóa và persistence feedback. Chúng không kiểm chứng việc ghi vào MySQL thật.

## Checklist nghiệm thu với database thử nghiệm — chưa chạy

Chỉ dùng database thử nghiệm đã được nhóm thống nhất. Không chạy initial migrations lên database hiện hữu chưa baseline; đợt sửa này không đổi schema và không bổ sung migration.

1. Cấu hình connection string bằng User Secrets/secret store theo README; không ghi mật khẩu vào repository. Frontend local dùng API `http://localhost:5083/api`. Host áp dụng origin trong `Cors:AllowedOrigins`.
2. Có ít nhất một user active hợp lệ do phần Identity cung cấp. Mở tạo Project; tạo Organization bằng nút riêng nếu danh sách trống.
3. Tạo Project; reload trang và xác nhận project, status, transition, board, column được đọc lại từ database.
4. Chọn project A, mở Settings của B, sửa B; kiểm tra ID và dữ liệu A không bị thay đổi. Thử ID không tồn tại: phải báo lỗi.
5. Thử tên rỗng/quá 200 ký tự, key sai/trùng, key của project đã xóa mềm, organization inactive. Kiểm tra HTTP 400/409 tương ứng và không sinh cấu hình dở dang.
6. Tạo/xóa transition, reload; thử self-transition, status ngoài project và duplicate.
7. Tạo board và column, reload; thử WIP 0/âm, status đã có column; API phải từ chối và UI hiển thị lỗi.
8. Đảo thứ tự column, reload hoặc đọc bằng request/context mới. Thử request thiếu ID, trùng ID hoặc ID ngoài board: trả 400, không đổi thứ tự một phần.
9. Ngắt API trong môi trường thử để kiểm phản hồi lỗi; không hiển thị thành công hoặc thay thứ tự local khi request thất bại.
10. Xóa mềm Project; reload danh sách và kiểm tra các API board/workflow đã sửa từ chối thao tác trên project bị xóa.

## Giới hạn xác minh

Kết quả kiểm tra ngày 18/10/2026: backend build thành công; 14 backend tests và 37 frontend tests thành công; frontend lint và production build thành công. Backend vẫn có các compiler/analyzer warning đã tồn tại, đặc biệt trong phần tài khoản. Dependency frontend được cài bằng `npm ci` theo lockfile hiện có, không cập nhật phiên bản package.

Không thực hiện migration, ghi database thật, deploy, commit/push hoặc sửa nhật ký Word. Build/unit test thành công không thay thế checklist tích hợp MySQL và kiểm thử phân quyền. Việc chạy checklist trên cần môi trường thử và dữ liệu do nhóm chuẩn bị.
