# Cẩm nang kỹ thuật chính thức

Sản phẩm: **Hệ thống Quản lý Dự án Scrum tích hợp AI**. Đây là một quyển tài liệu chuẩn tắc dùng xuyên suốt vòng đời dự án, không phải báo cáo tiến độ của một thời điểm. Người mới bắt đầu từ [bản PDF](Cam-nang-ky-thuat-He-thong-Quan-ly-Du-an-Scrum-tich-hop-AI.pdf) hoặc [bản HTML](Cam-nang-ky-thuat-He-thong-Quan-ly-Du-an-Scrum-tich-hop-AI.html); thành viên chỉnh sửa nội dung tại [nguồn Markdown](Cam-nang-ky-thuat.md).

| Tệp | Mục đích |
|---|---|
| `Cam-nang-ky-thuat.md` | Nguồn nội dung duy nhất; sửa ở đây trước. |
| `Cam-nang-ky-thuat-He-thong-Quan-ly-Du-an-Scrum-tich-hop-AI.docx` | Bản Word có mục lục và phụ lục sơ đồ in lớn. |
| `Cam-nang-ky-thuat-He-thong-Quan-ly-Du-an-Scrum-tich-hop-AI.pdf` | Bản phát hành/đọc/in. |
| `Cam-nang-ky-thuat-He-thong-Quan-ly-Du-an-Scrum-tich-hop-AI.html` | Bản web tự chứa về nội dung, dùng SVG trong `diagrams/svg/`. |
| `Cam-nang-ky-thuat-He-thong-Quan-ly-Du-an-Scrum-tich-hop-AI.txt` | Bản văn bản thuần để tìm kiếm nhanh. |
| `diagrams/Cam-nang-so-do.drawio` | 16 sơ đồ nghiệp vụ, tuần tự, hoạt động, kiến trúc và CI/CD chỉnh sửa được. |
| `diagrams/ERD-55-bang.drawio` | Ba trang ERD cho 55 bảng và 52 quan hệ FK trong DDL tham khảo; một FK chéo module được đánh dấu là ngoại lệ phải xử lý. |

Sơ đồ đơn sắc: nền trắng, nét xám/đen với độ đậm nhạt để nhấn trọng tâm; mỗi hình trong sách có tên, số hình và lời giải thích. Bản SVG/PNG là ảnh xuất để chèn tài liệu, không thay cho tệp draw.io gốc.

Khi sửa tài liệu: cập nhật Markdown, tệp sinh sơ đồ nếu có thay đổi hình, kiểm tra hai tệp draw.io, tái xuất HTML/TXT/Word/PDF và rà soát lại mục lục. Các lệnh tạo hiện được giữ tại `scripts/build-cam-nang-diagrams.cjs`, `scripts/build-cam-nang-erd.cjs`, `scripts/render-cam-nang-diagrams.cjs`, `scripts/build-cam-nang-book.cjs`. Chỉ một quyển cẩm nang này được coi là định hướng sản phẩm; các bản nháp, báo cáo cấu trúc hoặc tài liệu nguồn đầu bài chỉ là tư liệu đối chiếu.
