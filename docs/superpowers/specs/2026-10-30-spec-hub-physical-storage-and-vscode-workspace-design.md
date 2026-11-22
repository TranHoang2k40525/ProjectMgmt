# TÀI LIỆU THIẾT KẾ ĐẶC TẢ: KHO LƯU TRỮ VẬT LÝ SPEC HUB & GIAO DIỆN VS CODE WORKSPACE

## 1. TỔNG QUAN & MỤC TIÊU HỆ THỐNG
Tài liệu này đặc tả kiến trúc kỹ thuật cho Giai đoạn 1 của hệ thống Quản lý Dự án Scrum kết hợp Trí tuệ Nhân tạo:
- Xây dựng kho lưu trữ tài liệu đặc tả nghiệp vụ vật lý trên đĩa cứng máy chủ (`Spec_Hub`), phân cấp theo từng tổ chức và từng dự án.
- Thiết lập cơ chế đồng bộ 2 chiều (Bidirectional Sync) thời gian thực giữa giao diện người dùng và hệ thống tệp tin vật lý của hệ điều hành.
- Cải tiến giao diện Spec Hub thành không gian làm việc chuyên nghiệp chuẩn VS Code:
  - Cột trái: Cây thư mục (Explorer Tree) hỗ trợ tạo thư mục, tạo tệp văn bản (.txt), tải lên (.txt, .pdf, .docx, .xlsx, .md), đổi tên, xóa và duyệt cây thư mục đa cấp.
  - Cột phải: Khung làm việc mở nhiều tệp đồng thời (Multi-tab Workspace) hỗ trợ xem/sửa tệp văn bản, nhúng trình đọc PDF, trích xuất văn bản Word và bảng tính Excel, cùng tính năng bóc tách User Story trực tiếp sang Backlog.
- Phân quyền nghiêm ngặt theo mô hình Scrum phân cấp (Hierarchical Scrum RBAC), chống leo thang đặc quyền (Anti-Privilege Escalation).

---

## 2. KIẾN TRÚC LƯU TRỮ VẬT LÝ TRÊN ĐĨA CỨNG (SPEC_HUB ENGINE)

### 2.1. Cấu trúc cây thư mục vật lý
Kho dữ liệu được lưu trữ tại thư mục `Spec_Hub` nằm ngang cấp với thư mục `Assets` trong thư mục gốc của giải pháp Backend (`ProjectMgmt.Solution`):

```text
ProjectMgmt.Solution/
├── Assets/
│   └── avatars/
└── Spec_Hub/
    └── {Tên_Tổ_Chức}/
        └── {Tên_Dự_Án}_thuộc_{Tên_Tổ_Chức}/
            ├── Kien_truc_he_thong/
            │   ├── architecture_v1.pdf
            │   └── database_diagram.png
            ├── Yeu_cau_nghiep_vu/
            │   ├── dac_ta_srs.docx
            │   ├── bang_chuc_nang.xlsx
            │   └── ghi_chu_cuoc_hop.txt
            └── README.txt
```

### 2.2. Quy tắc chuẩn hóa đường dẫn (Path Sanitization)
- Tên thư mục tổ chức và tên thư mục dự án được chuẩn hóa bằng cách loại bỏ các ký tự không hợp lệ của hệ điều hành (`\ / : * ? " < > |`).
- Thư mục dự án tuân thủ cấu trúc định danh bắt buộc: `{Tên_Dự_Án}_thuộc_{Tên_Tổ_Chức}`.
- Ngăn chặn triệt để tấn công vượt quyền đường dẫn (Path Traversal Prevention): Mọi thao tác truy cập tệp đều được kiểm tra đường dẫn tuyệt đối (`Path.GetFullPath`) bắt buộc phải bắt đầu bằng đường dẫn gốc của dự án tương ứng.

---

## 3. MÔ HÌNH DỮ LIỆU & BẢNG DATABASE HYBRID

### 3.1. Thực thể ProjectDocument mở rộng
Cập nhật bảng `ProjectDocument` trong cơ sở dữ liệu để quản lý cấu trúc cây phân cấp và siêu dữ liệu tệp:

