using Microsoft.AspNetCore.Mvc;
using CongVan.Models;
using CongVan.Services;

namespace CongVan.Controllers;

// ĐÃ GỘP vào Lịch làm việc (migration 035, yêu cầu người dùng 2026-09-06): "lịch công tác là 1
// phần của lịch làm việc". Controller được GIỮ LẠI để link/bookmark cũ không 404 — Index/Nhap
// chuyển hướng sang LichLamViec; các action còn lại vẫn chạy trên bảng LichCongTac cũ (dữ liệu
// đã được copy sang LichLamViec, bảng cũ giữ nguyên để đối chiếu).
public class LichCongTacController : BaseController
{
    private readonly DbService _db;

    public LichCongTacController(DbService db)
    {
        _db = db;
    }

    private bool CoLienQuan(LichCongTac lc) =>
        CoQuyen("LichCongTac.QuanLyTatCa") || lc.MaNVTao == MaNV ||
        (lc.NguoiThamGia ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Contains(MaNV.ToString());

    // Lưới lịch dạng tháng — ô ngày hiện các sự kiện trong ngày đó, kèm cả ngày của tháng
    // trước/sau lấp đầy tuần đầu/cuối để lưới luôn đủ 7 cột x N hàng trọn tuần.
    public IActionResult Index() => RedirectToAction("Index", "LichLamViec");

    [NonAction]
    public async Task<IActionResult> IndexCu(int? nam, int? thang, byte? maDV)
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

        var vm = new LichCongTacFilterViewModel
        {
            Nam = year,
            Thang = month,
            MaDV = maDV,
            GridStart = gridStart,
            GridEnd = gridEnd,
            DanhSach = await _db.GetLichCongTacTheoKhoangNgayAsync(gridStart, gridEnd, maDV),
            DanhSachDonVi = await _db.GetDonViAsync(chiLayConHoatDong: false)
        };
        return View(vm);
    }

    [HttpGet]
    public IActionResult Nhap() => RedirectToAction("Nhap", "LichLamViec", new { loaiLich = 2 });

    [NonAction]
    public async Task<IActionResult> NhapCu(int? id)
    {
        var vm = new LichCongTacFormViewModel
        {
            DanhSachDonVi = await _db.GetDonViAsync(chiLayConHoatDong: false),
            DanhSachNhanVien = await _db.GetAllNhanVienAsync(),
            DanhSachPhong = await _db.GetPhongHopAsync()
        };
        if (id.HasValue)
        {
            var lc = await _db.GetLichCongTacByIdAsync(id.Value);
            if (lc == null) return NotFound();
            if (!CoLienQuan(lc)) return Forbid();
            vm.LichCongTac = lc;
            vm.DanhSachNguoiThamGia = (lc.NguoiThamGia ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(short.Parse).ToList();
        }
        else
        {
            vm.LichCongTac = new LichCongTac { ThoiGianBatDau = DateTime.Today.AddHours(8), MaDV = MaDV, LoaiSuKien = 1 };
        }
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Nhap(LichCongTacFormViewModel vm, List<short> nguoiThamGia, bool toanTruong)
    {
        var lc = vm.LichCongTac;
        bool isNew = lc.MaLich == 0;

        if (!isNew)
        {
            var existing = await _db.GetLichCongTacByIdAsync(lc.MaLich);
            if (existing == null) return NotFound();
            if (!CoLienQuan(existing)) return Forbid();
        }

        // Sự kiện toàn trường (MaDV=NULL) chỉ dành cho admin/văn thư/lãnh đạo (quyền 12);
        // còn lại luôn là sự kiện của đơn vị mình, không cần chọn thủ công.
        if (toanTruong)
        {
            if (!CoQuyen("LichCongTac.ToanTruong"))
            {
                TempData["Error"] = "Chỉ Ban Giám Hiệu, Admin hoặc Văn thư mới được tạo sự kiện toàn trường.";
                vm.DanhSachDonVi = await _db.GetDonViAsync(chiLayConHoatDong: false);
                vm.DanhSachNhanVien = await _db.GetAllNhanVienAsync();
                vm.DanhSachPhong = await _db.GetPhongHopAsync();
                return View(vm);
            }
            lc.MaDV = null;
        }
        else
        {
            lc.MaDV = MaDV;
        }

        if (string.IsNullOrWhiteSpace(lc.TieuDe))
        {
            TempData["Error"] = "Tiêu đề không được để trống.";
            vm.DanhSachDonVi = await _db.GetDonViAsync(chiLayConHoatDong: false);
            vm.DanhSachNhanVien = await _db.GetAllNhanVienAsync();
            vm.DanhSachPhong = await _db.GetPhongHopAsync();
            return View(vm);
        }

        if (lc.MaPhong is > 0)
        {
            var trung = await _db.GetTrungPhongAsync(lc.MaPhong.Value, lc.ThoiGianBatDau,
                lc.ThoiGianKetThuc ?? lc.ThoiGianBatDau.AddHours(1), isNew ? null : lc.MaLich);
            if (trung.Count > 0)
            {
                TempData["Error"] = $"Phòng đã có lịch trùng: \"{trung[0].TieuDe}\" lúc {trung[0].ThoiGianBatDau:HH:mm dd/MM/yyyy} (do {trung[0].TenNVTao}). Vui lòng chọn phòng/thời gian khác.";
                vm.DanhSachDonVi = await _db.GetDonViAsync(chiLayConHoatDong: false);
                vm.DanhSachNhanVien = await _db.GetAllNhanVienAsync();
                vm.DanhSachPhong = await _db.GetPhongHopAsync();
                return View(vm);
            }
        }
        else
        {
            lc.MaPhong = null;
        }

        lc.NguoiThamGia = nguoiThamGia.Count > 0 ? string.Join(",", nguoiThamGia.Distinct()) : null;

        if (isNew)
            lc.MaLich = await _db.ThemLichCongTacAsync(lc, MaNV);
        else
            await _db.SuaLichCongTacAsync(lc);

        TempData["Success"] = "Lưu thành công!";
        return RedirectToAction("Index");
    }

    // Kiểm tra nhanh trước khi lưu (AJAX, gọi khi người dùng đổi phòng/giờ trên form Nhập)
    [HttpGet]
    public async Task<IActionResult> KiemTraPhongTrong(byte maPhong, DateTime batDau, DateTime? ketThuc, int? loaiTruMaLich)
    {
        var trung = await _db.GetTrungPhongAsync(maPhong, batDau, ketThuc ?? batDau.AddHours(1), loaiTruMaLich);
        if (trung.Count == 0) return Json(new { trong = true });
        return Json(new { trong = false, msg = $"Phòng đã có lịch: \"{trung[0].TieuDe}\" lúc {trung[0].ThoiGianBatDau:HH:mm dd/MM/yyyy} (do {trung[0].TenNVTao})" });
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(int maLich)
    {
        var lc = await _db.GetLichCongTacByIdAsync(maLich);
        if (lc == null) return NotFound();
        if (!CoQuyen("LichCongTac.QuanLyTatCa") && lc.MaNVTao != MaNV) return Forbid();

        await _db.XoaLichCongTacAsync(maLich);
        TempData["Success"] = "Đã xóa sự kiện.";
        return RedirectToAction("Index");
    }
}
