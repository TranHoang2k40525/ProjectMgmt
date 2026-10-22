# Rà soát ProjectMgmt — 19/10/2026

> Đây là báo cáo hiện trạng trước khi sửa. Các mục được chọn về quyền dự án, persistence công việc/Sprint, thành viên, chi tiết issue/attachment và phiên đăng nhập đã được triển khai ngày 20/10; xem [thay đổi và kết quả kiểm chứng](workspace-implementation-2026-10-20.md).

Mốc mã nguồn: nhánh `hoangtv`, commit `90dad7de203194ee83655034d838a71dadf6dd56`.

**Kết luận:** hệ thống đã có backend đáng kể cho tài khoản, hồ sơ, phân quyền, dự án, thông báo và AI Breakdown; phần quản lý công việc/Sprint và một số màn hình quản trị vẫn đang dở. Chưa thể coi toàn bộ website là hoàn thành hoặc sẵn sàng dùng dữ liệu thật. Có lỗi phân quyền dự án đã tái hiện, lỗi tích hợp FE–BE và thao tác báo thành công nhưng chưa lưu database.

## Phạm vi và cách kiểm tra

- Đọc toàn bộ danh sách route FE, controller/API BE, các service nghiệp vụ, cấu hình, migration và bộ test; đi sâu vào những nhánh thực thi liên quan đến phát hiện.
- Chạy toàn bộ test BE, test FE, build production FE và lint FE.
- Duyệt 19 route sau đăng nhập bằng Chrome headless; mở thêm trang auth và chụp kích thước mobile. Đây là smoke test màn hình, không phải kiểm thử mọi nút hoặc mọi kích thước màn hình.
- Kiểm thử HTTP thực tế trên backend riêng `localhost:5084`, database bản sao `projectmgmt_audit_20261019`, SMTP local cổng `1026`. Tài khoản, dự án, issue thử và thay đổi phân quyền chỉ nằm trong bản sao.
- Frontend dùng code hiện tại, chỉ chuyển đích request sang backend kiểm thử trong trình duyệt tự động. Không sửa đường dẫn API sai trong code để làm test qua.
- Luồng Gmail thật vẫn đang bị từ chối xác thực SMTP như đã chẩn đoán trước đó. SMTP local chỉ giúp kiểm chứng logic OTP, không chứng minh Gmail đã hoạt động.
- Không sửa chức năng trong lần audit này. Thay đổi `environment.ts` sang `localhost:5083` là thay đổi có từ bước chạy local trước đó.

## Kết quả kiểm tra tự động

| Kiểm tra | Kết quả | Giới hạn |
|---|---|---|
| Backend `dotnet test` | **63/63 đạt** | Nhiều test dùng repository/email giả lập; host test chủ yếu kiểm tra truy cập ẩn danh, không bao phủ đầy đủ quyền giữa hai người dùng |
| Frontend `npm test -- --watch=false` | **67/67 đạt**, 15 file, với `VITEST_MAX_WORKERS=1` | Lần chạy mặc định bị `Worker exited unexpectedly`; lần chạy một worker thành công. Chưa kết luận nguyên nhân worker thoát |
| Frontend production `npm run build` | **Đạt** | Còn cảnh báo NG8107 về optional chaining; chưa kiểm thử toàn bộ bản production sau triển khai |
| Frontend `npm run lint` | **Không đạt: 1 lỗi** | Import `of` không dùng tại `project-management.service.ts:3` |
| Chrome smoke | 19 route tải được, không ghi nhận uncaught page error trong lượt duyệt | Các trang dự án có HTTP 404 và hiển thị lỗi; tải được trang không đồng nghĩa chức năng hoạt động |
| Lưu công việc/Sprint | **Không đạt** | Tạo thử trong service trình duyệt: 1 task, 1 Sprint; reload: 0 task, 0 Sprint |

## Ma trận chức năng

“Đã kiểm chứng” dưới đây chỉ áp dụng cho luồng được nêu, không phải chứng nhận không còn lỗi.

