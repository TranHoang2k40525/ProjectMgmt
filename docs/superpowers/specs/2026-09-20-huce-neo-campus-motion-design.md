# Thiết kế hệ thống chuyển động HUCE Neo Campus

Ngày: 2026-09-20  
Trạng thái: Thiết kế đã được duyệt trong hội thoại, chờ duyệt tài liệu trước khi lập kế hoạch triển khai  
Phạm vi: Angular frontend `frontend/projectmgmt-web`

## 1. Tóm tắt

HUCE Neo Campus là lớp trải nghiệm chuyển động mới cho ProjectMgmt. Hệ thống kết hợp:

- GSAP để điều phối timeline, chuyển trang, thay đổi bố cục, phản hồi thao tác và hoạt ảnh theo vùng nhìn.
- Three.js để dựng campus HUCE low-poly kết hợp robot AI, hologram, drone, nhân vật và vật thể theo ngữ cảnh.
- Cơ chế tự điều chỉnh chất lượng theo thiết bị, hướng xoay màn hình, khả năng WebGL và lựa chọn giảm chuyển động của người dùng.

Hệ thống không thay đổi nghiệp vụ, API, cấu trúc route hoặc bố cục desktop hiện tại. Motion và 3D là lớp trình bày độc lập, có thể tắt hoặc thay bằng fallback mà ứng dụng vẫn hoạt động đầy đủ.

## 2. Design read và mục tiêu chất lượng

Đọc thiết kế: ứng dụng quản lý dự án B2B dành cho sinh viên, giảng viên và nhóm nghiên cứu HUCE; có mật độ thông tin cao nhưng cần một bản sắc hiện đại, thân thiện và có yếu tố AI rõ ràng.

- `DESIGN_VARIANCE = 9`: cảnh và chuyển động có chiều sâu, khác biệt theo nhóm chức năng.
- `MOTION_INTENSITY = 9`: chuyển cảnh có biên đạo, tương tác 3D và phản hồi trạng thái rõ ràng.
- `VISUAL_DENSITY = 7`: giữ mật độ dữ liệu của ứng dụng quản lý; motion không che hoặc cạnh tranh với nội dung.

Mức 9 không có nghĩa mọi phần tử đều chuyển động. Mỗi hoạt ảnh phải truyền đạt ít nhất một trong bốn mục đích: phân cấp, kể chuyện, phản hồi hoặc chuyển trạng thái.

## 3. Nguyên tắc trải nghiệm

1. Nội dung và thao tác luôn quan trọng hơn trang trí.
2. Chuyển cảnh theo route có quy luật cố định, không chọn ngẫu nhiên.
3. Hoạt ảnh có thể bị ngắt khi người dùng thao tác tiếp; không khóa UI để chờ animation.
4. Không chiếm quyền cuộn của trình duyệt trên các trang nghiệp vụ.
5. Không dùng âm thanh tự động.
6. Không dùng con trỏ tùy biến. Phản ứng theo chuột chỉ tác động camera hoặc vật thể trang trí.
7. Màn cảm ứng không nhận hiệu ứng hover/magnetic giả.
8. `prefers-reduced-motion: reduce` đưa hệ thống về trạng thái gần như tĩnh.
9. Desktop giữ nguyên cấu trúc, kích thước và luồng đọc. Motion chỉ bổ sung lớp thời gian và chiều sâu.
10. Điện thoại và iPad hỗ trợ cả dọc lẫn ngang, không phát sinh tràn ngang document.

## 4. Ngôn ngữ hình ảnh HUCE Neo Campus

### 4.1 Phần campus low-poly

- Mô hình campus và Scrum Lab thu nhỏ.
- Sinh viên, giảng viên, Product Owner, Scrum Master và kỹ sư.
- Bàn làm việc, laptop, bảng Kanban, bản vẽ công trình, mô hình tòa nhà, cây, mây, đèn và các vật thể học tập.
- Hình khối thân thiện, dễ đọc ở kích thước nhỏ; màu HUCE và xanh chủ đạo hiện có được giữ làm nền tảng.

### 4.2 Phần AI tương lai

- Robot trợ lý AI là nhân vật kết nối xuyên suốt.
- Drone vận chuyển task hoặc thông báo.
- Hologram hiển thị Sprint, Burndown, token, node và trạng thái thời gian thực.
- Dòng dữ liệu, vòng quét và ánh sáng có tiết chế; không dùng glow phủ toàn màn hình.

### 4.3 Hoạt động sống

