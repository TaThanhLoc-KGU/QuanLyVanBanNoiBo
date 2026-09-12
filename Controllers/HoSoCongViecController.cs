using Microsoft.AspNetCore.Mvc;
using CongVan.Models;
using CongVan.Services;

namespace CongVan.Controllers;

public class HoSoCongViecController : BaseController
{
    private readonly DbService _db;
    private readonly FileService _fileSvc;

    public HoSoCongViecController(DbService db, FileService fileSvc)
    {
        _db = db;
        _fileSvc = fileSvc;
    }

    private bool CoLienQuan(HoSoCongViec hs) =>
        CoQuyen("HoSoCongViec.QuanLyTatCa") || hs.MaNVPhuTrach == MaNV || hs.MaNVTao == MaNV;

    public async Task<IActionResult> Index(byte? maDV, byte? trangThai, string? tuKhoa)
    {
        var vm = new HoSoCongViecFilterViewModel
        {
            MaDV = maDV, TrangThai = trangThai, TuKhoa = tuKhoa,
            DanhSach = await _db.GetHoSoCongViecAsync(maDV, trangThai, tuKhoa),
            DanhSachDonVi = await _db.GetDonViAsync(chiLayConHoatDong: false)
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Nhap(int? id)
    {
        var vm = new HoSoCongViecFormViewModel { DanhSachNhanVien = await _db.GetAllNhanVienAsync() };
        if (id.HasValue)
        {
            var hs = await _db.GetHoSoCongViecByIdAsync(id.Value);
            if (hs == null) return NotFound();
            if (!CoLienQuan(hs)) return Forbid();
            vm.HoSoCongViec = hs;
        }
        else
        {
            vm.HoSoCongViec = new HoSoCongViec { NgayMo = DateTime.Today, MaNVPhuTrach = MaNV, MaDVPhuTrach = MaDV };
        }
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Nhap(HoSoCongViecFormViewModel vm)
    {
        var hs = vm.HoSoCongViec;
        bool isNew = hs.MaHoSo == 0;

        if (!isNew)
        {
            var existing = await _db.GetHoSoCongViecByIdAsync(hs.MaHoSo);
            if (existing == null) return NotFound();
            if (!CoLienQuan(existing)) return Forbid();
        }

        if (string.IsNullOrWhiteSpace(hs.TieuDe) || hs.MaNVPhuTrach == 0 || hs.MaDVPhuTrach == 0)
        {
            TempData["Error"] = string.IsNullOrWhiteSpace(hs.TieuDe)
                ? "Tiêu đề không được để trống."
                : "Vui lòng chọn đầy đủ Người phụ trách và Đơn vị phụ trách.";
            vm.DanhSachNhanVien = await _db.GetAllNhanVienAsync();
            return View(vm);
        }

        if (isNew)
        {
            hs.NgayMo = DateTime.Today;
            hs.MaHoSo = await _db.ThemHoSoCongViecAsync(hs, MaNV);
        }
        else
        {
            await _db.SuaHoSoCongViecAsync(hs);
        }

        TempData["Success"] = "Lưu thành công!";
        return RedirectToAction("ChiTiet", new { id = hs.MaHoSo });
    }

    public async Task<IActionResult> ChiTiet(int id)
    {
        var hs = await _db.GetHoSoCongViecByIdAsync(id);
        if (hs == null) return NotFound();
        if (!CoLienQuan(hs)) return Forbid();

        var vm = new HoSoCongViecChiTietViewModel
        {
            HoSoCongViec = hs,
            CongViecLienQuan = await _db.GetCongViecLienQuanHoSoAsync(id),
            VanBanLienQuan = await _db.GetVanBanLienQuanHoSoAsync(id),
            DanhSachFile = await _db.GetFileHoSoCongViecAsync(id)
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(int maHoSo)
    {
        var hs = await _db.GetHoSoCongViecByIdAsync(maHoSo);
        if (hs == null) return NotFound();
        if (!CoQuyen("HoSoCongViec.QuanLyTatCa") && hs.MaNVTao != MaNV) return Forbid();

        var files = await _db.GetFileHoSoCongViecAsync(maHoSo);
        foreach (var f in files) _fileSvc.Delete(f.DuongDan);
        await _db.XoaHoSoCongViecAsync(maHoSo);
        TempData["Success"] = "Đã xóa hồ sơ.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> CapNhatTrangThai(int maHoSo, byte trangThai)
    {
        var hs = await _db.GetHoSoCongViecByIdAsync(maHoSo);
        if (hs == null) return Json(new { ok = false, msg = "Không tìm thấy hồ sơ" });
        if (!CoLienQuan(hs)) return Json(new { ok = false, msg = "Không có quyền thực hiện thao tác này" });

        await _db.CapNhatTrangThaiHoSoAsync(maHoSo, trangThai);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> GanCongViec(int maHoSo, int maCV)
    {
        var hs = await _db.GetHoSoCongViecByIdAsync(maHoSo);
        if (hs == null) return NotFound();
        if (!CoLienQuan(hs))
        {
            TempData["Error"] = "Không có quyền chỉnh sửa hồ sơ này";
            return RedirectToAction("ChiTiet", new { id = maHoSo });
        }
        var cv = await _db.GetCongViecByIdAsync(maCV);
        if (cv == null)
        {
            TempData["Error"] = "Không tìm thấy công việc với mã số đã nhập";
            return RedirectToAction("ChiTiet", new { id = maHoSo });
        }
        await _db.GanCongViecVaoHoSoAsync(maHoSo, maCV);
        TempData["Success"] = "Đã gắn công việc vào hồ sơ.";
        return RedirectToAction("ChiTiet", new { id = maHoSo });
    }

    [HttpPost]
    public async Task<IActionResult> GoCongViec(int maHoSo, int maCV)
    {
        var hs = await _db.GetHoSoCongViecByIdAsync(maHoSo);
        if (hs == null) return NotFound();
        if (!CoLienQuan(hs)) return Forbid();
        await _db.GoCongViecKhoiHoSoAsync(maHoSo, maCV);
        TempData["Success"] = "Đã gỡ công việc khỏi hồ sơ.";
        return RedirectToAction("ChiTiet", new { id = maHoSo });
    }

    [HttpPost]
    public async Task<IActionResult> GanVanBan(int maHoSo, byte loaiVanBan, string mscv)
    {
        var hs = await _db.GetHoSoCongViecByIdAsync(maHoSo);
        if (hs == null) return NotFound();
        if (!CoLienQuan(hs))
        {
            TempData["Error"] = "Không có quyền chỉnh sửa hồ sơ này";
            return RedirectToAction("ChiTiet", new { id = maHoSo });
        }
        if (string.IsNullOrWhiteSpace(mscv))
        {
            TempData["Error"] = "Vui lòng nhập mã số văn bản (MSCV)";
            return RedirectToAction("ChiTiet", new { id = maHoSo });
        }
        await _db.GanVanBanVaoHoSoAsync(maHoSo, loaiVanBan, mscv.Trim(), MaNV);
        TempData["Success"] = "Đã gắn văn bản vào hồ sơ.";
        return RedirectToAction("ChiTiet", new { id = maHoSo });
    }

    [HttpPost]
    public async Task<IActionResult> GoVanBan(int id, int maHoSo)
    {
        var hs = await _db.GetHoSoCongViecByIdAsync(maHoSo);
        if (hs == null) return NotFound();
        if (!CoLienQuan(hs)) return Forbid();
        await _db.GoVanBanKhoiHoSoAsync(id);
        TempData["Success"] = "Đã gỡ văn bản khỏi hồ sơ.";
        return RedirectToAction("ChiTiet", new { id = maHoSo });
    }

    [HttpPost]
    public async Task<IActionResult> UploadFile(int maHoSo, IFormFile? file)
    {
        var hs = await _db.GetHoSoCongViecByIdAsync(maHoSo);
        if (hs == null) return NotFound();
        if (!CoLienQuan(hs))
        {
            TempData["Error"] = "Không có quyền tải file lên hồ sơ này";
            return RedirectToAction("ChiTiet", new { id = maHoSo });
        }
        if (file != null && file.Length > 0)
        {
            var relPath = await _fileSvc.SaveAsync(file, "hosocongviec");
            await _db.ThemFileHoSoCongViecAsync(maHoSo, file.FileName, relPath, MaNV);
            TempData["Success"] = $"Đã lưu file: {file.FileName}";
        }
        return RedirectToAction("ChiTiet", new { id = maHoSo });
    }
}