| Nhóm chức năng | Trạng thái | Bằng chứng / phần còn thiếu |
|---|---|---|
| Đăng ký tài khoản | Có backend, luồng chính đạt trên SMTP local | Register 202, tài khoản được lưu; thiếu điện thoại trả 400 dù FE ghi không bắt buộc |
| Gửi lại/xác minh OTP | Logic chính đã kiểm chứng; Gmail local đang lỗi | Gửi lại 202, verify 200; đăng nhập trước xác minh trả 403; Gmail trả 534 khi xác thực |
| Đăng nhập JWT | Luồng chính đạt | Sau OTP nhận login 200 và truy cập API có bảo vệ thành công |
| Refresh token / logout | Luồng chính đạt | Refresh 200; logout 200; dùng lại refresh token đã logout trả 401 |
| Quên/đặt lại mật khẩu | Đạt với SMTP local | Forgot 202 → đọc OTP trong mail local → reset 200 → đăng nhập mật khẩu mới 200; mail thật vẫn bị chặn |
| Đổi mật khẩu | Luồng API chính đạt | PUT mật khẩu với mật khẩu hiện tại đúng trả 200; FE có logic đưa về đăng nhập |
| Google/Microsoft login | Chưa triển khai hoạt động | Endpoint trả 503 `AUTH_EXTERNAL_LOGIN_NOT_CONFIGURED` |
| Hồ sơ cá nhân | Có FE + BE, đọc/sửa đã kiểm chứng | GET/PUT hồ sơ 200, trang profile gọi API thành công |
| Avatar | Có implementation FE + BE | Có upload/storage/validation và test liên quan; chưa upload ảnh thật trong đợt audit |
| Kỹ năng cá nhân | API đọc/cập nhật đạt | GET danh mục và PUT kỹ năng cá nhân 200; xác nhận kỹ năng của người khác chưa thử đủ vai trò |
| Phiên đăng nhập / thu hồi từng thiết bị | Đang dở | `getActiveSessions()` luôn trả `[]`; `revokeSession()` trả lỗi 501; khác với logout phiên hiện tại đã có |
| Trang For You | Tích hợp BE một phần đạt | API đọc công việc từ DB trả 200; khu vực dự án chịu lỗi API dự án; chưa có luồng CRUD issue hoàn chỉnh để dùng xuyên suốt |
| Tổ chức | Backend tạo/đọc đạt, FE bị lỗi đường dẫn | POST `/api/v1/organizations` 201; FE gọi `/api/organizations` → 404; chưa thấy màn hình quản trị tổ chức đầy đủ |
| Dự án: danh sách/tạo/sửa/cấu hình | BE có thực thi, FE lỗi, phân quyền thiếu | Tạo dự án 201; FE gọi thiếu `/v1`; người ngoài dự án vẫn PUT sửa được |
| Thành viên dự án | Backend hoạt động; trang thành viên chưa nối đúng | API thêm thành viên 201 và phát notification; nút mời ở trang thành viên gọi service chưa triển khai, không subscribe |
| Workflow / cấu hình Board / cột / WIP | Có BE và FE; tích hợp chưa hoàn chỉnh | API đọc trạng thái/transition 200; tạo board và thêm/sửa/xóa cột đều 200. Nhánh FE lấy dữ liệu dự án thiếu `/v1`; chưa thử mọi luật chuyển trạng thái |
| Task / Story / Bug / Epic / Subtask | Chủ yếu trạng thái trong FE | Tạo/sửa/xóa/clone/bulk/đổi người xử lý/priority/story points thao tác trên signal; chưa có controller CRUD issue tương ứng |
| Backlog và Sprint | Đang dở, chưa lưu bền vững | Thêm Sprint, cập nhật mục tiêu/ngày/trạng thái, chuyển việc chỉ cập nhật signal; reload mất dữ liệu |
| Kanban kéo thả / Task List | Có UI tương tác, chưa hoàn tất nghiệp vụ | Đổi trạng thái/người xử lý chỉ cập nhật state; không có lưu issue qua API ở các thao tác này |
| Drawer công việc / comment | Có UI, lưu local | Comment/subtask/lịch sử thay đổi trong state, chưa phải lịch sử đồng bộ DB |
| Trang `/issues/:id` | Trang giữ chỗ | Chỉ dùng `FeaturePlaceholderComponent`; chi tiết issue, attachment, acceptance criteria chưa thành trang thật |
| Roadmap | UI sơ bộ | Dữ liệu từ Sprint local; Sprint active luôn hiển thị 60%; nút Tuần/Tháng/Quý chưa có handler |
| Báo cáo Burndown/Velocity | Chưa phải báo cáo thật | Đường Burndown là tọa độ SVG cố định; nhãn “Đúng tiến độ” cố định; Velocity dùng tổng điểm local, chưa dựa trên điểm hoàn thành/lịch sử |
| Import/export Excel | CSV hỗ trợ sơ bộ; XLSX chưa đúng | Input nhận `.xlsx` nhưng đọc bằng `readAsText` và parser CSV; import chỉ tạo task trong state; export thực tế là CSV |
| Thông báo / SignalR | Luồng chính đã kiểm chứng | Inbox/read-all 200; thêm thành viên tạo thông báo, client SignalR nhận 1 sự kiện realtime |
| Quản trị role/permission | Backend có; FE lỗi hợp đồng dữ liệu | FE đưa permission code vào trường GUID `permissionIds` → 400; bỏ permission hiện có khi map role; tạo role chưa lưu các quyền đã chọn |
| Quản trị người dùng | Đang dở | `getUsers()` trong IdentityService luôn trả `[]`; đổi role người dùng/khóa mở tài khoản ở lớp này trả lỗi chưa tích hợp |
| AI Breakdown | Backend pipeline chạy; inference giả lập; FE apply lỗi | Generate 201 → get 200 → apply với ID đúng 200 và ghi issue thật; payload FE thiếu `suggestedTaskId` → 400 |
| AI gợi ý người xử lý (AI 2) | Chưa triển khai thực tế | `candidateScores` khởi tạo rỗng, mở panel không gọi ranker; chọn ứng viên chỉ đổi assignee trong state |
| AI Governance / model / log / quota | Đang dở | Models/logs trả mảng rỗng; toggle model chỉ sửa object và hiện thông báo, không gọi API lưu |
| AI prompt templates | Có API | Đọc API 200; có endpoint tạo/activate; chưa kiểm chứng đầy đủ luồng quản trị trên UI |
| AI DataOps / dataset / cleaning / training / evaluation | Chủ yếu schema, trang giữ chỗ | Route AI DataOps là placeholder; chưa thấy API nghiệp vụ cho các luồng này |
| Tìm kiếm / gần đây / đánh dấu / giao diện | Có logic UI | Chủ yếu trên dữ liệu frontend; chưa kiểm thử toàn diện keyboard, accessibility, trình duyệt và màn hình nhỏ |
| Docker / CI / cấu hình | Có khung triển khai, còn thiếu | MySQL local chạy; lint sẽ chặn job CI; E2E script có đường dẫn máy cá nhân; chưa kiểm thử deploy IIS/TEST từ đầu |

