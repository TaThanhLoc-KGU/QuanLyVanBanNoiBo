using Microsoft.AspNetCore.Mvc;
using CongVan.Models;
using CongVan.Services;

namespace CongVan.Controllers;

public class DonViController : BaseController
{
    private readonly DbService _db;
    public DonViController(DbService db) => _db = db;

    public IActionResult Index()
    {
        // Quản lý đơn vị được tích hợp vào tab Admin
        return RedirectToAction("Index", "Admin", new { tab = "dv" });
    }

    [HttpPost]
    public async Task<IActionResult> Them(DonVi dv)
    {
        if (!CoQuyen("Admin.DonVi")) return Forbid();
        await _db.ThemDonViAsync(dv);
        TempData["Success"] = $"Đã thêm đơn vị \"{dv.TenDV}\"";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Sua(DonVi dv)
    {
        if (!CoQuyen("Admin.DonVi")) return Forbid();
        await _db.SuaDonViAsync(dv);
        TempData["Success"] = "Đã cập nhật đơn vị";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> GiaiThe(byte maDV)
    {
        if (!CoQuyen("Admin.DonVi")) return Forbid();
        await _db.GiaiTheDonViAsync(maDV);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> KhoiPhuc(byte maDV)
    {
        if (!CoQuyen("Admin.DonVi")) return Forbid();
        await _db.KhoiPhucDonViAsync(maDV);
        return Json(new { ok = true });
    }

    // ── Phân quyền nội bộ đơn vị: lãnh đạo đơn vị tự quản lý ai là "văn thư đơn vị" của MÌNH,
    // không cần xin Admin trung tâm mỗi lần đổi người. Admin (Admin.LanhDaoPhong) vẫn quản lý được
    // của MỌI đơn vị qua Views/Admin/LanhDaoPhong.cshtml — trang này chỉ mở khoanh vùng 1 đơn vị.
    private async Task<bool> LaLanhDaoDonViAsync(byte maDV)
    {
        var dv = await _db.GetDonViByIdAsync(maDV);
        return dv?.MaNV_TDV.HasValue == true && dv.MaNV_TDV.Value == MaNV;
    }

    [HttpGet]
    public async Task<IActionResult> PhanQuyenNoiBo()
    {
        if (!XemDuocTinhNangMoi) return TuChoi("Chức năng này chưa được mở cho bạn.");
        bool laAdmin = CoQuyen("Admin.LanhDaoPhong");
        if (!laAdmin && !await LaLanhDaoDonViAsync(MaDV)) return Forbid();

        var dv = await _db.GetDonViByIdAsync(MaDV);
        if (dv == null) return NotFound();

        // baoGomNghiViec:true — nếu đơn vị đang gán văn thư là người đã nghỉ việc (chưa kịp đổi),
        // trang này phải CHO THẤY rõ để lãnh đạo biết mà gán lại, không được âm thầm ẩn đi (cùng lý do
        // với Admin/LanhDaoPhong.cshtml).
        ViewBag.NhanVien = await _db.GetNhanVienAsync(MaDV, baoGomNghiViec: true);
        ViewBag.VanThuHienTai = await _db.GetVanThuDonViAsync(MaDV);
        return View(dv);
    }

    [HttpPost]
    public async Task<IActionResult> SetVanThuNoiBo(short maNV, bool add)
    {
        if (!CoQuyen("Admin.LanhDaoPhong") && !await LaLanhDaoDonViAsync(MaDV)) return Forbid();
        await _db.SetVanThuDonViAsync(MaDV, maNV, add);
        return Json(new { ok = true });
    }

    // ── Hồ sơ đơn vị: đơn vị TỰ cập nhật email nhận thông báo văn bản ────────
    // Trước đây chỉ Admin sửa được ở "Lãnh đạo phòng & Email" → mỗi lần đổi email phải nhờ quản trị.
    // Nay lãnh đạo đơn vị / văn thư đơn vị tự cập nhật email của CHÍNH đơn vị mình.
    private async Task<bool> CoTheSuaHoSoDonViAsync(byte maDV) =>
        CoQuyen("Admin.LanhDaoPhong") || await LaLanhDaoDonViAsync(maDV) || (LaVanThuDonVi && maDV == MaDV);

    [HttpGet]
    public async Task<IActionResult> HoSoDonVi()
    {
        if (!await CoTheSuaHoSoDonViAsync(MaDV)) return Forbid();
        var dv = await _db.GetDonViByIdAsync(MaDV);
        if (dv == null) return NotFound();
        ViewBag.LanhDao = dv.MaNV_TDV.HasValue ? await _db.GetNhanVienByIdAsync(dv.MaNV_TDV.Value) : null;
        ViewBag.VanThu = await _db.GetVanThuDonViAsync(MaDV);
        ViewBag.NhanVien = await _db.GetNhanVienAsync(MaDV, baoGomNghiViec: true);
        return View(dv);
    }

    [HttpPost]
    public async Task<IActionResult> LuuHoSoDonVi(string? email)
    {
        if (!await CoTheSuaHoSoDonViAsync(MaDV)) return Forbid();
        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (email != null && !System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s;,]+@[^@\s;,]+\.[^@\s;,]+$"))
        {
            TempData["Error"] = "Email không hợp lệ. Nhập 1 địa chỉ email duy nhất, VD: phongdaotao@vnkgu.edu.vn";
            return RedirectToAction("HoSoDonVi");
        }
        await _db.SetEmailDonViAsync(MaDV, email);
        TempData["Success"] = email == null
            ? "Đã xóa email nhận thông báo của đơn vị."
            : $"Đã cập nhật email nhận thông báo: {email}";
        return RedirectToAction("HoSoDonVi");
    }

    // Tra cứu văn bản đơn vị xử lý (cvden_socongvan_donvi)
    public async Task<IActionResult> TraCuuVanBan(byte? maDV, int? nam, string? tuKhoa)
    {
        nam ??= DateTime.Now.Year;

        bool xemTatCa = CoQuyen("Global.XemToanTruong");

        if (!xemTatCa)
            maDV = MaDV; // ép về đơn vị của người dùng hiện tại

        var donVi = await _db.GetDonViAsync(chiLayConHoatDong: true);
        ViewBag.DanhSachDonVi = donVi;
        ViewBag.MaDV = maDV.HasValue ? (int)maDV.Value : 0;
        ViewBag.Nam = nam;
        ViewBag.TuKhoa = tuKhoa;
        ViewBag.XemTatCa = xemTatCa;

        List<CongVanDen> danhSach = new();
        List<CongVanDi> danhSachDi = new();
        if (maDV.HasValue)
        {
            danhSach = await _db.GetCVDenTheoDonViAsync(maDV.Value, nam, tuKhoa);
            if (LaVienChucThuong) danhSach = danhSach.Where(x => x.DungChung).ToList(); // viên chức: chỉ văn bản dùng chung
            danhSachDi = await _db.GetCVDiTheoDonViAsync(maDV.Value, nam, tuKhoa);
            ViewBag.TenDV = donVi.FirstOrDefault(d => d.MaDV == maDV)?.TenDV;
        }
        ViewBag.VanBanDi = danhSachDi;

        return View(danhSach);
    }
}