| Tên trường | Kiểu dữ liệu | Ràng buộc | Ý nghĩa |
| :--- | :--- | :--- | :--- |
| `Id` | CHAR(36) | PK | Khóa chính GUID |
| `ProjectId` | CHAR(36) | FK, Not Null | Dự án trực thuộc |
| `ParentId` | CHAR(36) | Nullable | Khóa ngoại trỏ đến thư mục cha (NULL nếu ở gốc) |
| `Name` | VARCHAR(255) | Not Null | Tên tệp hoặc thư mục (kèm phần mở rộng) |
| `RelativePath`| VARCHAR(1000)| Not Null | Đường dẫn tương đối từ gốc dự án |
| `IsDirectory` | TINYINT(1) | Not Null | 1: Thư mục, 0: Tệp tin |
| `Extension` | VARCHAR(20) | Nullable | Phần mở rộng (.txt, .pdf, .docx, .xlsx, .md) |
| `FileSize` | BIGINT | Not Null, Default 0 | Kích thước tệp (bytes) |
| `Status` | VARCHAR(20) | Not Null, Default 'Draft' | Trạng thái kiểm duyệt: Draft / Review / Approved |
| `Version` | INT | Not Null, Default 1 | Phiên bản sửa đổi |
| `CreatedByUserId` | CHAR(36) | Not Null | Người tạo |
| `ApprovedByUserId`| CHAR(36) | Nullable | Người duyệt (PO / PM / Lead) |
| `CreatedAt` | DATETIME(6) | Not Null | Thời điểm tạo |
| `UpdatedAt` | DATETIME(6) | Nullable | Thời điểm cập nhật cuối cùng |

---

## 4. DANH MỤC API BACKEND (SPEC HUB ENDPOINTS)

Toàn bộ API được chuẩn hóa theo tiền tố `api/...` (loại bỏ tiền tố `/v1` theo yêu cầu):

1. `GET api/projects/{projectId}/documents/tree`
   - Trả về toàn bộ cây thư mục và tệp tin của dự án theo dạng lồng nhau (Hierarchical Tree DTO).
2. `POST api/projects/{projectId}/documents/folders`
   - Body: `{ parentId?: string, name: string }`
   - Tạo thư mục vật lý trên đĩa cứng và thêm bản ghi `IsDirectory = true` vào cơ sở dữ liệu.
3. `POST api/projects/{projectId}/documents/files/text`
   - Body: `{ parentId?: string, name: string, content: string }`
   - Tạo tệp tin văn bản `.txt` hoặc `.md` trên đĩa cứng và lưu metadata.
4. `POST api/projects/{projectId}/documents/files/upload`
   - Multipart Form Data: `file`, `parentId?`
   - Hỗ trợ các định dạng: `.txt`, `.pdf`, `.docx`, `.xlsx`, `.md`. Lưu file vào đĩa cứng và ghi nhận metadata.
5. `GET api/documents/{id}/content`
   - Trả về nội dung tệp:
     - Đối với `.txt`, `.md`: Trả về chuỗi văn bản UTF-8.
     - Đối với `.docx`: Backend trích xuất cấu trúc văn bản/HTML hoặc trả về nội dung text.
     - Đối với `.xlsx`: Backend trích xuất các sheet thành dữ liệu JSON dạng ma trận bảng (Grid Data).
6. `GET api/documents/{id}/download`
   - Trả về FileStream tải xuống tệp tin gốc với header `Content-Disposition: attachment`.
7. `GET api/documents/{id}/raw`
   - Trả về FileStream để xem trực tiếp (inline) trên trình duyệt (dùng cho iframe PDF).
8. `PUT api/documents/{id}/content`
   - Body: `{ content: string }`
   - Cập nhật nội dung tệp `.txt` hoặc `.md` vật lý trên đĩa và tăng số phiên bản `Version`.
9. `PUT api/documents/{id}/rename`
   - Body: `{ newName: string }`
   - Đổi tên tệp/thư mục trên đĩa và cập nhật `RelativePath` của đối tượng cùng các con bên trong.
10. `DELETE api/documents/{id}`
    - Xóa tệp hoặc thư mục đệ quy trên đĩa và xóa dữ liệu tương ứng trong cơ sở dữ liệu.
11. `PUT api/documents/{id}/status`
    - Body: `{ status: 'Draft' | 'Review' | 'Approved' }`
    - Cập nhật trạng thái duyệt tài liệu (chỉ dành cho PO / PM / Lead).

---

## 5. THIẾT KẾ GIAO DIỆN NGƯỜI DÙNG (FRONTEND ANGULAR)

### 5.1. Cột trái: Cây thư mục (VS Code Explorer)
- Khung điều khiển trên đỉnh:
  - Tên dự án hiện tại.
  - Các nút hành động bằng icon Material Symbols Outlined:
    - `create_new_folder`: Mở modal hoặc dòng nhập tên thư mục mới.
    - `note_add`: Tạo nhanh tệp văn bản `.txt`.
    - `upload_file`: Mở hộp thoại chọn tệp tải lên máy tính.
    - `unfold_less`: Thu gọn các nhánh thư mục.
    - `refresh`: Tải lại cây dữ liệu từ máy chủ.
- Hiển thị danh sách nút cây:
  - Hỗ trợ mở/đóng đệ quy các cấp thư mục với animation mượt mà.
  - Biểu tượng trực quan qua Material Symbols:
    - Thư mục: `folder` / `folder_open`
    - Văn bản: `description` (.txt, .md)
    - PDF: `picture_as_pdf` (.pdf)
    - Word: `article` (.docx)
    - Excel: `table_chart` (.xlsx)
  - Menu ngữ cảnh nhanh (Hover Action Menu) trên từng dòng:
    - Đổi tên (`edit`), Tải xuống (`download`), Xóa (`delete`).

