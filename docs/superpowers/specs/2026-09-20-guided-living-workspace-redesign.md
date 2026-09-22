# Thiết kế Guided Living Workspace cho ProjectMgmt

Ngày: 2026-09-20  
Trạng thái: Hướng thiết kế đã được duyệt trong hội thoại; tài liệu này là mốc duyệt cuối trước khi sửa code  
Phạm vi: Angular frontend `frontend/projectmgmt-web`  
Thay thế: `2026-09-20-huce-neo-campus-motion-design.md` và toàn bộ scene low-poly Neo Campus hiện tại

## 1. Quyết định thiết kế

ProjectMgmt sẽ không tiếp tục dùng cảnh 3D low-poly làm nền cho Auth, For You hoặc AI Governance. Cảnh đó tạo chuyển động nhưng không giúp người dùng quyết định, đồng thời làm tăng tải GPU và cạnh tranh với nội dung.

Thiết kế mới có hai lớp rõ ràng:

1. **Sylva Arrival**: trải nghiệm mở đầu trước đăng nhập, dùng nguyên bản ThreeUI `SylvaHero` biến thể `living-green`. Đây là khoảnh khắc thương hiệu, thư giãn và tạo ấn tượng.
2. **Guided Workspace**: sau đăng nhập, giao diện trở về một công cụ quản lý dự án rõ ràng. Chuyển động chỉ dùng để chỉ đúng việc cần làm, giải thích thay đổi và xác nhận phản hồi.

Không chạy WebGL lớn phía sau các trang nghiệp vụ. Cảm giác “sống” trong workspace đến từ dữ liệu, trạng thái và micro-interaction có mục đích chứ không đến từ vật thể 3D trang trí.

## 2. Vấn đề người dùng cần giải quyết

Rà soát hiện trạng dưới góc nhìn người dùng cho thấy:

- Trang For You mở đầu bằng một scene nhưng không nói việc gì quan trọng nhất.
- Dự án, tab công việc, sidebar và shortcut có nhiều lựa chọn ngang cấp; người mới không biết nên bấm đâu trước.
- Người quay lại phải tự nhớ task đang làm dở.
- Thông báo và công việc tồn tại ở hai nơi nhưng chưa hợp nhất thành một lời gọi hành động.
- Icon có hover nhưng không chủ động báo “đây là việc cần chú ý”.
- Chuyển động liên tục ở nhiều nơi làm giảm hiệu năng nhưng không giảm gánh nặng nhận thức.
- Sau đăng nhập hiện đang điều hướng tới Profile, trong khi mục tiêu phổ biến là quay lại công việc.

Thiết kế mới phải trả lời ngay ba câu hỏi:

1. Tôi cần làm gì tiếp theo?
2. Vì sao việc đó quan trọng lúc này?
3. Tôi bấm đâu để bắt đầu hoặc tiếp tục?

## 3. Người dùng và ý định chính

Guided Workspace thích nghi theo vai trò hiện có, không tạo thêm vai trò giả:

| Vai trò | Ý định thường gặp khi mở ứng dụng | Tín hiệu được ưu tiên |
|---|---|---|
| System Administrator | Kiểm tra cảnh báo, quyền, cấu hình hệ thống | Cảnh báo bảo mật/hệ thống chưa đọc; cấu hình cần xử lý |
| Project Administrator | Điều hành dự án và workflow | Sprint hiện tại, mục nghẽn, thành viên hoặc thiết lập còn thiếu |
| Product Owner | Sắp xếp ưu tiên và chuẩn bị backlog | Story ưu tiên cao, backlog chưa sẵn sàng, sprint kế tiếp |
| Scrum Master | Giữ luồng sprint thông suốt | Work in progress, review chờ lâu, tiến độ sprint |
| Developer Engineer | Tiếp tục task đang làm | Task được gán ở trạng thái In Progress, sau đó High/Urgent To Do |
| QA / Tester | Xác minh thay đổi và lỗi | Code Review, Bug Urgent/High, task cần xác nhận |
| Guest Viewer | Nắm tình hình | Tổng quan và báo cáo; không gợi ý hành động vượt quyền |

