# Báo cáo hoàn thiện refactor module

Ngày cập nhật: 2026-08-31 (Asia/Ho_Chi_Minh)

## Kết quả kiến trúc

- Backend còn đúng 7 project trong `ProjectMgmt.slnx`.
- `ProjectMgmt.Solution` là ASP.NET Core Web API/composition root, không phải class library và không dùng `.Host`.
- Ba project nghiệp vụ tương ứng ba nhóm sở hữu 14, 18 và 23 bảng.
- Một `ProjectMgmt.Contracts` duy nhất cho giao tiếp liên module.
- Một `ProjectMgmt.Tests` duy nhất chứa Unit/Architecture/Integration/Fakes.
- Repository/service theo kiểu truyền thống, không CQRS/MediatR.

## Domain và persistence

- Đã ánh xạ đủ 55 bảng trong DDL thành 55 entity.
- Configuration tách theo logical module, không nằm chung một file toàn hệ thống.
- Có ba `DbContext`, dùng cùng `ConnectionStrings:ProjectMgmt` và migrations-history name riêng.
- Có repository interface/implementation cho các aggregate/query chính.
- Không có `EnsureCreated`, `EnsureDeleted`, `Migrate` hay tự chạy file SQL.

## Giao tiếp liên module

- Delivery kiểm tra user qua contract do IdentityExperience cung cấp.
- Delivery kiểm tra project/sprint/workflow và lấy issue number qua contract do Planning cung cấp.
- Planning lấy số issue theo status qua contract do Delivery cung cấp để kiểm tra WIP.
- AI feedback/dataset được trao đổi bằng DTO, không lộ EF entity.

## Database thực tế

- User Secrets đã được dùng cho cấu hình local; credential không nằm trong source/git.
- Kiểm tra kết nối chỉ đọc thành công với MySQL local.
- Database hiện có 24/55 bảng; còn thiếu 31 bảng so với DDL.
- `/health` đã có schema-compatibility check chỉ đọc và trả 503 cho tới khi DBA áp dụng đủ schema. Không có thay đổi nào được thực hiện lên database.

## Kiểm tra

- Build solution: 0 warning, 0 error.
- Test hợp nhất Release: 17/17 pass sau khi dọn toàn bộ project cũ và kiểm tra hướng phụ thuộc Clean Architecture.
- Architecture test dựng cả ba EF model, xác nhận 14 + 18 + 23 = 55 bảng và không trùng ownership.
- Live health trả 200; DB/schema readiness trả 503 đúng với schema hiện tại còn thiếu.
