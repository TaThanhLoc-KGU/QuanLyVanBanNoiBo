using Microsoft.AspNetCore.Mvc;
using CongVan.Models;
using CongVan.Services;

namespace CongVan.Controllers;

// Sổ văn bản nội bộ riêng của 1 đơn vị (UI web nội bộ — dùng chung "cửa" dữ liệu VanBanNoiBo với
// Controllers/Api/VanBanNoiBoApiController.cs, chỉ khác đường vào: đây qua session đăng nhập, API qua
// API Key). Xem: mọi nhân viên trong đơn vị. Tạo/sửa/xóa: lãnh đạo đơn vị hoặc văn thư đơn vị (dùng
// lại đúng 2 vai trò đã có ở CongVanDen), hoặc quyền vượt phạm vi VanBanNoiBo.QuanLyTatCa.
public class VanBanNoiBoController : BaseController
{
    private readonly DbService _db;
    private readonly FileService _fileSvc;
    public VanBanNoiBoController(DbService db, FileService fileSvc) { _db = db; _fileSvc = fileSvc; }

    private async Task<bool> LaLanhDaoDonViAsync(byte maDV)
    {
        var dv = await _db.GetDonViByIdAsync(maDV);
        return dv?.MaNV_TDV.HasValue == true && dv.MaNV_TDV.Value == MaNV;
    }

    private async Task<bool> CoTheQuanLyAsync(byte maDV) =>
        CoQuyen("VanBanNoiBo.QuanLyTatCa") || await LaLanhDaoDonViAsync(maDV) || await _db.LaVanThuDonViAsync(MaNV, maDV);

    public async Task<IActionResult> Index(int? nam, string? tuKhoa, byte? maDV)
    {
        bool xemDonViKhac = CoQuyen("VanBanNoiBo.QuanLyTatCa");
        byte maDVXem = xemDonViKhac && maDV.HasValue ? maDV.Value : MaDV;
        var dv = await _db.GetDonViByIdAsync(maDVXem);

        var vm = new VanBanNoiBoFilterViewModel
        {
            Nam = nam,
            TuKhoa = tuKhoa,
            MaDV = maDVXem,
            TenDV = dv?.TenDV,
            DanhSach = await _db.GetVanBanNoiBoAsync(maDVXem, nam, tuKhoa)
        };
        ViewBag.CoTheQuanLy = await CoTheQuanLyAsync(maDVXem);
        ViewBag.XemDonViKhac = xemDonViKhac;
        ViewBag.DanhSachDonVi = xemDonViKhac ? await _db.GetDonViAsync(chiLayConHoatDong: true) : new List<DonVi>();
        return View(vm);
    }

    public async Task<IActionResult> ChiTiet(int id)
    {
        var v = await _db.GetVanBanNoiBoByIdAsync(id);
        if (v == null) return NotFound();
        // Xem: chỉ nhân viên đúng đơn vị đó (hoặc quyền vượt phạm vi) — văn bản nội bộ không công khai toàn trường.
        if (v.MaDV != MaDV && !CoQuyen("VanBanNoiBo.QuanLyTatCa")) return Forbid();

        ViewBag.CoTheQuanLy = await CoTheQuanLyAsync(v.MaDV);
        ViewBag.DanhSachFile = await _db.GetVanBanNoiBoFilesAsync(id);
        return View(v);
    }

    [HttpGet]
    public async Task<IActionResult> Nhap(int? id)
    {
        if (id.HasValue)
        {
            var v = await _db.GetVanBanNoiBoByIdAsync(id.Value);
            if (v == null) return NotFound();
            if (!await CoTheQuanLyAsync(v.MaDV)) return Forbid();
            return View(new VanBanNoiBoFormViewModel
            {
                VanBanNoiBo = v,
                DanhSachNhanVien = await _db.GetNhanVienAsync(v.MaDV),
                DanhSachFile = await _db.GetVanBanNoiBoFilesAsync(id.Value),
                DanhSachMauSo = await _db.GetVanBanNoiBoMauSoAsync(v.MaDV)
            });
        }

        if (!await CoTheQuanLyAsync(MaDV)) return Forbid();
        return View(new VanBanNoiBoFormViewModel
        {
            VanBanNoiBo = new VanBanNoiBo { MaDV = MaDV, NgayBanHanh = DateTime.Today, TrangThai = 1 },
            DanhSachNhanVien = await _db.GetNhanVienAsync(MaDV),
            DanhSachMauSo = await _db.GetVanBanNoiBoMauSoAsync(MaDV)
        });
    }

    // AJAX: gợi ý số hiệu tiếp theo khi chọn 1 "loại" đã cấu hình mẫu — chỉ gợi ý, người dùng vẫn
    // sửa tay được trước khi lưu (giống hệt cách CongVanDi/Nhap.cshtml đang dùng GetNextSoKyHieu).
    [HttpGet]
    public async Task<IActionResult> GetNextSoHieuNoiBo(string maLoai, int? nam)
    {
        if (!await CoTheQuanLyAsync(MaDV)) return Forbid();
        var (stt, soHieu) = await _db.GetNextSoHieuNoiBoAsync(MaDV, maLoai, nam ?? DateTime.Now.Year);
        return Json(new { stt, soHieu });
    }