Khi dữ liệu thật chưa đủ để xác định hạn chót hoặc blocker, giao diện không được bịa ra hai tín hiệu này.

## 4. Luồng trải nghiệm

### 4.1 Lần đầu hoặc phiên mới: Sylva Arrival

- Route `/auth` mở một màn hình Sylva toàn viewport.
- File HTML, Three.js runtime, font và ảnh được sao chép đúng byte từ nguồn ThreeUI đã đăng ký và giữ nguyên hash.
- Dock, cây rêu, hoa, dương xỉ, phấn hoa, bướm, pointer parallax và liquid-metal control giữ hành vi gốc.
- Nhấn mục **Enter** trong dock mở panel Auth HUCE ở lớp Angular phía trên.
- Có nút DOM rõ ràng **Bỏ qua và đăng nhập** cho người quay lại, bàn phím và thiết bị không dùng pointer.
- Sau lần mở đầu tiên trong cùng phiên, truy cập lại `/auth` có thể đi thẳng vào panel đăng nhập; người dùng vẫn có nút xem lại cảnh.
- Thiết bị bật reduced motion, save-data hoặc không đủ WebGL được đưa tới panel Auth cùng poster/fallback tĩnh, không chặn đăng nhập.

### 4.2 Đăng nhập

- Panel Auth là DOM Angular thật, không nằm trong canvas: hỗ trợ autofill, validation, bàn phím, trình đọc màn hình và OTP.
- Panel xuất hiện như một lớp kính tối, có độ tương phản cao; cảnh Sylva lùi nhẹ bằng opacity/scale chứ không tiếp tục tranh chú ý.
- Đăng nhập thành công điều hướng đến `/for-you`, không đưa người dùng sang Profile.
- Chỉ trạng thái thành công quan trọng mới có một brand moment ngắn; lỗi chỉ rung/đổi trạng thái rất nhẹ và kèm thông báo bằng chữ.

### 4.3 Sau đăng nhập: Guided Workspace

Phần đầu For You trở thành **Hôm nay của bạn**, theo thứ tự:

1. Lời chào ngắn theo thời điểm và vai trò.
2. Một thẻ **Bắt đầu từ đây** với hành động quan trọng nhất.
3. Một vùng **Tiếp tục việc đang dở** nếu có lịch sử thao tác hoặc task In Progress.
4. Một hàng **Cần bạn chú ý** gồm tối đa ba mục có lý do cụ thể.
5. Dự án và danh sách công việc còn lại nằm phía dưới.

Không dùng một lưới nhiều card bằng nhau cho các tín hiệu khác mức quan trọng. Hành động chính lớn hơn và có cấu trúc bất đối xứng; các mục phụ nhỏ, tĩnh và dễ quét.

### 4.4 Khi người dùng không thao tác

- Hành động chính chạy một nudge ngắn sau khi nội dung ổn định: icon thực hiện cử chỉ, đường dẫn sáng đi tới CTA và nhãn lý do xuất hiện.
- Nudge dừng sau tối đa ba nhịp, không lặp vô hạn.
- Nếu sau một khoảng yên lặng người dùng vẫn chưa thao tác, helper đổi câu chữ một lần, không bật popup và không che nội dung.
- Ngay khi người dùng hover, focus, cuộn hoặc thao tác, animation gọi chú ý dừng để giao diện nhường quyền chủ động.

## 5. Bộ máy chọn “việc tiếp theo”

### 5.1 Dữ liệu sử dụng

Chỉ dùng dữ liệu đã tồn tại:

- `IdentityService.authState().currentUser`
- Vai trò và permission hiện có
- `IdentityService.notifications` và `actionUrl`
- `ProjectManagementService.workItems`
- `currentProject`, sprint, priority, status, assignee và `updatedAt`
- Route/task cuối đã mở được lưu cục bộ

### 5.2 Quy tắc xếp hạng

Xếp hạng là xác định được và có thể kiểm thử, không random:

