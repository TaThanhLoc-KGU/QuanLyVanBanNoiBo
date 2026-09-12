# DESIGN.md — Cổng Dịch Vụ Công Quốc Gia

Hướng thiết kế cho toàn bộ UI của CongVan (chốt 2026-08-22, thay cho bản Bootstrap-xanh mặc định trước đó).

## Thesis

Trạng thái văn bản là một **tiến trình theo bước** (Tiếp nhận → Đang xử lý → Hoàn thành), không phải một badge tĩnh. Đây là ý tưởng trung tâm, mượn từ trải nghiệm dichvucong.gov.vn mà cán bộ văn thư đã quen dùng ngoài đời (làm CCCD, nộp phạt, tra cứu hồ sơ).

## Token hệ màu (`wwwroot/css/site.css`)

| Token | Giá trị | Vai trò |
|---|---|---|
| `--primary` | `#0B4A87` | Accent duy nhất — xanh dương thể chế |
| `--primary-dark` | `#073761` | Hover/active của primary |
| `--sidebar-bg` | `#0B2C4D` | Nền sidebar |
| `--bg` | `#F3F6FA` | Nền trang, nghiêng xanh nhạt |
| `--danger` | `#C0392B` | Đỏ quốc kỳ đã tiết chế — **chỉ** dùng cho khẩn/quá hạn, không trang trí |
| `--success` | `#1A7A4C` | Hoàn thành |
| `--warning` | `#B7791F` | Chưa xem / đang chờ |
| `--radius` | `10px` | Bo góc chuẩn — vuông vắn hơn bản cũ (12px), tránh cảm giác "bubbly" |

Font: **Be Vietnam Pro** (thay Inter) — vẫn là workhorse system-stack fallback, không cần font trình diễn.

Quy tắc màu: mọi màu hex cứng trong Views đã được quét và thay bằng token tương ứng (validation-error, legend màu, nút hành động) — chỉ còn ngoại lệ hợp lý là `Views/Account/Login.cshtml` (trang độc lập, `Layout = null`, không include site.css, tự có `<style>` riêng nhưng cùng giá trị hex với token).

## Component mới: `.status-stepper`

Thanh tiến trình ngang, 3 trạng thái mỗi mốc: `done` (xanh lá), `current` (xanh dương, có halo), `overdue` (đỏ, có halo), `pending` (viền xám nhạt). Biến thể `.lg` xếp dọc (chấm trên, nhãn dưới) dùng cho trang chi tiết.

Đã áp dụng tại:
- [Views/CongVanDen/ChiTiet.cshtml](Views/CongVanDen/ChiTiet.cshtml) — Tiếp nhận → Đang xử lý (có nhãn hạn xử lý/quá hạn) → Hoàn thành
- [Views/CongVanDi/ChiTiet.cshtml](Views/CongVanDi/ChiTiet.cshtml) — Soạn thảo → Trình ký (đọc theo `TrinhKy.TrangThai`) → Ban hành

Chưa áp dụng ở dạng rút gọn cho các dòng bảng danh sách (Index/DangXuLy/...) — các trang này vẫn dùng `.badge-status` chip, đã đủ rõ ở mật độ bảng dày; đưa stepper đầy đủ vào từng dòng sẽ làm bảng quá rộng.

## Những gì KHÔNG đổi

Toàn bộ tên class hiện có (`.card`, `.btn`, `.app-table`, `.sbadge`, `.tab-bar`, `.tab-pill`, `.form-control`, `.detail-tbl`, `.timeline`, `.modal-content`, `.cal-grid`...) được giữ nguyên tên — chỉ retune qua token. Nhờ vậy toàn bộ ~30 View còn lại (Admin, CongViec, HoSoCongViec, LichCongTac, LichLamViec, ThongBao, VanBanDieuHanh, DonVi...) tự động lên giao diện mới mà không cần sửa từng file, vì chúng dùng chung đúng những class này.

## Đã kiểm tra trực tiếp trên trình duyệt (2026-08-22)

- Login: gradient/nút/dải đỏ đúng giá trị token (đọc computed style).
- Sidebar/topbar/nền trang: đúng token trên `Home/Index`.
- `CongVanDen/ChiTiet` và `CongVanDi/ChiTiet`: stepper render đúng 3 bước ở trạng thái "đã hoàn thành/đã ký" với dữ liệu dev DB thật.
- `DonVi/TraCuuVanBan`: không còn lỗi đè chữ (đã fix trước đó bằng `td-ellipsis`), không lỗi console.
- Chưa có dữ liệu dev DB nào ở trạng thái "quá hạn" hoặc "đang xử lý" để chụp trực tiếp state `current`/`overdue` của stepper — đã xác nhận đúng qua logic CSS/Razor thay vì ảnh chụp.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, and DESIGN.md.