    [HttpPost]
    public async Task<IActionResult> ThemMauSo(string maLoai, string tenLoai, string mauChuoi)
    {
        if (!await CoTheQuanLyAsync(MaDV)) return Json(new { ok = false, msg = "Không có quyền" });
        if (string.IsNullOrWhiteSpace(maLoai) || string.IsNullOrWhiteSpace(mauChuoi))
            return Json(new { ok = false, msg = "Vui lòng nhập đủ mã loại và mẫu chuỗi" });
        var id = await _db.ThemVanBanNoiBoMauSoAsync(MaDV, maLoai.Trim(), string.IsNullOrWhiteSpace(tenLoai) ? maLoai.Trim() : tenLoai.Trim(), mauChuoi.Trim());
        return Json(new { ok = true, id });
    }

    [HttpPost]
    public async Task<IActionResult> XoaMauSo(int id)
    {
        if (!await CoTheQuanLyAsync(MaDV)) return Json(new { ok = false, msg = "Không có quyền" });
        await _db.XoaVanBanNoiBoMauSoAsync(id);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> Nhap(VanBanNoiBoFormViewModel vm, IFormFile? file)
    {
        var v = vm.VanBanNoiBo;
        bool isNew = v.ID == 0;

        byte maDVGoc = v.MaDV;
        if (!isNew)
        {
            var existing = await _db.GetVanBanNoiBoByIdAsync(v.ID);
            if (existing == null) return NotFound();
            maDVGoc = existing.MaDV;
        }
        if (!await CoTheQuanLyAsync(maDVGoc)) return Forbid();
        v.MaDV = maDVGoc; // không cho đổi sang đơn vị khác qua form

        if (string.IsNullOrWhiteSpace(v.TieuDe))
        {
            TempData["Error"] = "Tiêu đề không được để trống.";
            vm.DanhSachNhanVien = await _db.GetNhanVienAsync(v.MaDV);
            vm.DanhSachMauSo = await _db.GetVanBanNoiBoMauSoAsync(v.MaDV);
            if (!isNew) vm.DanhSachFile = await _db.GetVanBanNoiBoFilesAsync(v.ID);
            return View(vm);
        }

        if (isNew)
        {
            // Nếu chọn "loại" đã cấu hình mẫu, STT tính riêng theo (đơn vị, loại) và số hiệu do
            // client điền sẵn từ gợi ý GetNextSoHieuNoiBo — vẫn đọc lại STT ở server cho chắc (tránh
            // 2 người cùng lưu 1 lúc lệch STT hiển thị, dù chưa chặn tuyệt đối race hiếm gặp).
            v.STT = string.IsNullOrEmpty(v.LoaiVanBanNoiBo)
                ? await _db.NextSttVanBanNoiBoAsync(v.MaDV, v.NgayBanHanh.Year)
                : (await _db.GetNextSoHieuNoiBoAsync(v.MaDV, v.LoaiVanBanNoiBo, v.NgayBanHanh.Year)).Stt;
            v.MaNVTao = MaNV;
            v.NguonTao = 0;
            v.ID = await _db.ThemVanBanNoiBoAsync(v);
        }
        else
        {
            await _db.SuaVanBanNoiBoAsync(v);
        }

        if (file != null && file.Length > 0)
        {
            var rel = await _fileSvc.SaveAsync(file, "vanbannoibo");
            await _db.ThemVanBanNoiBoFileAsync(v.ID, file.FileName, rel);
        }

        TempData["Success"] = "Lưu thành công!";
        return RedirectToAction("ChiTiet", new { id = v.ID });
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(int id)
    {
        var v = await _db.GetVanBanNoiBoByIdAsync(id);
        if (v == null) return NotFound();
        if (!await CoTheQuanLyAsync(v.MaDV)) return Forbid();

        foreach (var f in await _db.GetVanBanNoiBoFilesAsync(id))
            _fileSvc.Delete(f.DuongDan);
        await _db.XoaVanBanNoiBoAsync(id);

        TempData["Success"] = "Đã xóa văn bản.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> XoaFile(int fileId)
    {
        var f = await _db.GetVanBanNoiBoFileByIdAsync(fileId);
        if (f == null) return NotFound();
        var v = await _db.GetVanBanNoiBoByIdAsync(f.VanBanID);
        if (v == null) return NotFound();
        if (!await CoTheQuanLyAsync(v.MaDV)) return Forbid();

        _fileSvc.Delete(f.DuongDan);
        await _db.XoaVanBanNoiBoFileAsync(fileId);
        return RedirectToAction("ChiTiet", new { id = f.VanBanID });
    }

    public async Task<IActionResult> TaiFile(int fileId)
    {
        var f = await _db.GetVanBanNoiBoFileByIdAsync(fileId);
        if (f == null) return NotFound();
        var v = await _db.GetVanBanNoiBoByIdAsync(f.VanBanID);
        if (v == null) return NotFound();
        if (v.MaDV != MaDV && !CoQuyen("VanBanNoiBo.QuanLyTatCa")) return Forbid();
        if (!_fileSvc.Exists(f.DuongDan)) return NotFound();

        var bytes = await System.IO.File.ReadAllBytesAsync(_fileSvc.GetFullPath(f.DuongDan));
        return File(bytes, FileService.GetMimeType(f.TenFile), f.TenFile);
    }
}