1. Mục Urgent phù hợp vai trò và quyền.
2. Task đang `In Progress` được gán cho người dùng.
3. Mục `Code Review` đối với QA, lead hoặc vai trò điều phối.
4. Thông báo chưa đọc có `actionUrl` và liên quan hành động.
5. Task High được gán nhưng chưa bắt đầu.
6. Mục tiêu sprint hoặc màn tổng quan phù hợp vai trò.

Nếu nhiều mục bằng điểm, ưu tiên mục được cập nhật gần nhất rồi đến `issueKey` để kết quả ổn định.

### 5.3 Giải thích quyết định

Mỗi gợi ý phải có câu lý do dạng người dùng hiểu được, ví dụ:

- “Bạn đang xử lý task này — tiếp tục từ lần trước.”
- “Ưu tiên cao và đang nằm trong sprint hiện tại.”
- “Đang chờ review; vai trò QA của bạn có thể xử lý.”
- “Thông báo mới có hành động cần xác nhận.”

Không hiển thị điểm số nội bộ hoặc thuật ngữ kỹ thuật của thuật toán.

## 6. Hệ thống chú ý và chuyển động

### 6.1 Quy tắc một tiêu điểm

- Tại một thời điểm chỉ có **một** chuyển động chủ động gọi người dùng.
- Animation phụ chỉ chạy do hover/focus/press hoặc do trạng thái vừa thay đổi.
- Không cho nhiều icon bounce, pulse hoặc glow cùng lúc.
- Chuyển động phải trỏ tới một thao tác thật; nếu không có CTA thì không chạy animation gọi chú ý.

### 6.2 Motion theo ngữ nghĩa

| Ngữ nghĩa | Chuyển động | Thời lượng dự kiến |
|---|---|---|
| Gọi hành động chính | halo thở nhẹ + icon nghiêng/chuyển 4–8px + copy xuất hiện | 650–900ms, tối đa 3 nhịp |
| Hover/focus | icon phản ứng một cử chỉ phù hợp, card nâng 2–3px | 100–180ms |
| Chọn/xác nhận | icon hoàn tất, progress hoặc check chuyển trạng thái | 180–320ms |
| Mở drawer/modal | chuyển từ chính phần tử đã bấm để giữ quan hệ không gian | 220–380ms |
| Chuyển route | outgoing giảm nhẹ, incoming xuất hiện theo điểm nhìn | 280–520ms |
| Thành công quan trọng | một phản hồi thương hiệu ngắn, sau đó trở về tĩnh | dưới 900ms |

Animation chỉ dùng `transform` và `opacity` trong luồng thường. Không animate layout như `width`, `height`, `top`, `left`; không dùng `transition: all`.

### 6.3 Icon và điều hướng

- Icon luôn đi kèm nhãn ở nơi nhận diện chức năng là quan trọng.
- Mục sidebar được gợi ý có badge “Tiếp theo” và một motion ngắn; mục khác tĩnh.
- Icon active/hover có cử chỉ theo chức năng: Board dịch thẻ, Backlog xếp chồng, Reports tăng cột, Notifications rung một lần.
- Không thay Material Symbols bằng emoji hoặc ký tự trang trí.

## 7. Tích hợp SylvaHero nguyên bản trong Angular

### 7.1 Nguồn bắt buộc

Sử dụng exact registered source revision `SHA-256 05f359ce157a` đã tải và kiểm tra. Các hash quan trọng đã khớp:

| Tệp | SHA-256 |
|---|---|
| `inner-green-3d.html` | `69c3694bd63f44ef9f007ebe4dac57a83e4402e0cdf6b54dd10b96dd4f05e197` |
| `three.min.js` | `8a5f7249903b54d30f79f708699d2fed2d6a1d0741a4cd41377d1f01bb5a2271` |
| `card-ecostove.jpg` | `70ce084084902bc502f00c366405b661ecdff90dee95d363b36a6e146829e433` |
| `card-ethos.jpg` | `337627390f499b3ae272cec9e2f83c817694a82f42e1aa10a7b26a2c7d679dff` |
| `lexend-latin.woff2` | `1ec8f6ee2750554b4bc59ff0b507d316a82a7ba37e0e5bebc41d3bd9b9faad46` |