## Lỗi và thiếu sót ưu tiên

### A01 — P0: Người không thuộc dự án sửa được dự án

Đã tạo hai tài khoản riêng trên database bản sao. Tài khoản A tạo tổ chức và dự án. Tài khoản B chưa được thêm vào dự án:

- GET `/api/v1/projects/{id}/members` → **403 `RBAC_PERMISSION_DENIED`**.
- GET `/api/v1/projects/{id}` → **200**.
- PUT `/api/v1/projects/{id}` với tên/mô tả mới → **200**, thay đổi được chấp nhận.

Backend có fallback yêu cầu đăng nhập, nhưng thiếu kiểm tra quyền theo dự án ở các thao tác này. Đây là lỗi phân quyền giữa người dùng đã đăng nhập, không phải kết luận API mở cho người ẩn danh: GET dự án không có token đã trả 401.

Nguồn: `ProjectMgmt.Solution/Controllers/ProjectsController.cs:41,108`; `ProjectMgmt.Modules.Planning/ProjectManagement/Application/Services/ProjectManagementService.cs:93,281`. Cần rà soát tương tự delete, board và workflow; chưa thực hiện thao tác xóa dự án của người khác.

### A02 — P1: FE gọi sai URL nhóm tổ chức/dự án

`environment.apiBaseUrl` kết thúc bằng `/api`, nhưng service nối `/projects` và `/organizations`; BE dùng `/api/v1/projects` và `/api/v1/organizations`. Đã tái hiện 404 qua HTTP và Chrome. Ảnh hưởng danh sách, tạo, settings, workflow và board thuộc project.

Nguồn: `frontend/projectmgmt-web/src/app/core/services/project-management.service.ts:597,604,627,665,726,738`.

Không sửa bằng cách thêm `/v1` vào baseUrl toàn cục một cách máy móc: Account/Identity/RBAC đã tự nối `/v1`, còn board-column và workflow controller vẫn dùng `/api/boards`, `/api/workflow`.