- Nhân vật: idle, trao đổi, chỉ bảng, gõ máy, đi bộ, vẫy tay, ngồi và phản hồi thành công.
- Robot: idle, bay, quét dữ liệu, chỉ dẫn, đồng ý, cảnh báo và ăn mừng ngắn.
- Vật thể: thẻ task di chuyển, màn hình cập nhật, cây và mây chuyển động nhẹ, drone bay theo tuyến, đèn đổi cường độ theo cảnh.
- Pointer: camera parallax nhẹ; mắt hoặc đầu nhân vật gần nhất có thể theo hướng trỏ trong giới hạn nhỏ.
- Raycasting chỉ bật cho một số vật thể có phản hồi rõ ràng; không biến toàn cảnh thành trò chơi khó đoán.

## 5. Thiết kế cảnh theo chức năng

### 5.1 Auth Campus Scene

Đây là scene đầy đủ nhất và được lazy-load cùng route `/auth`.

| Chế độ | Câu chuyện hình ảnh |
|---|---|
| Đăng nhập | Camera đi qua campus và dừng tại Scrum Lab, nơi nhóm đang làm việc quanh bảng Kanban |
| Đăng ký | Bàn làm việc mới được lắp ghép, nhân vật mới bước vào và robot chào đón |
| OTP | Sáu khối mã đi qua vòng hologram rồi ghép vào thiết bị xác thực |
| Quên mật khẩu | Drone bật một tuyến sáng dẫn tới trạm bảo mật |
| Xác thực thành công | Robot xác nhận, màn hình chuyển xanh trong thời lượng ngắn rồi điều hướng |
| Lỗi xác thực | Màn hình rung rất nhẹ và robot báo mất tín hiệu; không dùng hiệu ứng gây căng thẳng kéo dài |

Form vẫn là DOM HTML thật để bảo đảm nhập liệu, autofill, bàn phím và trợ năng. Canvas không nhận pointer ở khu vực form.

### 5.2 For You Command Center

- Scene nhỏ hơn, thể hiện campus command center đang hoạt động.
- Dự án, sprint và số task được ánh xạ thành vật thể trang trí, không dùng dữ liệu nhạy cảm trong canvas.
- Chỉ render khi scene nằm trong viewport.

### 5.3 AI Governance Lab

- Lõi AI hologram, node mô hình, dòng token và tín hiệu giám sát.
- Card model vẫn là DOM và giữ khả năng thao tác.
- Scene chỉ hỗ trợ phân cấp và không làm nền cho bảng log để giữ độ đọc.

### 5.4 Context scene và empty state

- Reports: robot phân tích dữ liệu, sau đó đường biểu đồ DOM/SVG được vẽ.
- Projects: nhóm nhân vật xem bản đồ dự án hoặc blueprint.
- Empty Backlog/Board: nhân vật đang chuẩn bị bảng hoặc chờ task.
- Error state: robot mất tín hiệu hoặc sửa thiết bị.
- Success state: phản hồi ngắn, không dùng pháo hoa liên tục.

Backlog, Board, Task List, RBAC và các bảng dữ liệu không chạy scene WebGL lớn phía sau nội dung.

## 6. Motion profile theo route

| Nhóm route | Chuyển cảnh chính | Hoạt ảnh nội dung |
|---|---|---|
| Auth | Camera dolly, hologram morph | Timeline theo mode, form cross-fade có chiều sâu |
| For You/Projects | Soft depth zoom | Header trước, card theo nhóm sau |
| Backlog | Planning flow | Task reveal theo nhịp; Flip khi đổi nhóm |
| Board | Horizontal column sweep | Cột và card stagger; Flip khi chuyển cột |
| Roadmap | Blueprint sweep | Timeline được mở theo chiều thời gian |
| Reports | Data beam | Line draw, bar grow và counter |
| Profile/Identity | Scanner reveal | Section hội tụ, badge phản hồi nhẹ |
| AI Governance | Node convergence | Model card và log xuất hiện theo lớp |
| Settings | Precision slide | Form section reveal ngắn, không gây phân tâm |

Thời lượng chuẩn:

- Phản hồi nút: 120-220ms.
- Modal, drawer, toast: 220-420ms.
- Route transition: 420-700ms.
- Auth cinematic lần đầu: 1.2-1.8 giây.
- Người dùng quay lại trong cùng phiên nhận bản rút gọn; không phát lại cinematic dài.

## 7. Kiến trúc phần mềm

### 7.1 Thành phần dùng chung

```text
src/app/core/motion/
  motion-orchestrator.service.ts
  route-transition.service.ts
  scene-quality.service.ts
  motion-preferences.service.ts
  motion-profile.ts

src/app/shared/motion/
  motion.directive.ts
  route-transition-layer/
  three-scene-host/
  scene-loading-fallback/

src/app/shared/scenes/
  auth-campus/
  command-center/
  ai-lab/
  empty-state/

public/assets/3d/
  characters/
  robots/
  campus/
  props/
  textures/
  licenses/
```

### 7.2 Trách nhiệm