Các file code đăng ký `LandingPages.tsx`, `pageTypography.ts`, `pageRecipes.ts`, `LandingPageFrame.tsx` và `threeui.css` cũng đã được đối chiếu đúng hash trong bundle.

### 7.2 Cách nhúng

- Giữ canonical HTML không sửa nội dung để bảo toàn cấu trúc, shader, responsive và hash.
- Host trang canonical bằng `iframe` same-origin trong một Angular wrapper riêng.
- Wrapper bắt sự kiện click của `.dock-item--enter` sau khi iframe load rồi mở Auth panel ở parent; canonical file không bị thay đổi.
- Wrapper dọn listener khi component bị hủy.
- Iframe có title, fallback và chính sách sandbox tối thiểu cần cho script/WebGL/same-origin.
- Auth DOM ở parent, không sửa trực tiếp form vào canonical HTML.

### 7.3 Bản quyền và tài sản

- Giữ ghi nhận nguồn và license đi kèm tài sản.
- Không tải tài sản từ CDN khi chạy; dùng asset local để build/deploy ổn định.
- Không dùng ảnh ngoài gói exact source khi chưa có quyền rõ ràng.

## 8. Kiến trúc frontend dự kiến

```text
src/app/features/auth/
  auth-page.ts
  auth-page.html
  auth-page.scss
  sylva-arrival/
    sylva-arrival.ts
    sylva-arrival.html
    sylva-arrival.scss

src/app/features/for-you/
  for-you-page.ts
  focus-recommendation.service.ts

src/app/core/attention/
  attention-orchestrator.service.ts
  recent-work.service.ts
  focus-recommendation.model.ts

public/landing-pages/
  inner-green-3d.html
  inner-green-assets/
    three.min.js
    card-ecostove.jpg
    card-ethos.jpg
    lexend-latin.woff2
    LICENSES.md
```

`AttentionOrchestratorService` bảo đảm chỉ một active nudge trên toàn shell. `RecentWorkService` lưu route/task gần nhất, không lưu dữ liệu nhạy cảm. `FocusRecommendationService` chỉ tính toán từ signals và trả về model hiển thị, không trực tiếp điều hướng hoặc chạy animation.

## 9. Responsive và khả năng tiếp cận

- Desktop từ `1280px`: Sylva toàn màn hình; Auth panel mở ở bên phải hoặc giữa tùy chiều rộng. Guided Workspace dùng bố cục bất đối xứng nhưng giữ mật độ dữ liệu.
- iPad: Auth panel thành sheet có chiều rộng đọc an toàn; phần “Hôm nay của bạn” 2 cột khi đủ chỗ, một cột khi dọc.
- Điện thoại: nút bỏ qua luôn tiếp cận được; Auth là panel toàn màn hình; thẻ hành động chính lên đầu, CTA tối thiểu 44px.
- Xoay ngang chiều cao thấp: ưu tiên form và CTA; scene có thể giảm hoặc tĩnh để tránh cắt nội dung.
- Mọi thao tác chính dùng được bằng bàn phím; focus không bị animation trì hoãn.
- `prefers-reduced-motion: reduce` tắt dịch chuyển không thiết yếu; trạng thái vẫn rõ bằng màu, chữ và icon tĩnh.
- Không flash, không parallax diện rộng trong workspace, không tự phát âm thanh.

## 10. Ngân sách hiệu năng

- WebGL chỉ tồn tại ở Sylva Arrival và bị hủy/dừng khi Auth route rời đi hoặc document bị ẩn.
- Không dùng Neo Campus canvas trong For You hay AI Governance.
- Chỉ load source Sylva ở `/auth`; chunk nghiệp vụ không mang theo scene.
- Animation workspace chỉ dùng compositor-friendly properties.
- `will-change` chỉ bật trong thời gian chuyển động và được gỡ sau đó.
- Không có vòng `requestAnimationFrame` mới trong dashboard.
- Không đọc layout lặp lại trong cùng frame; các đo đạc cần thiết được batch.
- Chấp nhận fallback tĩnh nếu WebGL context lỗi.

