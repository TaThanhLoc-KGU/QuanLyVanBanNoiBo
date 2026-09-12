using Microsoft.AspNetCore.Mvc;
using CongVan.Models;
using CongVan.Services;
using ClosedXML.Excel;

namespace CongVan.Controllers;

public class VanBanDieuHanhController : BaseController
{
    private readonly DbService _db;
    private readonly FileService _fileSvc;

    public VanBanDieuHanhController(DbService db, FileService fileSvc)
    {
        _db = db;
        _fileSvc = fileSvc;
    }

    public async Task<IActionResult> Index(int? nam, byte? maSCV, string? tuKhoa)
    {
        nam ??= DateTime.Now.Year;
        var vm = new VanBanDieuHanhFilterViewModel
        {
            Nam = nam, MaSCV = maSCV, TuKhoa = tuKhoa,
            DanhSach = await _db.GetVanBanDieuHanhAsync(nam, maSCV, tuKhoa),
            DanhSachSoCV = await _db.GetSoCVAsync()
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Nhap(string? id)
    {
        if (!CoQuyen("VanBanDieuHanh.Nhap")) return Forbid();
        var vm = new VanBanDieuHanhFormViewModel
        {
            VanBanDieuHanh = id != null
                ? (await _db.GetVanBanDieuHanhByIdAsync(id) ?? new())
                : new VanBanDieuHanh { NgayBanHanh = DateTime.Today },
            DanhSachSoCV = await _db.GetSoCVAsync(),
            DanhSachLoaiVB = await _db.GetLoaiVBAsync(chiHienThi: true),
            DanhSachLanhDao = await _db.GetLanhDaoAsync()
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Nhap(VanBanDieuHanhFormViewModel vm, IFormFile? fileDinhKem)
    {
        if (!CoQuyen("VanBanDieuHanh.Nhap")) return Forbid();
        var vb = vm.VanBanDieuHanh;

        if (string.IsNullOrWhiteSpace(vb.TrichYeu) || vb.MaSCV == 0 || vb.MaLVB == 0)
        {
            TempData["Error"] = string.IsNullOrWhiteSpace(vb.TrichYeu)
                ? "Trích yếu không được để trống."
                : "Vui lòng chọn đầy đủ Sổ văn bản và Loại văn bản.";
            vm.DanhSachSoCV = await _db.GetSoCVAsync();
            vm.DanhSachLoaiVB = await _db.GetLoaiVBAsync(chiHienThi: true);
            vm.DanhSachLanhDao = await _db.GetLanhDaoAsync();
            return View(vm);
        }

        if (fileDinhKem != null && fileDinhKem.Length > 0)
            vb.FileDinhKem = await _fileSvc.SaveAsync(fileDinhKem, "vanbandieuhanh");

        if (string.IsNullOrEmpty(vb.MSCV))
            await _db.ThemVanBanDieuHanhAsync(vb, MaNV);
        else
            await _db.SuaVanBanDieuHanhAsync(vb);

        TempData["Success"] = "Lưu thành công!";
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> ChiTiet(string id)
    {
        var vb = await _db.GetVanBanDieuHanhByIdAsync(id);
        if (vb == null) return NotFound();
        ViewBag.TrinhKyDangHoatDong = await _db.GetTrinhKyDangHoatDongAsync(3, id);
        ViewBag.NhanVien = await _db.GetNguoiDuyetTrinhKyAsync(MaDV, MaNV);
        return View(vb);
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(string mscv)
    {
        if (!CoQuyen("VanBanDieuHanh.Xoa")) return Forbid();
        var vb = await _db.GetVanBanDieuHanhByIdAsync(mscv);
        if (vb != null && !string.IsNullOrEmpty(vb.FileDinhKem)) _fileSvc.Delete(vb.FileDinhKem);
        await _db.XoaVanBanDieuHanhAsync(mscv);
        TempData["Success"] = "Đã xóa văn bản.";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> GetNextSoKyHieu(byte maSCV, short? maLVB)
    {
        var (stt, soKyHieu) = await _db.GetNextSoKyHieuDieuHanhAsync(maSCV, maLVB == 0 ? null : maLVB);
        return Json(new { stt, soKyHieu });
    }

    // ── Ký số + Trình ký nhiều cấp (xem ghi chú đầy đủ trong CongVanDiController — LoaiVanBan=3) ──
    [HttpPost]
    public async Task<IActionResult> TaiFileDaKy(string mscv, IFormFile fileDaKy, string loaiChungThu)
    {
        var active = await _db.GetTrinhKyDangHoatDongAsync(3, mscv);
        TrinhKyCap? capCuoi = null;
        if (active != null)
        {
            capCuoi = active.DanhSachCap.OrderBy(c => c.ThuTu).LastOrDefault();
            var capDangCho = active.DanhSachCap.Where(c => c.TrangThai == 0).OrderBy(c => c.ThuTu).FirstOrDefault();
            if (capDangCho == null || capCuoi == null || capDangCho.ID != capCuoi.ID)
            {
                TempData["Error"] = "Văn bản đang trình ký nhiều cấp — chưa đến lượt ký chính thức (còn cấp chưa duyệt).";
                return RedirectToAction("ChiTiet", new { id = mscv });
            }
            if (!CoQuyen("VanBanDieuHanh.KySo") && capCuoi.MaNVDuyet != MaNV)
            {
                TempData["Error"] = "Chỉ người được chỉ định ở cấp cuối mới được ký chính thức cho chuỗi trình ký này.";
                return RedirectToAction("ChiTiet", new { id = mscv });
            }
        }
        else if (!CoQuyen("VanBanDieuHanh.KySo"))
        {
            return Forbid();
        }

        if (fileDaKy == null || fileDaKy.Length == 0)
        {
            TempData["Error"] = "Vui lòng chọn file đã ký để tải lên";
            return RedirectToAction("ChiTiet", new { id = mscv });
        }
        if (string.IsNullOrWhiteSpace(loaiChungThu))
        {
            TempData["Error"] = "Vui lòng chọn loại chứng thư đã dùng để ký";
            return RedirectToAction("ChiTiet", new { id = mscv });
        }
        var relPath = await _fileSvc.SaveAsync(fileDaKy, "vanbandieuhanh");
        await _db.DanhDauDaKySoVanBanDieuHanhAsync(mscv, relPath, loaiChungThu, MaNV);
        if (active != null && capCuoi != null)
        {
            var okHoanTat = await _db.HoanTatTrinhKyAsync(active.ID, capCuoi.ID, MaNV);
            TempData["Success"] = okHoanTat
                ? "Đã lưu file đã ký số."
                : "Đã lưu file đã ký số, nhưng bước ký vừa bị người khác trả lại/từ chối cùng lúc — vui lòng kiểm tra lại trạng thái chuỗi trình ký.";
            return RedirectToAction("ChiTiet", new { id = mscv });
        }
        TempData["Success"] = "Đã lưu file đã ký số.";
        return RedirectToAction("ChiTiet", new { id = mscv });
    }

    [HttpPost]
    public async Task<IActionResult> TrinhKy(string mscv, List<short> nguoiDuyet, string? ghiChu)
    {
        if (!CoQuyen("VanBanDieuHanh.KySo")) return Forbid();
        if (nguoiDuyet == null || nguoiDuyet.Count == 0)
        {
            TempData["Error"] = "Vui lòng chọn ít nhất 1 người duyệt.";
            return RedirectToAction("ChiTiet", new { id = mscv });
        }
        var active = await _db.GetTrinhKyDangHoatDongAsync(3, mscv);
        if (active != null)
        {
            TempData["Error"] = "Văn bản đang có 1 chuỗi trình ký hoạt động — không thể mở chuỗi mới.";
            return RedirectToAction("ChiTiet", new { id = mscv });
        }
        await _db.TaoTrinhKyAsync(3, mscv, MaNV, nguoiDuyet, string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim());
        TempData["Success"] = "Đã trình ký. Chờ lần lượt các cấp duyệt.";
        return RedirectToAction("ChiTiet", new { id = mscv });
    }

    [HttpPost]
    public async Task<IActionResult> DuyetTrinhKy(string mscv, int capId, string? ghiChu)
    {
        var active = await _db.GetTrinhKyDangHoatDongAsync(3, mscv);
        var cap = active?.DanhSachCap.FirstOrDefault(c => c.ID == capId);
        if (active == null || cap == null) return NotFound();

        var capDangCho = active.DanhSachCap.Where(c => c.TrangThai == 0).OrderBy(c => c.ThuTu).FirstOrDefault();
        bool laCapCuoi = cap.ThuTu == active.DanhSachCap.Max(c => c.ThuTu);
        if (capDangCho == null || capDangCho.ID != capId || laCapCuoi)
        {
            TempData["Error"] = "Không hợp lệ — cấp này chưa đến lượt duyệt, hoặc đây là cấp cuối (ký bằng cách tải file đã ký lên).";
            return RedirectToAction("ChiTiet", new { id = mscv });
        }
        if (cap.MaNVDuyet != MaNV && !CoQuyen("VanBanDieuHanh.KySo"))
        {
            TempData["Error"] = "Chỉ người được chỉ định ở cấp này mới được duyệt.";
            return RedirectToAction("ChiTiet", new { id = mscv });
        }

        var okDuyet = await _db.DuyetCapTrinhKyAsync(capId, MaNV, string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim());
        TempData[okDuyet ? "Success" : "Error"] = okDuyet
            ? "Đã duyệt — chuyển sang cấp tiếp theo."
            : "Cấp này vừa được người khác xử lý — vui lòng tải lại trang.";
        return RedirectToAction("ChiTiet", new { id = mscv });
    }

    [HttpPost]
    public async Task<IActionResult> TuChoiTrinhKy(string mscv, int capId, string lyDo)
    {
        var active = await _db.GetTrinhKyDangHoatDongAsync(3, mscv);
        var cap = active?.DanhSachCap.FirstOrDefault(c => c.ID == capId);
        if (active == null || cap == null) return NotFound();

        var capDangCho = active.DanhSachCap.Where(c => c.TrangThai == 0).OrderBy(c => c.ThuTu).FirstOrDefault();
        if (capDangCho == null || capDangCho.ID != capId)
        {
            TempData["Error"] = "Không hợp lệ — cấp này chưa đến lượt xử lý.";
            return RedirectToAction("ChiTiet", new { id = mscv });
        }
        if (cap.MaNVDuyet != MaNV && !CoQuyen("VanBanDieuHanh.KySo"))
        {
            TempData["Error"] = "Chỉ người được chỉ định ở cấp này mới được từ chối.";
            return RedirectToAction("ChiTiet", new { id = mscv });
        }
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            TempData["Error"] = "Vui lòng nhập lý do từ chối.";
            return RedirectToAction("ChiTiet", new { id = mscv });
        }

        var okTuChoi = await _db.TuChoiCapTrinhKyAsync(capId, active.ID, MaNV, lyDo.Trim());
        TempData[okTuChoi ? "Success" : "Error"] = okTuChoi
            ? "Đã từ chối — chuỗi trình ký này bị hủy, người trình có thể trình ký lại."
            : "Cấp này vừa được người khác xử lý — vui lòng tải lại trang.";
        return RedirectToAction("ChiTiet", new { id = mscv });
    }

    // ── Nhập hàng loạt từ Excel (hoàn thiện hồ sơ cũ) ───────────────────────
    public IActionResult TaiMauExcel()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Mẫu nhập văn bản điều hành");
        string[] headers =
        {
            "Sổ văn bản", "Loại văn bản", "STT", "Số ký hiệu", "Ngày ban hành (dd/MM/yyyy)",
            "Người ký", "Trích yếu (*)", "Phạm vi áp dụng", "Ghi chú"
        };
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        ws.Row(1).Style.Font.Bold = true;
        ws.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F5FF");
        ws.Cell(2, 1).Value = "Sổ văn bản nội bộ";
        ws.Cell(2, 2).Value = "Quyết định";
        ws.Cell(2, 3).Value = 1;
        ws.Cell(2, 4).Value = "01/QĐ-VNKGU";
        ws.Cell(2, 5).Value = "02/01/2026";
        ws.Cell(2, 6).Value = "Nguyễn Văn A";
        ws.Cell(2, 7).Value = "Ví dụ: Về việc...";
        ws.Cell(2, 8).Value = "Toàn trường";
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Mau_NhapVanBanDieuHanh.xlsx");
    }

    [HttpPost]
    public async Task<IActionResult> NhapExcel(IFormFile? file)
    {
        if (!CoQuyen("VanBanDieuHanh.Excel")) return Forbid();
        var ketQua = new KetQuaNhapExcelViewModel();
        if (file == null || file.Length == 0)
        {
            ketQua.Loi.Add("Vui lòng chọn file Excel để nhập.");
            return View("KetQuaNhapExcel", ketQua);
        }

        using var stream = file.OpenReadStream();
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();

        int dong = 1;
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            dong++;
            var tenSCV   = row.Cell(1).GetString().Trim();
            var tenLVB   = row.Cell(2).GetString().Trim();
            var sttText  = row.Cell(3).GetString().Trim();
            var stt1     = row.Cell(4).GetString().Trim();
            var ngayBH   = DocNgayExcel(row.Cell(5));
            var nguoiKy  = row.Cell(6).GetString().Trim();
            var trichYeu = row.Cell(7).GetString().Trim();
            var phamVi   = row.Cell(8).GetString().Trim();
            var ghiChu   = row.Cell(9).GetString().Trim();

            if (string.IsNullOrWhiteSpace(trichYeu) && string.IsNullOrWhiteSpace(stt1) && string.IsNullOrWhiteSpace(tenSCV))
                continue;

            if (string.IsNullOrWhiteSpace(trichYeu))
            {
                ketQua.Loi.Add($"Dòng {dong}: thiếu Trích yếu, đã bỏ qua.");
                continue;
            }
            if (!ngayBH.HasValue)
            {
                ketQua.Loi.Add($"Dòng {dong}: Ngày ban hành trống hoặc sai định dạng (dd/MM/yyyy), đã bỏ qua.");
                continue;
            }

            try
            {
                var maSCV = await _db.FindOrCreateSoCVAsync(tenSCV);
                var maLVB = await _db.FindOrCreateLoaiVBAsync(tenLVB);
                var maLDKy = await _db.FindNhanVienTheoTenAsync(nguoiKy);

                var vb = new VanBanDieuHanh
                {
                    MaSCV = maSCV,
                    MaLVB = maLVB,
                    STT = int.TryParse(sttText, out var sttVal) ? sttVal : 0,
                    STT1 = string.IsNullOrWhiteSpace(stt1) ? null : stt1,
                    NgayBanHanh = ngayBH.Value,
                    MaLDKy = maLDKy,
                    TrichYeu = trichYeu,
                    PhamViApDung = string.IsNullOrWhiteSpace(phamVi) ? null : phamVi,
                    GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu
                };
                await _db.ThemVanBanDieuHanhAsync(vb, MaNV);
                ketQua.SoDongThanhCong++;
            }
            catch (Exception ex)
            {
                ketQua.Loi.Add($"Dòng {dong}: lỗi khi lưu — {ex.Message}");
            }
        }

        return View("KetQuaNhapExcel", ketQua);
    }

    private static DateTime? DocNgayExcel(ClosedXML.Excel.IXLCell cell)
    {
        if (cell.TryGetValue(out DateTime dt)) return dt;
        var s = cell.GetString().Trim();
        if (string.IsNullOrEmpty(s)) return null;
        if (DateTime.TryParseExact(s, new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" },
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed))
            return parsed;
        if (DateTime.TryParse(s, System.Globalization.CultureInfo.GetCultureInfo("vi-VN"),
                System.Globalization.DateTimeStyles.None, out var parsed2))
            return parsed2;
        return null;
    }

    public async Task<IActionResult> XuatExcel(int? nam, byte? maSCV, string? tuKhoa)
    {
        var list = await _db.GetVanBanDieuHanhAsync(nam, maSCV, tuKhoa);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sổ văn bản điều hành");
        ws.Cell(1, 1).Value = "Số văn bản"; ws.Cell(1, 2).Value = "Ngày ban hành";
        ws.Cell(1, 3).Value = "Loại văn bản"; ws.Cell(1, 4).Value = "Trích yếu";
        ws.Cell(1, 5).Value = "Người ký"; ws.Cell(1, 6).Value = "Phạm vi áp dụng";
        int row = 2;
        foreach (var vb in list)
        {
            ws.Cell(row, 1).Value = vb.STT1 ?? vb.STT.ToString();
            ws.Cell(row, 2).Value = vb.NgayBanHanh.ToString("dd/MM/yyyy");
            ws.Cell(row, 3).Value = vb.TenLVB;
            ws.Cell(row, 4).Value = vb.TrichYeu;
            ws.Cell(row, 5).Value = vb.TenLDKy;
            ws.Cell(row, 6).Value = vb.PhamViApDung;
            row++;
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"VanBanDieuHanh_{nam}.xlsx");
    }
}
