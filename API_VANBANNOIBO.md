# API Văn bản nội bộ đơn vị

API cho phép web/hệ thống riêng của **1 đơn vị** trong trường đọc và ghi sổ văn bản nội bộ của chính đơn vị đó, không cần đăng nhập vào CongVan. Dùng khi 1 phòng/khoa đã có web riêng và muốn đồng bộ dữ liệu qua lại thay vì nhập tay 2 nơi.

## Xác thực

Mọi request phải gửi header:

```
X-Api-Key: <key>
```

- Key được cấp bởi Admin CongVan tại **Quản trị hệ thống → API Key đơn vị**, mỗi đơn vị có thể có nhiều key (vd 1 key cho web chính, 1 key cho script nội bộ).
- **Key thật chỉ hiện đúng 1 lần** ngay lúc tạo — hệ thống chỉ lưu bản băm (SHA-256), không lưu và không thể xem lại key thật sau đó. Nếu mất key, phải thu hồi và tạo key mới.
- Thiếu header hoặc key sai/đã bị thu hồi → `401 Unauthorized`, body `{"error": "..."}`.
- **Mỗi key chỉ thấy và sửa được dữ liệu của đúng 1 đơn vị** (đơn vị được gán lúc tạo key). Không có cách nào qua API để đọc/ghi dữ liệu của đơn vị khác, kể cả khi biết ID của văn bản đó — mọi truy vấn đều tự khoanh vùng theo đơn vị của key, ID thuộc đơn vị khác sẽ trả về `404` như thể không tồn tại.

## Base URL

```
https://<domain-congvan>/api/v1/vanbannoibo
```

## Định dạng

- Request/response body: `application/json`, trừ upload file dùng `multipart/form-data`.
- Ngày giờ: ISO 8601 (`"2026-08-22"` hoặc `"2026-08-22T10:00:00"`).
- Lỗi luôn trả JSON dạng `{"error": "mô tả lỗi bằng tiếng Việt"}` kèm mã HTTP tương ứng (`400`, `401`, `404`).

---

## `GET /whoami`

Kiểm tra key còn dùng được không và đang đại diện cho đơn vị nào — dùng để test kết nối, không đụng dữ liệu thật.

**Response 200:**
```json
{ "maDV": 24, "tenDV": "Phòng Quản trị Cơ sở vật chất" }
```

---

## `GET /`

Danh sách văn bản nội bộ của đơn vị (key nào gọi thấy đơn vị đó).

**Query params (tùy chọn):**
| Tên | Kiểu | Mô tả |
|---|---|---|
| `nam` | int | Lọc theo năm ban hành |
| `tuKhoa` | string | Tìm trong số hiệu hoặc tiêu đề |

**Response 200:** mảng văn bản (không kèm danh sách file, chỉ có `soFile`):
```json
[
  {
    "id": 2, "stt": 2, "soHieu": "02/TB-P.QTCSVC", "tieuDe": "...",
    "noiDung": "...", "ngayBanHanh": "2026-08-22T00:00:00", "nguoiKy": "...",
    "trangThai": 1, "trangThaiText": "Đã ban hành",
    "ngayTao": "2026-08-22T23:28:22.457", "soFile": 0
  }
]
```

`trangThai`: `0` = Dự thảo, `1` = Đã ban hành, `2` = Thu hồi.

---

## `GET /{id}`

Chi tiết 1 văn bản, kèm danh sách file đính kèm. Trả `404` nếu không tồn tại **hoặc thuộc đơn vị khác**.

**Response 200:**
```json
{
  "id": 2, "stt": 2, "soHieu": "02/TB-P.QTCSVC", "tieuDe": "...",
  "noiDung": "...", "ngayBanHanh": "2026-08-22T00:00:00", "nguoiKy": "...",
  "trangThai": 1, "trangThaiText": "Đã ban hành", "ngayTao": "2026-08-22T23:28:22.457",
  "files": [ { "id": 1, "tenFile": "thongbao.pdf", "ngayUpload": "2026-08-22T23:29:57.42" } ]
}
```

---

## `POST /`

Tạo văn bản mới. Số thứ tự (`stt`) tự động tính (lớn nhất hiện có trong đơn vị + năm ban hành, cộng 1) — không tự truyền `stt`.

**Request body:**
```json
{
  "soHieu": "12/TB-P.QTCSVC",
  "tieuDe": "Thông báo lịch trực Tết",
  "noiDung": "Nội dung chi tiết (tùy chọn)",
  "ngayBanHanh": "2026-08-22",
  "nguoiKy": "Nguyễn Văn A",
  "trangThai": 1
}
```
Chỉ `tieuDe` và `ngayBanHanh` bắt buộc; thiếu `tieuDe` → `400`.

**Response 201**, header `Location` trỏ tới `GET /{id}`, body là văn bản vừa tạo (dạng như `GET /{id}` nhưng không có `files`, dùng dạng danh sách).

---

## `PUT /{id}`

Sửa văn bản đã có (không đổi được `stt`/đơn vị). Body giống `POST` nhưng `trangThai` bắt buộc truyền rõ. `404` nếu không thuộc đơn vị của key.

**Response 200:** văn bản sau khi sửa.

---

## `DELETE /{id}`

Xóa văn bản — xóa luôn toàn bộ file đính kèm (cả bản ghi lẫn file vật lý). Không thể hoàn tác.

**Response 204** (không có body). `404` nếu không thuộc đơn vị của key.

---

## `POST /{id}/files`

Upload 1 file đính kèm cho văn bản. `multipart/form-data`, field tên `file`. Giới hạn 50MB/file.

**Response 201:**
```json
{ "id": 5, "tenFile": "thongbao.pdf", "ngayUpload": "2026-08-22T23:29:57.42" }
```

---

## `GET /{id}/files/{fileId}`

Tải file đính kèm. Response là nội dung file thô kèm `Content-Type` đúng loại file. `404` nếu văn bản không thuộc đơn vị của key, hoặc file không thuộc đúng văn bản đó, hoặc file đã bị xóa khỏi server.

---

## `DELETE /{id}/files/{fileId}`

Xóa 1 file đính kèm (không xóa cả văn bản). **Response 204**.

---

## Ví dụ (curl)

```bash
API_KEY="..."
BASE="https://congvan.vnkgu.edu.vn/api/v1/vanbannoibo"

# Kiểm tra key
curl -s "$BASE/whoami" -H "X-Api-Key: $API_KEY"

# Danh sách năm 2026
curl -s "$BASE?nam=2026" -H "X-Api-Key: $API_KEY"

# Tạo mới
curl -s -X POST "$BASE" -H "X-Api-Key: $API_KEY" -H "Content-Type: application/json" \
  -d '{"tieuDe":"Thông báo họp","ngayBanHanh":"2026-08-22","trangThai":1}'

# Upload file
curl -s -X POST "$BASE/2/files" -H "X-Api-Key: $API_KEY" -F "file=@/duong/dan/file.pdf"
```

## Giới hạn hiện tại (chưa làm, cân nhắc nếu cần sau)

- Chưa có rate limiting (giới hạn số request/phút) — hiện dựa vào việc mỗi key chỉ cấp cho 1 web/hệ thống đơn vị tin cậy, không public rộng rãi.
- Chưa hỗ trợ webhook báo khi có văn bản mới — bên tích hợp phải tự poll (`GET /`) định kỳ nếu cần đồng bộ 2 chiều theo thời gian thực.
- Chưa có endpoint liệt kê/tạo API Key qua chính API (phải vào Admin CongVan để cấp) — đây là lựa chọn có chủ đích, tránh 1 key tự cấp thêm key khác.