Mục tiêu kiểm thử:

- Không có long task do animation trong thao tác thường.
- Route và CTA vẫn phản hồi ngay cả khi scene chưa load.
- Không tăng layout shift vì nudge hoặc helper copy.
- Mobile throttling không xuất hiện animation gây giật kéo dài.

## 11. Những phần bị loại bỏ

- `NeoCampusSceneComponent` và mọi import/usage liên quan.
- Cảnh `COMMAND` trong For You.
- Cảnh AI Lab low-poly trong AI Governance.
- Pointer camera/raycast và nhiều animation loop ở các route nghiệp vụ.
- Copy hoặc card chỉ tồn tại để giải thích scene cũ.

Việc xóa chỉ thực hiện sau khi không còn consumer và được kiểm tra bằng `rg`, build, lint và test.

## 12. Chia commit triển khai

Mỗi phần hoàn chỉnh được commit riêng, không push tự động:

1. `docs: define guided living workspace redesign`
2. `feat(auth): integrate exact Sylva arrival source`
3. `feat(auth): connect accessible HUCE auth panel`
4. `feat(focus): add role-aware next-action engine`
5. `feat(for-you): build guided workspace hierarchy`
6. `feat(attention): add purposeful navigation motion`
7. `refactor(motion): remove Neo Campus scenes`
8. `test(frontend): verify guided experience and performance`

Trước mỗi commit phải kiểm tra chỉ stage đúng file của phần đó. Không đưa các file untracked hiện có của người dùng vào commit.

## 13. Tiêu chí chấp nhận

### Auth

- Canonical Sylva và mọi binary asset khớp hash bắt buộc.
- Dock, butterfly, pollen, pointer, card và liquid-metal control hoạt động như nguồn.
- Enter mở được Auth; skip hoạt động bằng chuột, cảm ứng và bàn phím.
- Login/signup/forgot/OTP vẫn đầy đủ và có fallback không WebGL.

### Guided Workspace

- Trong 5 giây đầu, người dùng nhận ra một hành động chính và lý do.
- Người dùng quay lại thấy được việc đang dở mà không phải nhớ route/task.
- Gợi ý thay đổi đúng theo role/status/priority; không vượt quyền.
- Chỉ một active nudge tại một thời điểm và nudge tự dừng.
- Tắt motion không làm mất thông tin hoặc chức năng.

### Kỹ thuật

- Build, lint và toàn bộ unit test vượt qua.
- Không có import hoặc file runtime Neo Campus còn sót.
- Không có document overflow ngoài vùng cuộn có chủ đích tại `390×844`, `844×390`, `768×1024`, `1024×768`, `1440×900`.
- Kiểm tra trình duyệt xác nhận luồng Auth, route transition, focus engine và hành vi khi giảm chuyển động.

## 14. Căn cứ UX

- Nielsen Norman Group: ưu tiên recognition thay vì buộc người dùng nhớ; hiển thị lịch sử và nội dung gần đây giúp họ tiếp tục công việc bỏ dở.
- Atlassian Design System: motion phải có một tiêu điểm, làm rõ điều gì vừa thay đổi và người dùng nên làm gì tiếp theo; motion thường ngày cần nhanh và có mục đích.
- W3C WCAG: chuyển động không thiết yếu phải có khả năng tắt và tôn trọng reduced-motion.
- web.dev: ưu tiên `transform` và `opacity` để tránh layout/paint gây giật.

Nguồn tham khảo:

- https://www.nngroup.com/articles/recognition-and-recall/
- https://www.nngroup.com/articles/ten-usability-heuristics/
- https://atlassian.design/foundations/motion
- https://atlassian.design/components/spotlight/
- https://www.w3.org/WAI/WCAG21/Understanding/animation-from-interactions
- https://web.dev/articles/animations-guide
- https://github.com/MengTo/threeui