- `MotionPreferencesService`: đọc reduced motion, coarse pointer, save-data, visibility và capability.
- `SceneQualityService`: chọn `full`, `balanced`, `lite` hoặc `static`.
- `MotionOrchestratorService`: tạo timeline dùng chung, giữ token duration/ease và dọn animation.
- `RouteTransitionService`: ánh xạ route sang motion profile cố định.
- `MotionDirective`: reveal theo scope, không dùng selector toàn cục.
- `ThreeSceneHostComponent`: quản lý canvas, renderer, camera, resize, animation loop và dispose.
- Mỗi scene tự sở hữu model, mixer, light, raycaster và cleanup của mình.

### 7.3 Vòng đời Angular

- Khởi tạo DOM animation sau khi view đã sẵn sàng.
- GSAP selector luôn scope vào `ElementRef` hoặc component root.
- Dùng `gsap.context()` hoặc `gsap.matchMedia()`; gọi `revert()` khi component bị hủy.
- Event listener được gỡ trong cleanup.
- Three.js chạy ngoài Angular zone để không kích hoạt change detection mỗi frame.
- Renderer dùng `setAnimationLoop()` và được dừng trước khi dispose.
- Router transition không trì hoãn navigation nếu animation lỗi.

### 7.4 Plugin

- GSAP core và timeline cho motion cơ bản.
- `Flip` cho thay đổi vị trí card, filter và layout.
- `ScrollTrigger` chỉ cho reveal theo viewport, biểu đồ và section cần kể chuyện.
- Không dùng ScrollSmoother cho dashboard.
- Không đưa React hoặc `@gsap/react` vào dự án Angular.

## 8. Asset và giấy phép

Nguồn ưu tiên:

- Quaternius Universal Base Characters: glTF, rigged, CC0.
- Quaternius Low-Poly Robot: 14 hoạt ảnh, CC0.
- Kenney Blocky Characters và các prop tương thích: CC0.

Quy tắc:

1. Mọi asset tải về được lưu trong repository hoặc nguồn artifact ổn định, không hotlink khi chạy production.
2. Mỗi pack có file nguồn, URL, phiên bản/ngày tải và giấy phép trong `public/assets/3d/licenses`.
3. Không trộn nhiều phong cách model nếu palette, tỷ lệ đầu/thân và vật liệu không đồng nhất.
4. Model được kiểm tra bằng glTF validator trước khi dùng.
5. Geometry lớn được cân nhắc Draco; texture ưu tiên WebP hoặc KTX2.
6. Không thêm asset không rõ quyền sử dụng.

## 9. Chất lượng theo thiết bị

| Profile | Thiết bị | Đặc điểm |
|---|---|---|
| `full` | Desktop GPU phù hợp, màn rộng | Scene đầy đủ, shadow giới hạn, pointer parallax, nhiều nhân vật |
| `balanced` | iPad hoặc laptop trung bình | Ít nhân vật nền, shadow đơn giản, particle giảm |
| `lite` | Điện thoại hoặc thiết bị yếu | Scene đơn giản, ít mixer, không post-processing |
| `static` | Reduced motion, save-data, WebGL lỗi | Poster hoặc nền CSS tĩnh; ứng dụng đầy đủ chức năng |

Landscape có camera framing riêng, không chỉ dùng lại camera portrait.

Ngân sách ban đầu:

- Không tăng initial application chunk bằng Three.js; Three.js phải nằm trong lazy chunk.
- Auth scene tải phần bắt buộc trước, asset phụ tải sau khi form đã tương tác được.
- Device pixel ratio của renderer được giới hạn, mặc định không vượt quá 1.5.
- Pause loop khi document hidden hoặc scene ngoài viewport.
- Dùng `InstancedMesh` cho cây, đèn, node và task lặp lại.
- Số lượng light có shadow và material trong mỗi scene phải được giới hạn.

## 10. Fallback và xử lý lỗi

- Nếu dynamic import Three.js thất bại, hiển thị poster/CSS background và tiếp tục form.
- Nếu model lỗi, scene bỏ qua model đó; không làm route thất bại.
- Nếu WebGL context bị mất, thử khôi phục một lần; sau đó chuyển sang static fallback.
- Loading scene dùng skeleton hoặc poster có kích thước cố định để tránh CLS.
- Route transition có timeout an toàn và luôn giải phóng overlay.
- Animation không được quyết định trạng thái nghiệp vụ. Trạng thái Angular là nguồn sự thật.

## 11. Trợ năng

