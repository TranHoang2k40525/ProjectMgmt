# Kế hoạch triển khai HUCE Neo Campus Motion

Ngày: 2026-09-20  
Spec: `docs/superpowers/specs/2026-09-20-huce-neo-campus-motion-design.md`  
Frontend: `frontend/projectmgmt-web`

## Mục tiêu

Triển khai hệ thống chuyển động GSAP và bối cảnh Three.js cho ProjectMgmt theo spec đã duyệt, giữ nguyên nghiệp vụ và cấu trúc desktop, có fallback hoàn chỉnh cho mobile, thiết bị yếu và reduced motion.

## Nguyên tắc thực hiện

- Hoàn tất responsive baseline trước khi thêm motion.
- Mỗi lớp có build/lint/test độc lập.
- Motion không được nắm giữ trạng thái nghiệp vụ.
- Mọi selector GSAP phải scope trong component hoặc page host.
- Mọi timeline, listener, animation loop và GPU resource phải có cleanup.
- Three.js luôn lazy-load và có static fallback.
- Không stage hoặc sửa các script Jira và file test responsive ngoài phạm vi.

## Giai đoạn 0: Khóa responsive baseline

1. Rà diff responsive hiện có và loại bỏ file kiểm thử tạm do agent tạo.
2. Chạy build, lint, unit test.
3. Kiểm tra các route đại diện ở phone portrait, phone landscape, iPad portrait, iPad landscape và desktop.
4. Sửa lỗi responsive còn lại nhưng không thay layout từ `1280px` trở lên.
5. Commit riêng responsive implementation.

Tiêu chí hoàn tất:

- Build/lint/test đạt.
- Không có document-level horizontal overflow.
- Desktop giữ bố cục cũ.

## Giai đoạn 1: Motion foundation

Tạo:

- `src/app/core/motion/motion-profile.ts`
- `src/app/core/motion/motion-preferences.service.ts`
- `src/app/core/motion/scene-quality.service.ts`
- `src/app/core/motion/motion-orchestrator.service.ts`
- `src/app/core/motion/route-transition.service.ts`
- `src/app/shared/motion/motion.directive.ts`
- `src/app/shared/motion/route-transition-layer/*`

Công việc:

1. Định nghĩa duration/ease/profile theo nhóm route.
2. Đọc reduced motion, pointer type, save-data, visibility và WebGL capability.
3. Dùng `gsap.matchMedia()` cho desktop/tablet/mobile và cleanup.
4. Tạo page entrance, card stagger, KPI counter, modal/drawer/toast transitions.
5. Gắn route transition vào App Shell qua sự kiện activate của router outlet.

Kiểm thử:

- Unit test profile mapping và quality selection.
- Điều hướng nhanh không để overlay hoặc inline transform sót lại.

## Giai đoạn 2: Three.js scene foundation

Dependency:

- Cài `three` đúng phiên bản ổn định tương thích toolchain hiện tại.

Tạo:

- `src/app/shared/motion/three-scene-host/*`
- `src/app/shared/motion/scene-loading-fallback/*`
- `src/app/shared/scenes/scene-contract.ts`
- `src/app/shared/scenes/shared/*`

Công việc:

1. Dynamic import Three.js chỉ khi scene đủ điều kiện chạy.
2. Tạo renderer/camera/resize loop ngoài Angular zone.
3. Giới hạn DPR và hỗ trợ camera riêng cho portrait/landscape.
4. Pause khi hidden/offscreen.
5. Dispose geometry, material, texture, mixer và renderer.
6. Fallback CSS/poster khi WebGL hoặc dynamic import lỗi.

Kiểm thử:

- Mount/unmount nhiều lần không nhân đôi canvas hoặc loop.
- Reduced motion và lite profile không tạo renderer nặng.

## Giai đoạn 3: Auth Campus Scene

Tạo:

- `src/app/shared/scenes/auth-campus/*`
- `public/assets/3d/licenses/*`

Scene đầu tiên dùng hình khối low-poly code-native để có thể chạy ngay và không phụ thuộc mạng; cấu trúc loader vẫn hỗ trợ thay bằng glTF CC0 sau đó.

Nội dung:

- Campus/Scrum Lab, bàn, laptop, Kanban board, cây, đèn và skyline.
- Nhóm nhân vật low-poly với idle, làm việc, trao đổi và chào đón.
- Robot AI, drone, hologram, task blocks và data nodes.
- Camera parallax bằng `gsap.quickTo()`; raycasting giới hạn ở robot và task.
- Timeline riêng cho login, signup, OTP và forgot password.

Tích hợp:

- Cập nhật `auth-page.ts/html/scss`.
- Form là DOM thật và luôn tương tác trước khi scene tải xong.
- Scene đổi trạng thái khi `mode()` thay đổi.

## Giai đoạn 4: Chuyển trang và motion theo chức năng

1. App Shell: route transition layer, nav feedback, drawer/toast motion.
2. For You/Projects: depth reveal và command-center vignette.
3. Backlog/Board: stagger theo cột và GSAP Flip cho thay đổi layout phù hợp.
4. Roadmap: blueprint sweep không chiếm quyền scroll.
5. Reports: SVG line draw, bar grow và counter.
6. Profile/RBAC: scanner reveal có tiết chế.
7. AI Governance: node/data motion và AI Lab vignette.
8. Settings/forms: precision slide ngắn, không cản nhập liệu.
9. Modal/drawer/confirm/import: entrance/exit có cleanup và focus không đổi.

## Giai đoạn 5: Context scene và asset pipeline

1. Thêm scene nhỏ dùng chung cho For You và AI Governance.
2. Tạo empty/error/success variants bằng nhân vật và robot.
3. Nếu tải asset CC0, lưu model cục bộ và thêm file nguồn/giấy phép.
4. Chỉ dùng Draco/KTX2 khi đo được lợi ích thực tế.
5. Dùng instancing cho cây, đèn, node và task lặp lại.

## Giai đoạn 6: Kiểm thử cuối

Chạy:

- `npm run build`
- `npm run lint`
- `npm test -- --watch=false`

Audit các viewport:

- 390 x 844
- 844 x 390
- 768 x 1024
- 1024 x 768
- 1440 x 900

Audit bổ sung:

- Reduced motion.
- Touch/coarse pointer.
- Landscape rotation không reload.
- Điều hướng liên tục.
- Tab background/foreground.
- WebGL fallback.
- Memory sau nhiều chu kỳ mount/unmount.
- Desktop layout snapshot.

## Commit dự kiến

1. `feat(frontend): finish responsive adaptive layout`
2. `feat(motion): add GSAP orchestration foundation`
3. `feat(three): add responsive Three.js scene host`
4. `feat(auth): build HUCE Neo Campus auth experience`
5. `feat(motion): animate routes and product interactions`
6. `test(frontend): verify responsive motion experience`

## Điều kiện dừng

Không hoàn tất nếu còn một trong các lỗi sau:

- Build/lint/test thất bại.
- Route bị khóa vì animation.
- Form Auth không dùng được khi scene lỗi.
- WebGL loop còn chạy sau khi rời route.
- Reduced motion vẫn chạy camera/parallax/perpetual animation.
- Document bị tràn ngang.
- Desktop bị đổi bố cục ngoài phạm vi đã duyệt.