### A03 — P1: Task/Sprint thao tác xong không lưu DB

Đã gọi đúng service mà giao diện sử dụng để tạo 1 task và 1 Sprint trong browser kiểm thử. Trước reload: `{tasks:1,sprints:1}`; sau reload: `{tasks:0,sprints:0}`. CRUD, kéo thả, comment, bulk và các trạng thái liên quan cần API persistence và tải dữ liệu lại.

Nguồn: `project-management.service.ts:254–593`. `DeliveryService.cs` và `PlanningService.cs` ở thư mục Application/Services hiện là file rỗng; không được hiểu điều này là toàn bộ Planning rỗng, vì `ProjectManagementService` đã có triển khai riêng.

### A04 — P1: AI Breakdown FE báo thành công dù backend từ chối

Drawer map gợi ý thành title/points/selected và làm mất `suggestedTaskId`. Khi apply, chỉ gửi title/estimatePoints. Với payload giống FE, backend trả **400 `AI_BREAKDOWN_SELECTION_INVALID`**. FE bỏ qua error, tạo subtask local và hiện toast thành công ngay. API chạy được nếu gửi ID đúng; vì vậy đây là lỗi tích hợp frontend cụ thể.

Nguồn: `frontend/projectmgmt-web/src/app/shared/components/task-detail-drawer/task-detail-drawer.ts:455–502`; `ProjectMgmt.Solution/Services/AiBreakdownApplyCoordinator.cs:81`.

### A05 — P1: Ma trận phân quyền gửi sai kiểu và hiển thị thiếu dữ liệu

- `IdentityService.updateRolePermissions()` truyền danh sách code, ví dụ `project.read`, cho API client đóng gói thành `permissionIds`.
- BE yêu cầu `List<Guid>`, payload này đã trả **400**.
- `getRoles()` luôn đặt `permissionCodes: []`, không giữ các quyền backend trả về.
- `createRole()` chỉ gửi name/scope/description; lựa chọn quyền mới chưa được lưu.
- `saveMatrix()` không xử lý lỗi trong subscribe nên loading có thể không kết thúc khi API lỗi.

Nguồn: `identity.service.ts:370–448`; `core/api/rbac.api.ts:26`; `features/identity/rbac-admin-page.ts`.

### A06 — P1: Thêm thành viên qua màn hình hiện không thực hiện

`ProjectMembersPageComponent.submitInvite()` gọi `ProjectManagementService.addProjectMember()` nhưng không subscribe; phương thức service này chỉ trả `throwError` “chưa được tích hợp”. Modal vẫn đóng và xóa ô nhập. Trong khi đó API RBAC thật đã thêm thành viên thành công, phát notification và realtime.

Nguồn: `features/projects/project-members-page.ts:129`; `core/services/project-management.service.ts:618`. Cần nối sang IdentityService/RbacApi hiện có và tải lại members.

### A07 — P1 môi trường: Gmail gửi OTP đang bị chặn

Kết nối TCP/TLS tới Gmail thành công nhưng SMTP AUTH trả **534 5.7.9 / WebLoginRequired**. Đây là blocker cấu hình/tài khoản gửi email ở môi trường hiện tại. Đăng ký, OTP, login, forgot/reset đã chạy được với SMTP local. Không được đổi `FallbackToLogOnFailure=true` rồi coi gửi email đã được sửa: tùy chọn này trả success ngay cả khi gửi thất bại.

### A08 — P2: Form ghi số điện thoại không bắt buộc, BE bắt buộc

Register không có phone đã trả **400 `AUTH_PHONE_REQUIRED`**. FE chỉ có pattern validator và nhãn “không bắt buộc”. Cần thống nhất quy tắc với yêu cầu nghiệp vụ.

Nguồn: `features/auth/auth-page.ts:77`, `features/auth/auth-page.html:147`; `AccountServices.cs` nhánh `AUTH_PHONE_REQUIRED`.

### A09 — P2: Chỉ số báo cáo và Roadmap chưa phản ánh tiến độ thật

Burndown SVG và nhãn “Đúng tiến độ” được ghi cứng. Roadmap active hiển thị 60% cố định. Không dùng các số này để đánh giá tiến độ thực tế.

Nguồn: `features/reports/reports-page.ts:40–53,90–92`; `features/roadmap/roadmap-page.ts`.

