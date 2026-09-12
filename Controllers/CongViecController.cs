using Microsoft.AspNetCore.Mvc;
using CongVan.Models;
using CongVan.Services;

namespace CongVan.Controllers;

public class CongViecController : BaseController
{
    private readonly DbService _db;
    private readonly FileService _fileSvc;

    public CongViecController(DbService db, FileService fileSvc)
    {
        _db = db;
        _fileSvc = fileSvc;
    }

    private bool CoLienQuan(CongViec cv)
    {
        if (CoQuyen("CongViec.QuanLyTatCa")) return true;
        if (cv.MaNVChuTri == MaNV || cv.MaNVGiao == MaNV || cv.MaNVTao == MaNV) return true;
        var phoiHop = (cv.NguoiPhoiHop ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        return phoiHop.Contains(MaNV.ToString());
    }

    public async Task<IActionResult> Index(byte? maDV, byte? trangThai, string? tuKhoa, bool chiCuaToi = false)
    {
        var vm = new CongViecFilterViewModel
        {
            MaDV = maDV, TrangThai = trangThai, TuKhoa = tuKhoa, ChiCuaToi = chiCuaToi,
            DanhSach = await _db.GetCongViecAsync(maDV, trangThai, tuKhoa, chiCuaToi ? MaNV : null),
            DanhSachDonVi = await _db.GetDonViAsync(chiLayConHoatDong: false)
        };
        return View(vm);
    }

    // Tìm nhanh văn bản gốc (gõ số hoặc trích yếu) — dùng cho ô "Văn bản gốc" ở form công việc.
    [HttpGet]
    public async Task<IActionResult> TimVanBan(byte loai, string? q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return Json(Array.Empty<object>());
        var ds = await _db.TimVanBanGocAsync(loai, q.Trim());
        return Json(ds.Select(x => new
        {
            mscv = x.MSCV,
            soVanBan = x.SoVanBan,
            trichYeu = x.TrichYeu,
            ngay = x.Ngay.ToString("dd/MM/yyyy")
        }));
    }

    [HttpGet]
    public async Task<IActionResult> Nhap(int? id)
    {
        var vm = new CongViecFormViewModel
        {
            DanhSachNhanVien = await _db.GetAllNhanVienAsync()
        };
        if (id.HasValue)
        {
            var cv = await _db.GetCongViecByIdAsync(id.Value);
            if (cv == null) return NotFound();
            if (!CoLienQuan(cv)) return Forbid();
            vm.CongViec = cv;
            vm.DanhSachNVPhoiHop = (cv.NguoiPhoiHop ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(short.Parse).ToList();
        }
        else
        {
            vm.CongViec = new CongViec { NgayGiao = DateTime.Today, MaNVChuTri = MaNV, MaDV = MaDV, MucDoUuTien = 1 };
        }
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Nhap(CongViecFormViewModel vm, List<short> nvPhoiHop, IFormFile? fileDinhKem)
    {
        var cv = vm.CongViec;
        bool isNew = cv.MaCV == 0;

        if (!isNew)
        {
            var existing = await _db.GetCongViecByIdAsync(cv.MaCV);
            if (existing == null) return NotFound();
            if (!CoLienQuan(existing)) return Forbid();
        }

        if (string.IsNullOrWhiteSpace(cv.TieuDe) || cv.MaNVChuTri == 0 || cv.MaDV == 0)
        {
            TempData["Error"] = string.IsNullOrWhiteSpace(cv.TieuDe)
                ? "Tiêu đề không được để trống."
                : "Vui lòng chọn đầy đủ Người chủ trì và Đơn vị.";
            vm.DanhSachNhanVien = await _db.GetAllNhanVienAsync();
            vm.DanhSachNVPhoiHop = nvPhoiHop;
            return View(vm);
        }

        cv.NguoiPhoiHop = nvPhoiHop.Count > 0 ? string.Join(",", nvPhoiHop.Distinct()) : null;
        cv.MSCVGoc = string.IsNullOrWhiteSpace(cv.MSCVGoc) ? null : cv.MSCVGoc.Trim();
        if (cv.MSCVGoc == null) cv.LoaiNguonGoc = null;

        if (isNew)
        {
            cv.NgayGiao = DateTime.Today;
            cv.MaNVGiao = MaNV;
            cv.MaCV = await _db.ThemCongViecAsync(cv, MaNV);
        }
        else
        {
            await _db.SuaCongViecAsync(cv);
        }

        if (fileDinhKem != null && fileDinhKem.Length > 0)
        {
            var relPath = await _fileSvc.SaveAsync(fileDinhKem, "congviec");
            await _db.ThemFileCongViecAsync(cv.MaCV, fileDinhKem.FileName, relPath, MaNV);
        }

        TempData["Success"] = "Lưu thành công!";
        return RedirectToAction("ChiTiet", new { id = cv.MaCV });
    }

    public async Task<IActionResult> ChiTiet(int id)
    {
        var cv = await _db.GetCongViecByIdAsync(id);
        if (cv == null) return NotFound();
        if (!CoLienQuan(cv)) return Forbid();

        var phoiHopIds = (cv.NguoiPhoiHop ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(short.Parse).ToList();
        var tatCaNV = await _db.GetAllNhanVienAsync(baoGomNghiViec: true); // hiển thị đúng tên phối hợp cũ dù người đó đã nghỉ việc

        var vm = new CongViecChiTietViewModel
        {
            CongViec = cv,
            NhatKy = await _db.GetNhatKyCongViecAsync(id),
            DanhSachFile = await _db.GetFileCongViecAsync(id),
            TenNVPhoiHop = tatCaNV.Where(nv => phoiHopIds.Contains(nv.MaNV)).Select(nv => $"{nv.HoNV} {nv.TenNV}").ToList(),
            TieuDeVanBanGoc = await _db.GetTieuDeVanBanGocAsync(cv.LoaiNguonGoc, cv.MSCVGoc),
            BinhLuan = await _db.GetBinhLuanCongViecAsync(id)
        };
        return View(vm);
    }

    // Trao đổi/bình luận — mở cho người liên quan bất kể trạng thái công việc (khác Nhật ký xử lý).
    [HttpPost]
    public async Task<IActionResult> ThemBinhLuan(int maCV, string noiDung)
    {
        var cv = await _db.GetCongViecByIdAsync(maCV);
        if (cv == null) return NotFound();
        if (!CoLienQuan(cv))
        {
            TempData["Error"] = "Không có quyền bình luận công việc này";
            return RedirectToAction("ChiTiet", new { id = maCV });
        }
        if (string.IsNullOrWhiteSpace(noiDung))
        {
            TempData["Error"] = "Vui lòng nhập nội dung bình luận";
            return RedirectToAction("ChiTiet", new { id = maCV });
        }

        await _db.ThemBinhLuanCongViecAsync(maCV, MaNV, noiDung.Trim());
        return RedirectToAction("ChiTiet", new { id = maCV });
    }

    [HttpPost]
    public async Task<IActionResult> XoaBinhLuan(int id, int maCV)
    {
        var bl = await _db.GetBinhLuanByIdAsync(id);
        if (bl == null || bl.MaCV != maCV) return NotFound();
        if (!CoQuyen("CongViec.QuanLyTatCa") && bl.MaNV != MaNV)
        {
            TempData["Error"] = "Chỉ người viết bình luận (hoặc Admin/Văn thư) mới được xóa";
            return RedirectToAction("ChiTiet", new { id = maCV });
        }

        await _db.XoaBinhLuanCongViecAsync(id);
        return RedirectToAction("ChiTiet", new { id = maCV });
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(int maCV)
    {
        var cv = await _db.GetCongViecByIdAsync(maCV);
        if (cv == null) return NotFound();
        if (!CoQuyen("CongViec.QuanLyTatCa") && cv.MaNVTao != MaNV && cv.MaNVChuTri != MaNV) return Forbid();

        var files = await _db.GetFileCongViecAsync(maCV);
        foreach (var f in files) _fileSvc.Delete(f.DuongDan);
        await _db.XoaCongViecAsync(maCV);
        TempData["Success"] = "Đã xóa công việc.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> CapNhatTrangThai(int maCV, byte trangThai)
    {
        var cv = await _db.GetCongViecByIdAsync(maCV);
        if (cv == null) return Json(new { ok = false, msg = "Không tìm thấy công việc" });
        if (!CoLienQuan(cv)) return Json(new { ok = false, msg = "Không có quyền thực hiện thao tác này" });

        await _db.CapNhatTrangThaiCongViecAsync(maCV, trangThai);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> ThemNhatKy(int maCV, string noiDung, byte? trangThaiMoi, IFormFile? file)
    {
        var cv = await _db.GetCongViecByIdAsync(maCV);
        if (cv == null) return NotFound();
        if (!CoLienQuan(cv))
        {
            TempData["Error"] = "Không có quyền ghi nhật ký cho công việc này";
            return RedirectToAction("ChiTiet", new { id = maCV });
        }

        if (string.IsNullOrWhiteSpace(noiDung))
        {
            TempData["Error"] = "Vui lòng nhập nội dung nhật ký";
            return RedirectToAction("ChiTiet", new { id = maCV });
        }

        string? relPath = null;
        if (file != null && file.Length > 0)
            relPath = await _fileSvc.SaveAsync(file, "congviec");

        await _db.ThemNhatKyCongViecAsync(maCV, MaNV, noiDung, trangThaiMoi, relPath);
        if (trangThaiMoi.HasValue)
            await _db.CapNhatTrangThaiCongViecAsync(maCV, trangThaiMoi.Value);

        TempData["Success"] = "Đã ghi nhận nhật ký.";
        return RedirectToAction("ChiTiet", new { id = maCV });
    }

    // Chủ trì xin trả lại/chuyển người khác — ghi nhật ký + (nếu gắn văn bản đến) báo vào luồng
    // chỉ đạo của văn bản đó để lãnh đạo đơn vị thấy và phân công lại.
    [HttpPost]
    public async Task<IActionResult> XinTraLai(int maCV, string lyDo)
    {
        var cv = await _db.GetCongViecByIdAsync(maCV);
        if (cv == null) return NotFound();
        if (!CoQuyen("CongViec.QuanLyTatCa") && cv.MaNVChuTri != MaNV)
        {
            TempData["Error"] = "Chỉ người chủ trì mới xin trả lại được.";
            return RedirectToAction("ChiTiet", new { id = maCV });
        }
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            TempData["Error"] = "Vui lòng nhập lý do xin trả lại.";
            return RedirectToAction("ChiTiet", new { id = maCV });
        }

        await _db.ChuyenVienXinTraLaiAsync(maCV, MaNV, lyDo.Trim());
        TempData["Success"] = "Đã gửi yêu cầu trả lại/chuyển người khác.";
        return RedirectToAction("ChiTiet", new { id = maCV });
    }
}
