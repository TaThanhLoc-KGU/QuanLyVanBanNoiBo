using Microsoft.AspNetCore.Mvc;
using CongVan.Models;
using CongVan.Services;

namespace CongVan.Controllers;

// Lịch làm việc nội bộ đơn vị — chung cho cả đơn vị (khác LichLamViec cá nhân lãnh đạo, khác
// LichCongTac toàn trường). Mặc định luôn khoanh vùng vào đơn vị của người đang đăng nhập; chỉ ai có
// quyền LichNoiBoDonVi.QuanLyTatCa (vd Admin) mới chọn được đơn vị khác để xem/quản lý giúp.
public class LichNoiBoDonViController : BaseController
{
    private readonly DbService _db;
    public LichNoiBoDonViController(DbService db) => _db = db;

    private bool CoLienQuan(LichNoiBoDonVi l) => CoQuyen("LichNoiBoDonVi.QuanLyTatCa") || l.MaNVTao == MaNV;

    public async Task<IActionResult> Index(int? nam, int? thang, byte? maDV)
    {
        bool xemDonViKhac = CoQuyen("LichNoiBoDonVi.QuanLyTatCa");
        byte maDVXem = xemDonViKhac && maDV.HasValue ? maDV.Value : MaDV;

        var today = DateTime.Today;
        int year = nam ?? today.Year;
        int month = thang ?? today.Month;
        var firstOfMonth = new DateTime(year, month, 1);
        var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);
        int leadingDays = (int)firstOfMonth.DayOfWeek == 0 ? 6 : (int)firstOfMonth.DayOfWeek - 1;
        var gridStart = firstOfMonth.AddDays(-leadingDays);
        int trailingDays = (int)lastOfMonth.DayOfWeek == 0 ? 0 : 7 - (int)lastOfMonth.DayOfWeek;
        var gridEnd = lastOfMonth.AddDays(trailingDays);

        var vm = new LichNoiBoDonViFilterViewModel
        {
            Nam = year,
            Thang = month,
            MaDV = maDVXem,
            GridStart = gridStart,
            GridEnd = gridEnd,
            DanhSach = await _db.GetLichNoiBoDonViTheoKhoangNgayAsync(maDVXem, gridStart, gridEnd)
        };
        ViewBag.XemDonViKhac = xemDonViKhac;
        ViewBag.DanhSachDonVi = xemDonViKhac ? await _db.GetDonViAsync(chiLayConHoatDong: true) : new List<DonVi>();
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Nhap(int? id)
    {
        var vm = new LichNoiBoDonViFormViewModel();
        if (id.HasValue)
        {
            var l = await _db.GetLichNoiBoDonViByIdAsync(id.Value);
            if (l == null) return NotFound();
            if (!CoLienQuan(l)) return Forbid();
            vm.LichNoiBoDonVi = l;
        }
        else
        {
            vm.LichNoiBoDonVi = new LichNoiBoDonVi { MaDV = MaDV, ThoiGianBatDau = DateTime.Today.AddHours(8), LoaiSuKien = 1 };
        }
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Nhap(LichNoiBoDonViFormViewModel vm)
    {
        var l = vm.LichNoiBoDonVi;
        bool isNew = l.MaLich == 0;

        if (!isNew)
        {
            var existing = await _db.GetLichNoiBoDonViByIdAsync(l.MaLich);
            if (existing == null) return NotFound();
            if (!CoLienQuan(existing)) return Forbid();
            l.MaDV = existing.MaDV; // không cho đổi sang đơn vị khác qua form
        }
        else
        {
            l.MaDV = MaDV; // luôn là đơn vị của người tạo — lịch nội bộ không tạo hộ đơn vị khác
        }

        if (string.IsNullOrWhiteSpace(l.TieuDe))
        {
            TempData["Error"] = "Tiêu đề không được để trống.";
            return View(vm);
        }

        if (isNew)
            l.MaLich = await _db.ThemLichNoiBoDonViAsync(l, MaNV);
        else
            await _db.SuaLichNoiBoDonViAsync(l);

        TempData["Success"] = "Lưu thành công!";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(int maLich)
    {
        var l = await _db.GetLichNoiBoDonViByIdAsync(maLich);
        if (l == null) return NotFound();
        if (!CoLienQuan(l)) return Forbid();

        await _db.XoaLichNoiBoDonViAsync(maLich);
        TempData["Success"] = "Đã xóa sự kiện.";
        return RedirectToAction("Index");
    }
}