### 5.2. Cột phải: Không gian mở nhiều Tab (VS Code Multi-tab Viewer)
- Thanh Tab Bar:
  - Hiển thị danh sách các tệp đang mở song song.
  - Mỗi tab chứa: Icon định dạng, Tên tệp, nút đóng tab (`close`).
  - Tab đang chọn có đường viền chỉ báo nổi bật (Active Tab Highlight).
  - Hỗ trợ đóng tab nhanh, tự động chuyển tiêu điểm sang tab liền kề.
- Khung nội dung tài liệu theo định dạng:
  - **Tệp `.txt` và `.md`**:
    - Trình soạn thảo văn bản hỗ trợ chỉnh sửa trực tiếp (đối với role có quyền).
    - Nút "Lưu thay đổi" (`save`) và Nút "Bóc tách User Story" (`bolt`).
  - **Tệp `.pdf`**:
    - Nhúng khung xem PDF trực tiếp qua `iframe` với đường dẫn an toàn từ Backend.
    - Hỗ trợ cuộn trang, phóng to, thu nhỏ, in ấn.
  - **Tệp `.docx` (Word)**:
    - Trình xem văn bản Word tự động kết xuất tiêu đề, đoạn văn, danh sách đầu dòng và bảng biểu.
  - **Tệp `.xlsx` (Excel)**:
    - Bảng tính tương tác lưới (Spreadsheet Grid) với các tab Sheet bên dưới để chuyển đổi qua lại.
  - Thanh công cụ phía trên mỗi tab luôn có nút "Tải xuống bản gốc" (`download`).

---

## 6. MA TRẬN PHÂN QUYỀN SCRUM (HIERARCHICAL RBAC MATRIX)

Tuân thủ nghiêm ngặt quy tắc phân quyền từ trên xuống:

| Hành động | Chủ tổ chức (Org Owner) | Giám đốc dự án / PO | Scrum Master / PM | Kỹ sư (Dev) & QA | Người xem (Viewer) |
| :--- | :---: | :---: | :---: | :---: | :---: |
| Xem nội dung cây & Mở tabs | Cho phép | Cho phép | Cho phép | Cho phép | Cho phép |
| Tải xuống tệp gốc (Download) | Cho phép | Cho phép | Cho phép | Cho phép | Cho phép |
| Bình luận phản biện (Comment) | Cho phép | Cho phép | Cho phép | Cho phép | Chặn |
| Tạo thư mục mới | Cho phép | Cho phép | Cho phép | Chặn | Chặn |
| Tạo tệp mới (.txt) | Cho phép | Cho phép | Cho phép | Chặn | Chặn |
| Tải tệp lên (.pdf, .docx, .xlsx...) | Cho phép | Cho phép | Cho phép | Chặn | Chặn |
| Sửa nội dung văn bản (.txt, .md) | Cho phép | Cho phép | Cho phép | Chặn | Chặn |
| Đổi tên / Xóa tệp & thư mục | Cho phép | Cho phép | Cho phép | Chặn | Chặn |
| Phê duyệt tài liệu (Approved) | Cho phép | Cho phép | Chặn | Chặn | Chặn |

---

## 7. KẾ HOẠCH KIỂM THỬ & XÁC THỰC (VERIFICATION PLAN)
1. **Kiểm tra đồng bộ tệp vật lý**:
   - Tạo thư mục `Tài_liệu_kiến_trúc` trên UI -> Kiểm tra trên đĩa cứng thư mục đã sinh ra đúng đường dẫn `Spec_Hub/{Org}/{Project}_thuộc_{Org}/Tài_liệu_kiến_trúc`.
   - Tạo tệp `spec.txt`, tải lên tệp `spec.pdf`, `spec.docx`, `spec.xlsx` -> Xác minh tệp có mặt trên đĩa cứng với nội dung và dung lượng chính xác.
   - Sửa tệp `spec.txt` -> Xác minh file trên đĩa được ghi đè nội dung mới.
   - Xóa tệp và thư mục -> Xác minh file trên đĩa cứng bị xóa tương ứng.
2. **Kiểm tra Multi-tab UI**:
   - Mở đồng thời 3 tệp (`.txt`, `.pdf`, `.docx`) -> Các tab xuất hiện đúng thứ tự, chuyển đổi qua lại mượt mà, đóng tab chuẩn xác.
3. **Kiểm tra phân quyền**:
   - Đăng nhập tài khoản Dev -> Các nút Tạo thư mục, Tạo tệp, Tải lên, Đổi tên, Xóa bị ẩn hoàn toàn; chỉ có thể đọc và tải xuống.
