# API công khai — Văn bản đi công khai

API **chỉ đọc**, **không cần đăng nhập / không cần API Key**. Chỉ trả về các văn bản đi đã được
văn thư gắn **"dấu công khai"** trong phần mềm (nút *Gắn dấu công khai* ở trang chi tiết văn bản đi,
hoặc tick *Công khai văn bản này* khi vào sổ).

Base URL production: `http://100.110.62.27:5100/api/v1/vanban-cong-khai`

CORS mở cho mọi origin (`Access-Control-Allow-Origin: *`) — gọi trực tiếp từ JavaScript của trang
web công khai khác được. Kết quả được cache 2 phút (`Cache-Control`).

---

## 1. Danh sách — `GET /api/v1/vanban-cong-khai`

Tham số query (đều không bắt buộc):

| Tham số | Kiểu | Mặc định | Ý nghĩa |
|---|---|---|---|
| `nam` | int | — | Lọc theo năm văn bản |
| `maSCV` | int | — | Lọc theo Sổ văn bản (mã — lấy từ `/danh-muc`) |
| `maLVB` | int | — | Lọc theo Loại văn bản (mã — lấy từ `/danh-muc`) |
| `q` | string | — | Từ khóa: tìm trong trích yếu + số văn bản |
| `page` | int | 1 | Trang |
| `pageSize` | int | 20 | Số dòng/trang (tối đa 100) |

Phản hồi `200`:

```json
{
  "total": 42,
  "page": 1,
  "pageSize": 20,
  "items": [
    {
      "id": "2026080012",
      "soVanBan": "378/QĐ-ĐHKG",
      "ngayVanBan": "2026-08-25T00:00:00",
      "ngayBanHanh": "2026-08-25T00:00:00",
      "trichYeu": "Về việc phê duyệt ...",
      "loaiVanBan": "Quyết định",
      "soVanBanThuoc": "Sổ công văn",
      "nguoiKy": "Nguyễn Văn Thành",
      "donViSoanThao": "P.ĐT",
      "daKySo": true,
      "coFile": true,
      "ngayCongKhai": "2026-08-27T09:15:00"
    }
  ]
}
```

## 2. Chi tiết — `GET /api/v1/vanban-cong-khai/{id}`

`id` = mã số văn bản (`items[].id`). `404` nếu không tồn tại hoặc chưa được công khai.

```json
{
  "id": "2026080012",
  "soVanBan": "378/QĐ-ĐHKG",
  "ngayVanBan": "2026-08-25T00:00:00",
  "ngayBanHanh": "2026-08-25T00:00:00",
  "trichYeu": "Về việc phê duyệt ...",
  "loaiVanBan": "Quyết định",
  "soVanBanThuoc": "Sổ công văn",
  "nguoiKy": "Nguyễn Văn Thành",
  "donViSoanThao": "P.ĐT",
  "soLuong": 1,
  "daKySo": true,
  "loaiChungThu": "VGCA",
  "coFile": true,
  "urlTaiFile": "/api/v1/vanban-cong-khai/2026080012/tai-file",
  "ngayCongKhai": "2026-08-27T09:15:00"
}
```

## 3. Tải file đính kèm — `GET /api/v1/vanban-cong-khai/{id}/tai-file`

Trả về file (PDF/Word/…) kèm `Content-Type` đúng định dạng. `404` nếu văn bản chưa công khai hoặc
không có file.

## 4. Danh mục để dựng bộ lọc — `GET /api/v1/vanban-cong-khai/danh-muc`

```json
{
  "soVanBan":   [ { "ma": 1, "ten": "Sổ công văn" } ],
  "loaiVanBan": [ { "ma": 5, "ten": "Quyết định" }, { "ma": 8, "ten": "Công văn" } ]
}
```

---

## Không bao giờ lộ qua API này

Nơi nhận nội bộ, ghi chú, thông tin người nhập/người công khai, đường dẫn file thật trên server,
và tất nhiên mọi văn bản chưa gắn dấu công khai.

## Gỡ công khai

Văn thư bấm *Gỡ công khai* ở trang chi tiết → văn bản (và file) lập tức biến mất khỏi mọi endpoint
ở trên (chậm nhất sau 2 phút do cache).
