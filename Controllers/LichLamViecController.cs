using Microsoft.AspNetCore.Mvc;
using CongVan.Models;
using CongVan.Services;

namespace CongVan.Controllers;

// Lịch làm việc riêng của lãnh đạo (đơn vị + trường) — khác Lịch công tác (toàn trường, ai cũng
// thêm được). Chỉ người có "LichLamViec.Tao" mới tạo, và chỉ quản lý lịch của chính mình. Ở mức
// tổng quan (lưới tháng) chỉ hiện SỐ LƯỢNG lãnh đạo có lịch/ngày — không hiện tên, không hiện
// người kèm theo — bấm vào 1 ngày mới thấy chi tiết từng lãnh đạo + người kèm theo của họ.
public class LichLamViecController : BaseController
{
    private readonly DbService _db;
    public LichLamViecController(DbService db) => _db = db;

    // LoaiLich=1 (lịch lãnh đạo): cần quyền LichLamViec.Tao.
    // LoaiLich=2 (lịch công tác — gộp từ module Lịch công tác cũ): ai cũng tạo được, như trước đây.
    private bool CoTheTao => CoQuyen("LichLamViec.Tao");
    private bool CoTheTaoLoai(byte loaiLich) => loaiLich == 2 || CoTheTao;
    private bool CoLienQuan(LichLamViec ll) => Quyen.Contains(0) || ll.MaNVLanhDao == MaNV || ll.MaNVTao == MaNV;

    public async Task<IActionResult> Index(int? nam, int? thang)
    {
        var today = DateTime.Today;
        int year = nam ?? today.Year;
        int month = thang ?? today.Month;
        var firstOfMonth = new DateTime(year, month, 1);
        var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);
        int leadingDays = (int)firstOfMonth.DayOfWeek == 0 ? 6 : (int)firstOfMonth.DayOfWeek - 1;
        var gridStart = firstOfMonth.AddDays(-leadingDays);
        int trailingDays = (int)lastOfMonth.DayOfWeek == 0 ? 0 : 7 - (int)lastOfMonth.DayOfWeek;
        var gridEnd = lastOfMonth.AddDays(trailingDays);

        var vm = new LichLamViecFilterViewModel
        {
            Nam = year,
            Thang = month,
            GridStart = gridStart,
            GridEnd = gridEnd,
            DanhSach = await _db.GetLichLamViecTheoKhoangNgayAsync(gridStart, gridEnd)
        };
        // Lịch nội bộ của đơn vị người dùng cũng hiện chung trên lưới (chỉ đọc) — yêu cầu "link
        // lịch nội bộ đơn vị với lịch làm việc". Bấm vào sẽ nhảy sang module Lịch nội bộ đơn vị.
        ViewBag.LichNoiBo = await _db.GetLichNoiBoTheoKhoangNgayAsync(gridStart, gridEnd, MaDV == 0 ? null : MaDV);
        ViewBag.CoTheTao = CoTheTao;
        // baoGomNghiViec:true — modal chi tiết ngày có thể hiện tháng cũ, người kèm theo lúc đó
        // có thể đã nghỉ việc, vẫn cần hiện đúng tên thay vì rơi về "NV#123".
        ViewBag.TatCaNhanVien = await _db.GetAllNhanVienAsync(baoGomNghiViec: true);
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Nhap(int? id, byte loaiLich = 1)
    {
        bool isAdmin = Quyen.Contains(0);
        var vm = new LichLamViecFormViewModel
        {
            DanhSachNhanVien = await _db.GetAllNhanVienAsync(),
            DanhSachLanhDao = await _db.GetLanhDaoTruongVaDonViAsync(),
            DanhSachDonVi = await _db.GetDonViAsync(),
            DanhSachPhong = await _db.GetPhongHopAsync()
        };
        if (id.HasValue)
        {
            var ll = await _db.GetLichLamViecByIdAsync(id.Value);
            if (ll == null) return NotFound();
            if (!CoLienQuan(ll)) return Forbid();
            if (!CoTheTaoLoai(ll.LoaiLich)) return Forbid();
            vm.LichLamViec = ll;
            vm.DanhSachNguoiKemTheo = (ll.NguoiKemTheo ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(short.Parse).ToList();
        }
        else
        {
            if (!CoTheTaoLoai(loaiLich)) return Forbid();
            vm.LichLamViec = new LichLamViec
            {
                ThoiGianBatDau = DateTime.Today.AddHours(8), MaNVLanhDao = MaNV, LoaiSuKien = 1,
                LoaiLich = loaiLich, MaDV = loaiLich == 2 ? MaDV : null
            };
        }
        ViewBag.IsAdmin = isAdmin;
        ViewBag.CoTheTaoLichLanhDao = CoTheTao;
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Nhap(LichLamViecFormViewModel vm, List<short> nguoiKemTheo)
    {
        var ll = vm.LichLamViec;
        if (ll.LoaiLich != 2) ll.LoaiLich = 1;
        if (!CoTheTaoLoai(ll.LoaiLich)) return Forbid();
        bool isNew = ll.MaLich == 0;
        bool isAdmin = Quyen.Contains(0);

        if (!isNew)
        {
            var existing = await _db.GetLichLamViecByIdAsync(ll.MaLich);
            if (existing == null) return NotFound();
            if (!CoLienQuan(existing)) return Forbid();
        }

        if (ll.LoaiLich == 2)
        {
            // Lịch công tác: "chủ lịch" là người tạo, không có khái niệm lãnh đạo riêng.
            ll.MaNVLanhDao = isNew ? MaNV : ll.MaNVLanhDao == 0 ? MaNV : ll.MaNVLanhDao;
        }
        else
        {
            ll.MaDV = null; ll.MaPhong = null;
            // Chỉ admin mới được chọn lãnh đạo khác — người thường luôn tạo lịch của chính mình.
            if (!isAdmin) ll.MaNVLanhDao = MaNV;
            else if (ll.MaNVLanhDao == 0) ll.MaNVLanhDao = MaNV;
        }

        if (string.IsNullOrWhiteSpace(ll.TieuDe))
        {
            TempData["Error"] = "Tiêu đề không được để trống.";
            vm.DanhSachNhanVien = await _db.GetAllNhanVienAsync();
            vm.DanhSachLanhDao = await _db.GetLanhDaoTruongVaDonViAsync();
            vm.DanhSachDonVi = await _db.GetDonViAsync();
            vm.DanhSachPhong = await _db.GetPhongHopAsync();
            vm.DanhSachNguoiKemTheo = nguoiKemTheo;
            ViewBag.IsAdmin = isAdmin;
            ViewBag.CoTheTaoLichLanhDao = CoTheTao;
            return View(vm);
        }

        ll.NguoiKemTheo = nguoiKemTheo.Count > 0 ? string.Join(",", nguoiKemTheo.Distinct()) : null;

        if (isNew)
            ll.MaLich = await _db.ThemLichLamViecAsync(ll, MaNV);
        else
            await _db.SuaLichLamViecAsync(ll);

        TempData["Success"] = "Lưu thành công!";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(int maLich)
    {
        var ll = await _db.GetLichLamViecByIdAsync(maLich);
        if (ll == null) return NotFound();
        if (!CoLienQuan(ll)) return Forbid();

        await _db.XoaLichLamViecAsync(maLich);
        TempData["Success"] = "Đã xóa lịch làm việc.";
        return RedirectToAction("Index");
    }
}
