using Microsoft.AspNetCore.Mvc;
using CongVan.Services;

namespace CongVan.Controllers;

// Trang CÔNG KHAI (/cong-khai) — không cần đăng nhập, xem được từ bất kỳ đâu kể cả internet.
// KHÔNG kế thừa BaseController (BaseController.OnActionExecuting ép redirect sang Login nếu chưa
// đăng nhập) — controller này CỐ Ý đứng ngoài, và view của nó tự vẽ trang riêng (Layout=null),
// không dùng chung _Layout.cshtml (menu/sidebar nội bộ không phù hợp cho khách vãng lai).
//
// Phạm vi hiển thị — QUYẾT ĐỊNH CỦA NGƯỜI QUẢN TRỊ 2026-09-13 sau khi đã cảnh báo rõ: hiện TOÀN BỘ
// lịch làm việc kể cả lịch cá nhân của lãnh đạo (không giới hạn chỉ lịch công tác chung). Nếu sau
// này muốn thu hẹp lại, đổi GetLichLamViecTheoKhoangNgayAsync (lấy hết) sang lọc thêm
// "WHERE LoaiLich=2" (chỉ lịch công tác, ẩn lịch cá nhân lãnh đạo) ngay trong DbService.
public class PublicController : Controller
{
    private readonly DbService _db;

    public PublicController(DbService db) => _db = db;

    public async Task<IActionResult> Index()
    {
        // Chỉ mở khi Admin đã bật "công bố tính năng mới" (Admin → cấu hình tính năng mới) — trước đó
        // trang này lộ dữ liệu của tính năng mà nội bộ còn đang chặn cả với người dùng đăng nhập.
        ViewBag.DaMo = await _db.GetTinhNangMoiCongKhaiAsync();
        if (ViewBag.DaMo != true) return View();

        var tuNgay = DateTime.Today.AddDays(-3);
        var denNgay = DateTime.Today.AddDays(45);

        ViewBag.DanhSachLich = await _db.GetLichLamViecTheoKhoangNgayAsync(tuNgay, denNgay);
        ViewBag.ThongBao = await _db.GetThongBaoCongKhaiAsync();

        // Link "Thêm vào Google Calendar" — dùng đúng URL Google hỗ trợ sẵn để đăng ký (subscribe)
        // 1 lịch ngoài qua tham số cid=webcal://... — bấm vào trên điện thoại thường mở thẳng app
        // Google Calendar hoặc trang xác nhận thêm lịch, không cần thao tác thủ công như bản
        // trong app (dành cho người đã đăng nhập, xem Lịch làm việc → Liên kết Google Calendar).
        var host = Request.Host.Value;
        var icsPath = "/calendar/cong-khai.ics";
        ViewBag.LinkIcs = $"{Request.Scheme}://{host}{icsPath}";
        var webcalUrl = $"webcal://{host}{icsPath}";
        ViewBag.LinkGoogleCalendar = $"https://calendar.google.com/calendar/render?cid={Uri.EscapeDataString(webcalUrl)}";

        return View();
    }
}