- Tôn trọng `prefers-reduced-motion` trong CSS và `gsap.matchMedia()`.
- Reduced motion bỏ parallax, camera fly, perpetual motion, scrub và stagger dài.
- Canvas trang trí dùng `aria-hidden="true"` và không nằm trong tab order.
- Nội dung/nhãn trong canvas phải có bản DOM tương đương nếu mang ý nghĩa.
- Focus ring, keyboard navigation, screen reader và autofill không phụ thuộc animation.
- Không flash nhanh, không rung mạnh và không chuyển camera đột ngột.
- Contrast hiện có không bị giảm bởi overlay hoặc scene.

## 12. Data flow

```text
Router Navigation
      |
      v
RouteTransitionService -----> motion profile
      |                             |
      v                             v
Transition Layer              GSAP timeline
      |
      v
Angular route active -----> page-scoped motion directive
      |
      +---- optional ----> ThreeSceneHost ----> scene module
                                  |
                                  +---- quality/profile/preferences
```

Scene nhận config trình bày tối thiểu. Scene không gọi API nghiệp vụ trực tiếp và không giữ dữ liệu nhạy cảm.

## 13. Kiểm thử và tiêu chí chấp nhận

### 13.1 Viewport

- `390 x 844`: điện thoại dọc.
- `844 x 390`: điện thoại ngang.
- `768 x 1024`: iPad dọc.
- `1024 x 768`: iPad ngang.
- `1440 x 900`: desktop.

### 13.2 Kiểm thử chức năng

- Đăng nhập, đăng ký, OTP và quên mật khẩu dùng được khi scene đang tải, lỗi hoặc bị tắt.
- Chuyển route liên tục không để lại overlay hoặc khóa pointer.
- Modal, drawer, toast và form giữ focus đúng.
- Board/Backlog đổi trạng thái đúng khi Flip bị tắt.
- Xoay màn hình không nhân đôi renderer, mixer, listener hoặc ScrollTrigger.

### 13.3 Kiểm thử kỹ thuật

- Build, lint và unit test đạt.
- Không có console error, unhandled rejection hoặc WebGL warning lặp lại.
- Không có document-level horizontal overflow.
- `prefers-reduced-motion` đưa scene về fallback đúng.
- Browser không giữ animation loop sau khi rời route.
- Kiểm tra memory qua nhiều chu kỳ vào/rời auth và AI Governance.
- Desktop layout snapshot không thay đổi ngoài lớp motion được chấp nhận.
- Lighthouse và profile hiệu năng được chạy cho auth, For You và AI Governance.

## 14. Thứ tự triển khai dự kiến

1. Hoàn tất và khóa responsive foundation đang có.
2. Thêm motion tokens, preferences, quality profile và lifecycle foundation.
3. Thêm route transition layer và animation dùng chung cho modal/drawer/toast.
4. Xây Auth Campus Scene với static fallback trước.
5. Kết nối các mode login, signup, OTP và forgot vào scene timeline.
6. Thêm motion profile cho các nhóm route nghiệp vụ.
7. Thêm Command Center, AI Lab và contextual empty states.
8. Tối ưu asset, Draco/KTX2, pause/dispose và device tiers.
9. Chạy toàn bộ test matrix, accessibility và performance audit.

## 15. Ngoài phạm vi

- Không biến ứng dụng thành game hoặc thêm điều khiển di chuyển nhân vật.
- Không thêm âm thanh tự phát.
- Không thay API/backend hoặc schema dữ liệu.
- Không thay route, quyền truy cập hoặc nội dung nghiệp vụ.
- Không chạy WebGL toàn màn hình trên mọi trang.
- Không yêu cầu người dùng chờ cinematic mới được nhập form.
- Không đưa React vào Angular frontend.

## 16. Nguồn kỹ thuật

- GSAP matchMedia: https://gsap.com/docs/v3/GSAP/gsap.matchMedia%28%29/
- GSAP quickTo: https://gsap.com/docs/v3/GSAP/gsap.quickTo%28%29/
- GSAP Flip: https://gsap.com/docs/v3/Plugins/Flip/
- GSAP ScrollTrigger: https://gsap.com/docs/v3/Plugins/ScrollTrigger/
- Three.js responsive design: https://threejs.org/manual/pages/responsive.html
- Three.js WebGLRenderer: https://threejs.org/docs/pages/WebGLRenderer.html
- Three.js GLTFLoader: https://threejs.org/docs/pages/GLTFLoader.html
- Three.js AnimationMixer: https://threejs.org/docs/pages/AnimationMixer.html
- Three.js InstancedMesh: https://threejs.org/docs/pages/InstancedMesh.html
- Three.js DRACOLoader: https://threejs.org/docs/pages/DRACOLoader.html
- Khronos glTF/KTX2: https://www.khronos.org/gltf/
- Quaternius Universal Base Characters: https://quaternius.com/packs/universalbasecharacters.html
- Quaternius Low-Poly Robot: https://quaternius.itch.io/lowpoly-robot
- Kenney Blocky Characters: https://www.kenney.nl/assets/blocky-characters