### A10 — P2: XLSX chưa được hỗ trợ dù UI cho chọn

Input chấp nhận CSV/XLSX nhưng parser chỉ nhận văn bản CSV; XLSX là định dạng khác, cần thư viện đọc workbook hoặc giới hạn giao diện về CSV. Parser CSV hiện cũng chưa xử lý đầy đủ ô rỗng, multiline và dấu quote escape. Chưa chạy bộ kiểm thử định dạng file biên; đây là kết luận đọc implementation.

Nguồn: `shared/components/excel-import-modal/excel-import-modal.ts:58` và `readFile`; `core/services/excel-data.service.ts:parseCsv`.

### A11 — P2: Kiểm thử và môi trường chưa chứng minh được chất lượng xuyên suốt

- Lint lỗi import `of`, có thể chặn job frontend CI trước test/build.
- `tests/e2e-chrome-test.cjs:1` require Playwright bằng đường dẫn tuyệt đối `C:/Users/hoang/...`, không chạy nguyên trạng trên máy hiện tại.
- Script E2E có nhiều `logSuccess` sau khi chỉ điều hướng, chưa phải assertion rằng dữ liệu hoặc nghiệp vụ đúng. Không dùng các dòng PASS đó làm bằng chứng tính năng hoàn thành.
- Test hiện có chưa phát hiện sai URL FE, mất task/Sprint sau reload, người ngoài sửa project hoặc payload AI/RBAC sai.
- `/health` chỉ `AddHealthChecks()` không đăng ký kiểm tra DB; HTTP 200 không chứng minh MySQL/SMTP hoạt động.

### A12 — Cấu hình và dữ liệu cần hoàn thiện trước triển khai

- File `appsettings.Development.json` đang được Git theo dõi và có giá trị SMTP credential không rỗng. Cần chuyển bí mật khỏi file theo dõi và kiểm tra/thu hồi credential nếu đó là credential thật; báo cáo không sao chép giá trị.
- Database local bắt đầu từ SQL bootstrap, đã được bổ sung các thay đổi schema ở lượt đồng bộ trước. Chưa có kiểm chứng quy trình upgrade tự động từ mọi phiên bản cũ; không chạy lại script bootstrap có DROP vào DB đang dùng.
- Chưa kiểm thử restore, tải cao, race condition với DB thật, toàn bộ role/scope matrix, expiry/reconnect SignalR, toàn bộ quy tắc upload avatar và deploy IIS/TEST.

## Thứ tự hoàn thiện đề xuất

1. Chặn truy cập/sửa dự án trái quyền và rà cùng nhóm board/workflow; thêm integration test hai người dùng.
2. Sửa URL FE–BE; hoàn thiện thêm thành viên, ma trận RBAC và payload AI Breakdown; xử lý lỗi đúng thay vì toast thành công sớm.
3. Thống nhất form đăng ký và cấu hình Gmail hợp lệ để luồng người dùng chạy được trên máy này.
4. Xây API CRUD issue/Sprint/comment/bulk, nối FE và kiểm thử reload/đa người dùng.
5. Đưa báo cáo/Roadmap sang dữ liệu thật, hoàn thiện phiên đăng nhập/AI governance/DataOps theo phạm vi được chọn.
6. Chuẩn hóa E2E chạy độc lập máy, sửa lint, bổ sung CI integration, health/readiness và quy trình migration/backup.

## Bằng chứng và trạng thái sau audit

- Kết quả HTTP, network và thử reload đã được tổng hợp trong `audit-project-2026-10-19-evidence.json`, không chứa access token, password hay OTP.
- File test backend TRX và dữ liệu tạm của lượt audit nằm ở `%TEMP%/projectmgmt-audit`. Thư và session kiểm thử không được đưa vào repo.
- Database bản sao `projectmgmt_audit_20261019` được giữ lại để tái hiện; dữ liệu test không nằm trong `projectmgmt` đang dùng.
- Backend kiểm thử 5084 và SMTP local 1026 được dừng khi kết thúc. FE 4200 và backend thường 5083 vẫn được giữ để bạn dùng.
- Đây là đánh giá chức năng hiện trạng, không phải tỷ lệ hoàn thành theo hợp đồng: project chưa có checklist nghiệm thu thống nhất để đưa ra con số phần trăm đáng tin cậy.
