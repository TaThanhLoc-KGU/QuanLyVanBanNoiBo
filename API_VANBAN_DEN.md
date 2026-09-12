# API Công văn đến (theo đơn vị)

API cho web/hệ thống riêng của **1 đơn vị** đọc danh sách **công văn đến mà đơn vị đó được giao
xử lý** (chủ trì hoặc phối hợp). **Chỉ đọc** (GET). Không cho tạo/sửa/xóa.

Cùng cơ chế xác thực với [API_VANBANNOIBO.md](API_VANBANNOIBO.md) — header `X-Api-Key`, mỗi key gắn
với đúng 1 đơn vị, key của đơn vị A không đọc được công văn đến của đơn vị B (kể cả biết mã số →
trả `404` như thể không tồn tại).

## Base URL

```
https://qlvb.vnkgu.edu.vn/api/v1/vanban-den
```

(Server-to-server trong cùng Tailscale có thể dùng `http://100.110.62.27:5100/api/v1/vanban-den`.)

## Xác thực

```
X-Api-Key: <key>
```

Key được Admin CongVan cấp tại **Quản trị hệ thống → API Key đơn vị** (chọn đơn vị + đặt tên).
Key thật chỉ hiện 1 lần lúc tạo, hệ thống chỉ lưu SHA-256. Thiếu/sai key → `401`.

## Định dạng

- Response: `application/json`, trừ endpoint tải file trả nội dung file thô.
- Ngày giờ: ISO 8601. `null` = chưa có.
- Lỗi: `{"error": "..."}` kèm mã HTTP (`401`, `404`).

---

## `GET /whoami`

Kiểm tra key + đơn vị của key.

```json
{ "maDV": 9, "tenDV": "Phòng Đào tạo" }
```

---

## `GET /`  — danh sách

**Query (tùy chọn):**

| Tên | Kiểu | Mô tả |
|---|---|---|
| `nam` | int | Lọc theo năm công văn đến |
| `q` | string | Tìm trong trích yếu, số văn bản, tên cơ quan ban hành, **và nội dung file PDF đã trích xuất** |
| `page` | int | Trang, mặc định `1` |
| `pageSize` | int | Số dòng/trang, mặc định `20`, tối đa `100` |

**Response 200:**

```json
{
  "total": 72,
  "page": 1,
  "pageSize": 20,
  "items": [
    {
      "id": "2026070104",
      "soVanBan": "825",
      "soDen": "4754/BGDĐT-QLCL",
      "ngayDen": "2026-07-27T00:00:00",
      "ngayBanHanh": "2026-07-24T00:00:00",
      "trichYeu": "Về việc khẩn trương thực hiện số hóa dữ liệu văn bằng, chứng chỉ",
      "coQuanBanHanh": "Bộ Giáo dục và Đào tạo",
      "loaiVanBan": "Công văn",
      "nguoiKy": "Phạm Ngọc Thưởng",
      "donViXuLyChinh": "Phòng Đào tạo",
      "hanXuLy": null,
      "ngayHoanThanh": null,
      "trangThai": "moi",
      "trangThaiText": "Mới",
      "coFile": true
    }
  ]
}
```

`trangThai` (mã) / `trangThaiText` (hiển thị): `moi` / `dang_xu_ly` / `hoan_thanh` / `qua_han` ...
(giá trị tổng hợp theo tiến độ xử lý của các đơn vị được giao — dùng `trangThaiText` để hiển thị).

---

## `GET /{id}`  — chi tiết

`id` = `items[].id` (mã số văn bản). `404` nếu không thuộc đơn vị của key.

```json
{
  "id": "2026070104",
  "soVanBan": "825",
  "soDen": "4754/BGDĐT-QLCL",
  "ngayDen": "2026-07-27T00:00:00",
  "ngayBanHanh": "2026-07-24T00:00:00",
  "trichYeu": "...",
  "coQuanBanHanh": "Bộ Giáo dục và Đào tạo",
  "loaiVanBan": "Công văn",
  "nguoiKy": "Phạm Ngọc Thưởng",
  "ghiChu": null,
  "donViXuLyChinh": "Phòng Đào tạo",
  "hanXuLy": null,
  "ngayHoanThanh": null,
  "trangThai": "moi",
  "trangThaiText": "Mới",
  "xuLyCuaDonVi": {
    "vaiTro": "Chủ trì",
    "trangThai": 0,
    "trangThaiText": "Chưa tiếp nhận",
    "ngayTiepNhan": null,
    "ngayHoanThanh": null,
    "ghiChu": null
  },
  "files": [
    { "id": 3736, "tenFile": "quyet-dinh.pdf", "ngayUpload": "2026-08-03T00:05:42.903",
      "urlTaiFile": "/api/v1/vanban-den/2026070104/files/3736" }
  ]
}
```

`xuLyCuaDonVi` = tiến độ xử lý của CHÍNH đơn vị của key (`null` nếu chưa có bản ghi xử lý riêng).
`xuLyCuaDonVi.trangThai`: `0` Chưa tiếp nhận, `1` Đã tiếp nhận, `2` Đang xử lý, `3` Hoàn thành,
`4` Đã chuyển tiếp, `5` Đã trả lại.

---

## `GET /{id}/files/{fileId}`  — tải file đính kèm

Trả nội dung file kèm `Content-Type` đúng loại. `404` nếu công văn không thuộc đơn vị của key,
file không thuộc công văn đó, hoặc file đã bị xóa khỏi server.

---

## Ví dụ (curl)

```bash
API_KEY="..."
BASE="https://qlvb.vnkgu.edu.vn/api/v1/vanban-den"

curl -s "$BASE/whoami"          -H "X-Api-Key: $API_KEY"
curl -s "$BASE?nam=2026&page=1" -H "X-Api-Key: $API_KEY"
curl -s "$BASE/2026070104"      -H "X-Api-Key: $API_KEY"
curl -s "$BASE/2026070104/files/3736" -H "X-Api-Key: $API_KEY" -o file.pdf
```

## Giới hạn

- Chỉ đọc — không có endpoint tạo/sửa/xóa công văn đến qua API.
- Không rate limiting — dựa vào việc mỗi key chỉ cấp cho 1 hệ thống đơn vị tin cậy.
- Không webhook — bên tích hợp tự poll `GET /` định kỳ.
- "Được giao xử lý" = đơn vị là **chủ trì** (`MaDVXL`) **hoặc phối hợp** (`BoPhanPhoiHop`) trên công văn đó.
