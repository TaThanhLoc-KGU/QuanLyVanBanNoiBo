using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using CongVan.Models;
using CongVan.Services;
using ClosedXML.Excel;

namespace CongVan.Controllers;

public class CongVanDenController : BaseController
{
    private readonly DbService _db;
    private readonly FileService _fileSvc;
    private readonly EmailService _email;
    private readonly PdfTextService _pdfText;
    private readonly IServiceScopeFactory _scopeFactory;

    public CongVanDenController(DbService db, FileService fileSvc, EmailService email, PdfTextService pdfText, IServiceScopeFactory scopeFactory)
    {
        _db = db;
        _fileSvc = fileSvc;
        _email = email;
        _pdfText = pdfText;
        _scopeFactory = scopeFactory;
    }

    // Trích xuất chữ từ 1 file PDF ở NỀN (không await từ action gọi nó) — OCR có thể mất vài giây
    // đến vài chục giây/file, không được chặn phản hồi upload. Tự tạo scope DI riêng vì scope của
    // request gốc (và DbService/PdfTextService lấy từ đó) có thể đã bị dispose khi Task này chạy tới.
    private void TrichXuatONen(int fileId, string fullPath)
    {
        if (!_pdfText.LaFilePdf(fullPath)) return;
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbService>();
            var pdfSvc = scope.ServiceProvider.GetRequiredService<PdfTextService>();
            try
            {
                var text = await pdfSvc.ExtractTextAsync(fullPath);
                await db.CapNhatTrichXuatFileAsync(fileId, text, 1);
            }
            catch
            {
                await db.CapNhatTrichXuatFileAsync(fileId, null, 2);
            }
        });
    }

    // Tra cứu sổ công văn đến
    public async Task<IActionResult> Index(int? nam, byte? maSCV, string? tuKhoa,
        byte? maDVXL, string? nguoiKy, DateTime? tuNgay, DateTime? denNgay, int page = 1)
    {
        nam ??= DateTime.Now.Year;
        byte? filterMaDV = CoQuyen("Global.XemToanTruong") ? null : (byte?)MaDV;
        var pageSize = 30;
        var (items, total) = await _db.GetCongVanDenPagedAsync(
            nam, maSCV, tuKhoa, filterMaDV, MaNV, maDVXL, nguoiKy, tuNgay, denNgay, page, pageSize);
        var vm = new CongVanDenFilterViewModel
        {
            Nam = nam, MaSCV = maSCV, TuKhoa = tuKhoa, MaDVXL = maDVXL, NguoiKy = nguoiKy, TuNgay = tuNgay, DenNgay = denNgay,
            DanhSach = items,
            Trang = new PageInfo { Page = page, PageSize = pageSize, TotalCount = total },
            DanhSachSoCV = await _db.GetSoCVAsync(),
            // chiLayConHoatDong:false — danh sách này còn dùng để hiện tên "Đơn vị đồng xử lý" của
            // văn bản CŨ (có thể phối hợp bởi 1 đơn vị đã giải thể); ẩn đơn vị giải thể ở đây sẽ làm
            // tên đơn vị đó biến mất khỏi cột hiển thị dù dữ liệu vẫn còn — chỉ ẩn ở dropdown lọc.
            DanhSachDonVi = await _db.GetDonViAsync(chiLayConHoatDong: false)
        };
        return View(vm);
    }

    // Nhập công văn đến
    [HttpGet]
    public async Task<IActionResult> Nhap(string? id)
    {
        if (!CoQuyen("CongVanDen.Nhap")) return Forbid();
        var danhSachSoCV = await _db.GetSoCVAsync();
        var vm = new CongVanDenFormViewModel
        {
            CongVanDen = id != null
                ? (await _db.GetCongVanDenByIdAsync(id) ?? new())
                : new CongVanDen { NgayDen = DateTime.Today, NgayBanHanh = DateTime.Today, MaSCV = danhSachSoCV.FirstOrDefault()?.MaSCV ?? 0 },
            DanhSachSoCV = danhSachSoCV,
            DanhSachCoQuan = await _db.GetCoQuanAsync(chiHienThi: true),
            DanhSachLoaiVB = await _db.GetLoaiVBAsync(chiHienThi: true),
            DanhSachNhomCV = await _db.GetNhomCVAsync(),
            DanhSachDonVi = await _db.GetDonViAsync(),
            DanhSachLanhDao = await _db.GetLanhDaoAsync()
        };
        if (id != null)
        {
            if (!string.IsNullOrEmpty(vm.CongVanDen.BoPhanPhoiHop))
                vm.DanhSachDVPhoiHop = vm.CongVanDen.BoPhanPhoiHop.Split(',')
                    .Where(x => byte.TryParse(x.Trim(), out _))
                    .Select(x => byte.Parse(x.Trim())).ToList();
            vm.DanhSachFileDinhKem = await _db.GetFileDinhKemAsync(id);
        }
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Nhap(CongVanDenFormViewModel vm, List<IFormFile> fileDinhKem, List<byte> dvPhoiHop)
    {
        if (!CoQuyen("CongVanDen.Nhap")) return Forbid();
        var cv = vm.CongVanDen;
        cv.BoPhanPhoiHop = dvPhoiHop.Count > 0 ? string.Join(",", dvPhoiHop) : null;

        if (string.IsNullOrWhiteSpace(cv.TrichYeu))
        {
            TempData["Error"] = "Trích yếu không được để trống.";
            vm.DanhSachSoCV = await _db.GetSoCVAsync();
            vm.DanhSachCoQuan = await _db.GetCoQuanAsync(chiHienThi: true);
            vm.DanhSachLoaiVB = await _db.GetLoaiVBAsync(chiHienThi: true);
            vm.DanhSachNhomCV = await _db.GetNhomCVAsync();
            vm.DanhSachDonVi = await _db.GetDonViAsync();
            vm.DanhSachLanhDao = await _db.GetLanhDaoAsync();
            vm.DanhSachDVPhoiHop = dvPhoiHop;
            if (!string.IsNullOrEmpty(cv.MSCV)) vm.DanhSachFileDinhKem = await _db.GetFileDinhKemAsync(cv.MSCV);
            return View(vm);
        }

        bool isNew = string.IsNullOrEmpty(cv.MSCV);
        if (isNew)
            cv.MSCV = await _db.ThemCongVanDenAsync(cv, MaNV);
        else
            await _db.SuaCongVanDenAsync(cv);

        // Tạo/cập nhật bản ghi theo dõi đơn vị
        await _db.TaoXuLyDVAsync(cv.MSCV, cv.MaDVXL, cv.BoPhanPhoiHop);

        // Lưu nhiều file đính kèm, thu thập path để gửi kèm email
        var savedFiles = new List<(string TenFile, string FullPath)>();
        foreach (var file in fileDinhKem.Where(f => f.Length > 0))
        {
            var relPath = await _fileSvc.SaveAsync(file, "congvanden");
            var fileId = await _db.ThemFileDinhKemAsync(cv.MSCV, file.FileName, relPath);
            var fullPath = _fileSvc.GetFullPath(relPath);
            TrichXuatONen(fileId, fullPath);
            savedFiles.Add((file.FileName, fullPath));
        }

        TempData["Success"] = "Lưu thành công!";
        return RedirectToAction("Index");
    }

    // Gửi email thông báo cho đơn vị xử lý — trước đây tự động khi lưu, nay văn thư/đơn vị
    // bấm nút mới gửi (yêu cầu người dùng chủ động kiểm soát khi nào thông báo được gửi đi).
    [HttpPost]
    public async Task<IActionResult> GuiEmail(string mscv)
    {
        var cv = await _db.GetCongVanDenByIdAsync(mscv);
        if (cv == null) return Json(new { ok = false, msg = "Không tìm thấy văn bản" });

        if (!CoQuyen("CongVanDen.GuiEmail") && !(cv.MaDVXL.HasValue && await LaLanhDaoDonViAsync(cv.MaDVXL.Value)) && !(cv.MaDVXL.HasValue && await LaVanThuDonViAsync(cv.MaDVXL.Value)))
            return Json(new { ok = false, msg = "Không có quyền gửi email cho văn bản này" });

        if (!cv.MaDVXL.HasValue)
            return Json(new { ok = false, msg = "Văn bản chưa gán đơn vị xử lý" });

        var (tenDV, emailDV) = await _db.GetDonViEmailAsync(cv.MaDVXL.Value);
        if (string.IsNullOrWhiteSpace(emailDV))
            return Json(new { ok = false, msg = "Đơn vị xử lý chưa cấu hình email nhận thông báo" });

        var danhSachFile = await _db.GetFileDinhKemAsync(mscv);
        var attachments = danhSachFile.Select(f => (f.TenFile, _fileSvc.GetFullPath(f.DuongDan))).ToList();

        cv.TenDVXL = tenDV;
        var (ok, msg) = await _email.GuiThongBaoCongVanDenAsync(cv, tenDV ?? "", emailDV, attachments);
        if (ok) await _db.DanhDauDaGuiEmailAsync(mscv, MaNV);
        return Json(new { ok, msg = ok ? $"Đã gửi email đến {emailDV}" : msg });
    }

    // Đánh dấu đã xem ngay trên danh sách (không cần mở Chi tiết)
    [HttpPost]
    public async Task<IActionResult> DanhDauDaXemAjax(string mscv)
    {
        if (MaNV > 0) await _db.DanhDauDaXemAsync(mscv, MaNV);
        return Json(new { ok = true });
    }

    // BUG BẢO MẬT đã vá 2026-09-12: action này TRƯỚC ĐÂY không kiểm tra quyền gì cả — bất kỳ ai
    // đăng nhập cũng xóa được file đính kèm của BẤT KỲ công văn đến nào (chỉ cần biết id+mscv, mà
    // mscv có định dạng tuần tự dễ đoán). Chỉ dùng ở form Nhập (xem Views/CongVanDen/Nhap.cshtml)
    // nên gác đúng bằng quyền "CongVanDen.Nhap" — giống hệt điều kiện của action Nhap chứa nó.
    [HttpPost]
    public async Task<IActionResult> XoaFile(int id, string mscv)
    {
        if (!CoQuyen("CongVanDen.Nhap")) return Json(new { ok = false, msg = "Không có quyền xóa file đính kèm." });
        var files = await _db.GetFileDinhKemAsync(mscv);
        var file = files.FirstOrDefault(f => f.ID == id);
        if (file != null)
        {
            _fileSvc.Delete(file.DuongDan);
            await _db.XoaFileDinhKemAsync(id);
        }
        return Json(new { ok = true });
    }

    // Xem trực tiếp 1 file đính kèm trên trình duyệt (không ép tải về) — nút "Xem PDF online"
    // BUG BẢO MẬT đã vá 2026-09-12: XemFile/TaiFileMot/TaiFile trước đây không kiểm tra phạm vi đơn
    // vị — kết hợp với lỗ hổng ChiTiet (đã vá ở trên) cho phép tải file của MỌI đơn vị. Cùng điều
    // kiện CoTheXemCongVanDen với trang Chi tiết.
    public async Task<IActionResult> XemFile(int id, string mscv)
    {
        var cv = await _db.GetCongVanDenByIdAsync(mscv);
        if (cv == null || !CoTheXemCongVanDen(cv)) return NotFound();
        var files = await _db.GetFileDinhKemAsync(mscv);
        var f = files.FirstOrDefault(x => x.ID == id);
        if (f == null) return NotFound();
        var fullPath = _fileSvc.GetFullPath(f.DuongDan);
        if (!System.IO.File.Exists(fullPath)) return NotFound();
        return PhysicalFile(fullPath, FileService.GetMimeType(f.TenFile));
    }

    // Tải về đúng 1 file đính kèm (thay vì luôn tải tất cả/nén zip như TaiFile)
    public async Task<IActionResult> TaiFileMot(int id, string mscv)
    {
        var cv = await _db.GetCongVanDenByIdAsync(mscv);
        if (cv == null || !CoTheXemCongVanDen(cv)) return NotFound();
        var files = await _db.GetFileDinhKemAsync(mscv);
        var f = files.FirstOrDefault(x => x.ID == id);
        if (f == null) return NotFound();
        var fullPath = _fileSvc.GetFullPath(f.DuongDan);
        if (!System.IO.File.Exists(fullPath)) return NotFound();
        return PhysicalFile(fullPath, FileService.GetMimeType(f.TenFile), f.TenFile);
    }

    public async Task<IActionResult> TaiFile(string mscv)
    {
        var cv = await _db.GetCongVanDenByIdAsync(mscv);
        if (cv == null || !CoTheXemCongVanDen(cv)) return NotFound();
        var files = await _db.GetFileDinhKemAsync(mscv);
        if (files.Count == 0) return NotFound();

        if (files.Count == 1)
        {
            var f = files[0];
            var fullPath = _fileSvc.GetFullPath(f.DuongDan);
            if (!System.IO.File.Exists(fullPath)) return NotFound();
            return PhysicalFile(fullPath, "application/octet-stream", f.TenFile);
        }

        // Nhiều file → nén zip. Nhiều file đính kèm có thể trùng TÊN GỐC (vd 2 file đều tên
        // "bao_cao.pdf") — ZipArchive KHÔNG tự đổi tên trùng, tạo 2 entry cùng tên vẫn "thành công"
        // nhưng 1 số phần mềm giải nén sẽ ghi đè/báo lỗi khó hiểu. Đánh số hậu tố (2), (3)... khi
        // trùng tên để entry trong zip luôn duy nhất — vá 2026-09-12.
        using var ms = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var tenDaDung = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in files)
            {
                var fullPath = _fileSvc.GetFullPath(f.DuongDan);
                if (!System.IO.File.Exists(fullPath)) continue;

                var tenFile = string.IsNullOrWhiteSpace(f.TenFile) ? "file" : f.TenFile;
                var tenTrongZip = tenFile;
                var soThuTu = 2;
                while (!tenDaDung.Add(tenTrongZip))
                {
                    var ext = Path.GetExtension(tenFile);
                    var ten = Path.GetFileNameWithoutExtension(tenFile);
                    tenTrongZip = $"{ten} ({soThuTu++}){ext}";
                }

                var entry = zip.CreateEntry(tenTrongZip);
                using var es = entry.Open();
                using var fs2 = System.IO.File.OpenRead(fullPath);
                fs2.CopyTo(es);
            }
        }
        ms.Position = 0;
        return File(ms.ToArray(), "application/zip", $"vanban_{mscv.Trim()}.zip");
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(string mscv)
    {
        if (!CoQuyen("CongVanDen.Xoa")) return Forbid();
        var duongDanFile = await _db.XoaCongVanDenAsync(mscv);
        // Xóa file vật lý SAU KHI DB đã xóa xong (transaction đã commit) — nếu làm ngược lại và DB
        // lỡ rollback thì đã mất file trong khi bản ghi vẫn còn.
        foreach (var d in duongDanFile) _fileSvc.Delete(d);
        TempData["Success"] = "Đã xóa công văn đến.";
        return RedirectToAction("Index");
    }

    // Theo dõi đang xử lý
    private const int DanhSachConPageSize = 30;

    public async Task<IActionResult> DangXuLy(int page = 1)
    {
        byte? filterMaDV = CoQuyen("Global.XemToanTruong") ? null : (byte?)MaDV;
        var (items, total) = await _db.GetCVDenDangXuLyPagedAsync(filterMaDV, MaNV, page, DanhSachConPageSize);
        return View(new CongVanDenListViewModel { DanhSach = items, Trang = new PageInfo { Page = page, PageSize = DanhSachConPageSize, TotalCount = total } });
    }

    public async Task<IActionResult> DangXuLyTre(int page = 1)
    {
        byte? filterMaDV = CoQuyen("Global.XemToanTruong") ? null : (byte?)MaDV;
        var (items, total) = await _db.GetCVDenQuaHanPagedAsync(filterMaDV, MaNV, page, DanhSachConPageSize);
        return View(new CongVanDenListViewModel { DanhSach = items, Trang = new PageInfo { Page = page, PageSize = DanhSachConPageSize, TotalCount = total } });
    }

    public async Task<IActionResult> HoanThanhDungHan(int page = 1)
    {
        byte? filterMaDV = CoQuyen("Global.XemToanTruong") ? null : (byte?)MaDV;
        var (items, total) = await _db.GetCVDenHoanThanhPagedAsync(true, filterMaDV, MaNV, page, DanhSachConPageSize);
        return View(new CongVanDenListViewModel { DanhSach = items, Trang = new PageInfo { Page = page, PageSize = DanhSachConPageSize, TotalCount = total } });
    }

    public async Task<IActionResult> HoanThanhTre(int page = 1)
    {
        byte? filterMaDV = CoQuyen("Global.XemToanTruong") ? null : (byte?)MaDV;
        var (items, total) = await _db.GetCVDenHoanThanhPagedAsync(false, filterMaDV, MaNV, page, DanhSachConPageSize);
        return View(new CongVanDenListViewModel { DanhSach = items, Trang = new PageInfo { Page = page, PageSize = DanhSachConPageSize, TotalCount = total } });
    }

    public async Task<IActionResult> ChuaHoanThanh(int page = 1)
    {
        byte? filterMaDV = CoQuyen("Global.XemToanTruong") ? null : (byte?)MaDV;
        var (items, total) = await _db.GetCVDenQuaHanPagedAsync(filterMaDV, MaNV, page, DanhSachConPageSize);
        return View(new CongVanDenListViewModel { DanhSach = items, Trang = new PageInfo { Page = page, PageSize = DanhSachConPageSize, TotalCount = total } });
    }

    // Được xem chi tiết 1 công văn đến: đơn vị chủ trì/phối hợp của chính văn bản đó, hoặc có quyền
    // vượt phạm vi (toàn trường / vào sổ / xử lý đơn vị khác — văn thư trường cần xem lại mọi văn
    // bản đã nhập). Cùng logic lọc đã dùng ở Index/GetCongVanDenPagedAsync — chỉ áp cho trang riêng
    // từng văn bản để không lộ ra ngoài phạm vi đơn vị.
    // SỬA GẤP 2026-09-12: bản vá đầu tiên chỉ cho qua 3 quyền (Global.XemToanTruong/CongVanDen.Nhap/
    // CongVanDen.XuLyDV) — bỏ sót các quyền "Văn bản đến" khác vốn CŨNG hàm ý xử lý/xem vượt phạm vi
    // đơn vị (vd BGH chỉ giữ CongVanDen.BGHTraLoiYKien khi được xin ý kiến ngoài đơn vị họ), làm gãy
    // luôn việc xem/tải văn bản hợp lệ của những vai trò đó ngay sau khi deploy — lỗi thật do người
    // dùng báo lại. Nay CHỈ CẦN GIỮ 1 TRONG BẤT KỲ quyền "Văn bản đến" nào (tất cả đều ngụ ý được xử
    // lý/xem ngoài phạm vi đơn vị mình) — người bị chặn thật sự chỉ còn là tài khoản không giữ quyền
    // "Văn bản đến" nào cả và cũng không thuộc đơn vị liên quan (đúng đối tượng lỗ hổng ban đầu).
    private static readonly string[] QuyenVuotPhamViCongVanDen =
    {
        "Global.XemToanTruong", "CongVanDen.Nhap", "CongVanDen.Xoa", "CongVanDen.GuiEmail",
        "CongVanDen.XacNhanHoanThanh", "CongVanDen.XuLyDV", "CongVanDen.ChuyenXuLy",
        "CongVanDen.ChiDaoPhanCong", "CongVanDen.XinYKienBGH", "CongVanDen.TraLai", "CongVanDen.BGHTraLoiYKien"
    };
    private bool CoTheXemCongVanDen(CongVanDen cv) =>
        QuyenVuotPhamViCongVanDen.Any(CoQuyen)
        || cv.MaDVXL == MaDV
        || (',' + (cv.BoPhanPhoiHop ?? "") + ',').Contains($",{MaDV},");

    // Chi tiết + lịch sử xử lý
    // BUG BẢO MẬT đã vá 2026-09-12: TRƯỚC ĐÂY action này không kiểm tra phạm vi đơn vị — bất kỳ
    // tài khoản nào cũng xem được toàn bộ chi tiết/file/lịch sử xử lý của MỌI công văn đến chỉ cần
    // biết MSCV (định dạng tuần tự dễ đoán, vd 2026090011). Nay chỉ đơn vị liên quan hoặc người có
    // quyền vượt phạm vi mới xem được — xem CoTheXemCongVanDen ở trên.
    public async Task<IActionResult> ChiTiet(string id)
    {
        var cv = await _db.GetCongVanDenByIdAsync(id);
        if (cv == null) return NotFound();
        if (!CoTheXemCongVanDen(cv)) return Forbid();
        if (MaNV > 0) await _db.DanhDauDaXemAsync(id, MaNV);
        if (MaDV > 0) await _db.DanhDauDaXemXuLyDVAsync(id, MaDV);
        ViewBag.LichSuXuLy  = await _db.GetLichSuXuLyAsync(id);
        ViewBag.DVDaGiao    = await _db.GetCVDenDVAsync(id);
        ViewBag.FileDinhKem = await _db.GetFileDinhKemAsync(id);
        ViewBag.DonVi       = await _db.GetDonViAsync(chiLayConHoatDong: false);
        var xuLyDV          = await _db.GetXuLyDVAsync(id);
        ViewBag.XuLyDV      = xuLyDV;
        ViewBag.FilesByXuLyDV = await _db.GetXuLyDVFilesByMscvAsync(id);
        ViewBag.ChiDao      = await _db.GetChiDaoAsync(id);
        ViewBag.LanhDao     = await _db.GetLanhDaoAsync();

        // Tính sẵn theo từng đơn vị xuất hiện trong bảng xử lý: user có thao tác được (tiếp nhận/
        // cập nhật/upload) không, và có phải lãnh đạo đơn vị đó (chỉ đạo/phân công/xin ý kiến/trả
        // lại) không — tính 1 lần ở đây (có gọi DB) thay vì gọi lại nhiều lần trong Razor view.
        var maDVCoTheXuLy = new HashSet<byte>();
        var maDVLaLanhDao = new HashSet<byte>();
        foreach (var maDV in xuLyDV.Select(x => x.MaDV).Distinct())
        {
            if (await CoTheXuLyDonViAsync(maDV)) maDVCoTheXuLy.Add(maDV);
            if (await LaLanhDaoDonViAsync(maDV) || CoQuyen("CongVanDen.ChiDaoPhanCong")) maDVLaLanhDao.Add(maDV);
        }
        ViewBag.MaDVCoTheXuLy = maDVCoTheXuLy;
        ViewBag.MaDVLaLanhDao = maDVLaLanhDao;
        ViewBag.CoTheGuiEmail = cv.MaDVXL.HasValue &&
            (CoQuyen("CongVanDen.GuiEmail") || await LaLanhDaoDonViAsync(cv.MaDVXL.Value) || await LaVanThuDonViAsync(cv.MaDVXL.Value));

        // Tự động "Đã tiếp nhận" khi người của ĐÚNG đơn vị mình mở xem trang này lần đầu — chỉ áp
        // dụng cho đơn vị của người xem (session MaDV), KHÔNG áp dụng cho các đơn vị khác mà admin/
        // văn thư trung tâm có quyền vượt phạm vi xem được, để tránh mở xem hộ 1 lần lại làm tất cả
        // đơn vị khác bị đánh dấu tiếp nhận oan. Chỉ dừng ở "Đã tiếp nhận" (1) — muốn báo đang xử lý
        // thật vẫn phải tự bấm nút "Đang xử lý" như bình thường.
        if (MaDV > 0)
        {
            var rowMe = xuLyDV.FirstOrDefault(r => r.MaDV == MaDV);
            if (rowMe != null && rowMe.TrangThai == 0 && maDVCoTheXuLy.Contains(MaDV))
            {
                await _db.CapNhatTrangThaiXuLyDVAsync(id, MaDV, 1, MaNV, null);
                rowMe.TrangThai = 1;
            }
        }

        return View(cv);
    }

    // Xác nhận hoàn thành (admin/văn thư confirm chính thức)
    [HttpPost]
    public async Task<IActionResult> XacNhanHoanThanh(string mscv)
    {
        if (!CoQuyen("CongVanDen.XacNhanHoanThanh")) return Forbid();
        await _db.XacNhanHoanThanhAsync(mscv, MaNV);
        TempData["Success"] = "Đã xác nhận hoàn thành!";
        return RedirectToAction("ChiTiet", new { id = mscv });
    }

    [HttpPost]
    public async Task<IActionResult> HuyXacNhanHT(string mscv)
    {
        if (!CoQuyen("CongVanDen.XacNhanHoanThanh")) return Forbid();
        await _db.HuyXacNhanHTAsync(mscv);
        TempData["Success"] = "Đã hủy xác nhận hoàn thành.";
        return RedirectToAction("ChiTiet", new { id = mscv });
    }

    // Xuất Excel
    public async Task<IActionResult> XuatExcel(int? nam, byte? maSCV, string? tuKhoa,
        byte? maDVXL, string? nguoiKy, DateTime? tuNgay, DateTime? denNgay)
    {
        byte? filterMaDV = CoQuyen("Global.XemToanTruong") ? null : (byte?)MaDV;
        var list = await _db.GetCongVanDenAsync(nam, maSCV, tuKhoa, filterMaDV, MaNV, maDVXL, nguoiKy, tuNgay, denNgay);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sổ văn bản đến");
        ws.Cell(1, 1).Value = "Ngày đến"; ws.Cell(1, 2).Value = "Số CV";
        ws.Cell(1, 3).Value = "Cơ quan"; ws.Cell(1, 4).Value = "Ngày ban hành";
        ws.Cell(1, 5).Value = "Loại VB"; ws.Cell(1, 6).Value = "Trích yếu";
        ws.Cell(1, 7).Value = "Người ký"; ws.Cell(1, 8).Value = "Đơn vị XL";
        ws.Cell(1, 9).Value = "Ngày YCHT"; ws.Cell(1, 10).Value = "Ngày HT";
        int row = 2;
        foreach (var cv in list)
        {
            ws.Cell(row, 1).Value = cv.NgayDen.ToString("dd/MM/yyyy");
            ws.Cell(row, 2).Value = cv.STT1 ?? cv.STT.ToString();
            ws.Cell(row, 3).Value = cv.TenCQ;
            ws.Cell(row, 4).Value = cv.NgayBanHanh.ToString("dd/MM/yyyy");
            ws.Cell(row, 5).Value = cv.TenLVB;
            ws.Cell(row, 6).Value = cv.TrichYeu;
            ws.Cell(row, 7).Value = cv.NguoiKy;
            ws.Cell(row, 8).Value = cv.TenDVXL;
            ws.Cell(row, 9).Value = cv.NgayYCHT?.ToString("dd/MM/yyyy");
            ws.Cell(row, 10).Value = cv.NgayHT?.ToString("dd/MM/yyyy");
            row++;
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"CongVanDen_{nam}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> GetNextSTT(byte maSCV)
    {
        var stt = await _db.GetNextSTTAsync(maSCV, DateTime.Now.Year);
        return Json(stt);
    }

    [HttpPost]
    public async Task<IActionResult> ThemCoQuan([FromBody] string tenCQ)
    {
        if (string.IsNullOrWhiteSpace(tenCQ))
            return BadRequest("Tên cơ quan không được để trống");
        var maCQ = await _db.ThemCoQuanAsync(tenCQ.Trim());
        return Json(new { maCQ, tenCQ = tenCQ.Trim() });
    }

    // ── Theo dõi xử lý theo đơn vị ─────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> CapNhatTrangThaiDV(string mscv, byte maDV, byte trangThai, string? ghiChu)
    {
        if (!await CoTheXuLyDonViAsync(maDV))
            return Json(new { ok = false, msg = "Không có quyền thực hiện thao tác này" });
        await _db.CapNhatTrangThaiXuLyDVAsync(mscv, maDV, trangThai, MaNV, ghiChu);
        return Json(new { ok = true });
    }

    // Chuyển xử lý cho đơn vị khác — đơn vị thường chỉ được chuyển theo luồng Admin đã cấu hình
    // trước (LuongXuLy); admin/văn thư được chuyển tự do cho bất kỳ đơn vị nào.
    //
    // maDVNguon PHẢI lấy từ dòng người dùng thực sự bấm (client gửi lên) — KHÔNG được suy ra từ
    // MaDV của người đang đăng nhập. Bug đã sửa 2026-08-22: văn thư trung tâm/admin (có
    // CongVanDen.XuLyDV/ChuyenXuLy) đứng ngoài mọi đơn vị vẫn bấm "Chuyển xử lý" được trên dòng của
    // ĐƠN VỊ A — nếu lấy MaDV của họ làm nguồn thì ChuyenXuLyDVAsync sẽ update nhầm chỗ (WHERE
    // MaDV=@maDVNguon không khớp dòng của A), khiến CongVanDen.MaDVXL không được đồng bộ sang đơn vị
    // đích — và MỌI danh sách/badge (Index, DangXuLy, unread count) đều lọc theo MaDVXL, nên đơn vị
    // đích không bao giờ thấy văn bản này ở đâu cả, trừ khi có link trực tiếp.
    [HttpPost]
    public async Task<IActionResult> ChuyenXuLy(string mscv, byte maDVNguon, byte maDVDich, string? ghiChu)
    {
        if (!await CoTheXuLyDonViAsync(maDVNguon))
            return Json(new { ok = false, msg = "Bạn không có quyền chuyển xử lý cho đơn vị này." });

        // Chỉ đơn vị CHỦ TRÌ mới chuyển được toàn bộ trách nhiệm xử lý cho đơn vị khác — đơn vị
        // phối hợp chỉ hỗ trợ, không có quyền định đoạt luồng của cả văn bản. Kiểm tra này áp dụng
        // cho MỌI người gọi kể cả admin/văn thư trung tâm (không chỉ khi !khongGioiHan) — trước đây
        // chỉ kiểm khi thiếu quyền vượt phạm vi, nên 1 lệnh gọi với maDVNguon không tồn tại/không phải
        // chủ trì từ tài khoản có quyền vượt phạm vi vẫn "thành công" giả (tạo dòng đích mồ côi, không
        // dòng nguồn nào thực sự được đóng lại).
        var xuLyRows = await _db.GetXuLyDVAsync(mscv);
        var rowNguon = xuLyRows.FirstOrDefault(r => r.MaDV == maDVNguon);
        if (rowNguon == null || rowNguon.LoaiDV != 1)
            return Json(new { ok = false, msg = "Chỉ đơn vị chủ trì mới được chuyển xử lý cho đơn vị khác." });

        // Giới hạn luồng chuyển-đến-đâu theo cấu hình (LuongXuLy) chỉ áp dụng cho người KHÔNG có
        // quyền vượt phạm vi — admin/văn thư trung tâm được chuyển tự do cho bất kỳ đơn vị nào.
        if (!CoQuyen("CongVanDen.ChuyenXuLy"))
        {
            var choPhep = await _db.GetDonViDichChoPhepAsync(maDVNguon);
            if (!choPhep.Contains(maDVDich))
                return Json(new { ok = false, msg = "Đơn vị của bạn chưa được cấu hình luồng chuyển đến đơn vị này. Liên hệ Admin để cấu hình." });
        }

        var (ok, msg) = await _db.ChuyenXuLyDVAsync(mscv, maDVNguon, maDVDich, MaNV, ghiChu);
        return Json(new { ok, msg });
    }

    [HttpGet]
    public async Task<IActionResult> GetDonViDichChoPhep(byte maDVNguon)
    {
        if (CoQuyen("CongVanDen.ChuyenXuLy"))
        {
            var tatCa = await _db.GetDonViAsync(chiLayConHoatDong: true);
            return Json(tatCa.Where(d => d.MaDV != maDVNguon).Select(d => new { d.MaDV, d.TenDV }));
        }
        var choPhep = await _db.GetDonViDichChoPhepAsync(maDVNguon);
        var donVi = await _db.GetDonViAsync(chiLayConHoatDong: true);
        return Json(donVi.Where(d => choPhep.Contains(d.MaDV)).Select(d => new { d.MaDV, d.TenDV }));
    }

    // ── Luồng xử lý theo vai trò: Lãnh đạo đơn vị → BGH → Chuyên viên cụ thể ──

    private async Task<bool> LaLanhDaoDonViAsync(byte maDV)
    {
        var dv = await _db.GetDonViByIdAsync(maDV);
        return dv?.MaNV_TDV.HasValue == true && dv.MaNV_TDV.Value == MaNV;
    }

    private async Task<bool> LaVanThuDonViAsync(byte maDV) => await _db.LaVanThuDonViAsync(MaNV, maDV);

    // Được xử lý (tiếp nhận/cập nhật trạng thái/upload file) văn bản đến của 1 đơn vị: văn thư
    // trung tâm (CongVanDen.XuLyDV, thấy mọi đơn vị), hoặc lãnh đạo/văn thư CỦA ĐÚNG đơn vị đó.
    // RBAC v3: thắt chặt so với trước đây (trước đây BẤT KỲ ai có session thuộc đơn vị đó đều
    // thao tác được) — giờ chỉ 2 vai trò theo-đơn-vị này hoặc văn thư trung tâm mới được.
    private async Task<bool> CoTheXuLyDonViAsync(byte maDV) =>
        CoQuyen("CongVanDen.XuLyDV") || await LaLanhDaoDonViAsync(maDV) || await LaVanThuDonViAsync(maDV);

    [HttpGet]
    public async Task<IActionResult> GetNhanVienDonVi(byte maDV)
    {
        var ds = await _db.GetNhanVienAsync(maDV);
        return Json(ds.Select(nv => new { nv.MaNV, TenNV = $"{nv.HoNV} {nv.TenNV}" }));
    }

    [HttpPost]
    public async Task<IActionResult> ChiDaoPhanCong(string mscv, byte maDV, short maNVPhanCong,
        List<short>? nguoiPhoiHop, string? noiDung, DateTime? hanXuLy)
    {
        if (!CoQuyen("CongVanDen.ChiDaoPhanCong") && !await LaLanhDaoDonViAsync(maDV))
            return Json(new { ok = false, msg = "Chỉ lãnh đạo đơn vị (hoặc Admin/Văn thư) mới được chỉ đạo & phân công." });
        if (maNVPhanCong <= 0)
            return Json(new { ok = false, msg = "Vui lòng chọn ít nhất người chủ trì phụ trách chính." });

        var maCV = await _db.ChiDaoPhanCongAsync(mscv, maDV, MaNV, maNVPhanCong, nguoiPhoiHop, noiDung?.Trim(), hanXuLy);
        return Json(new { ok = true, maCV });
    }

    [HttpPost]
    public async Task<IActionResult> XinYKienBGH(string mscv, byte maDV, short maNVNhan, string noiDung)
    {
        if (!CoQuyen("CongVanDen.XinYKienBGH") && !await LaLanhDaoDonViAsync(maDV))
            return Json(new { ok = false, msg = "Chỉ lãnh đạo đơn vị (hoặc Admin/Văn thư) mới được xin ý kiến BGH." });
        if (maNVNhan <= 0 || string.IsNullOrWhiteSpace(noiDung))
            return Json(new { ok = false, msg = "Vui lòng chọn lãnh đạo BGH và nhập nội dung xin ý kiến." });

        await _db.XinYKienBGHAsync(mscv, maDV, MaNV, maNVNhan, noiDung.Trim());
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> BGHTraLoiYKien(string mscv, byte maDV, string noiDung)
    {
        if (!CoQuyen("CongVanDen.BGHTraLoiYKien"))
            return Json(new { ok = false, msg = "Chỉ lãnh đạo BGH mới cho ý kiến chỉ đạo được." });
        if (string.IsNullOrWhiteSpace(noiDung))
            return Json(new { ok = false, msg = "Vui lòng nhập nội dung ý kiến chỉ đạo." });

        await _db.BGHTraLoiYKienAsync(mscv, maDV, MaNV, noiDung.Trim());
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> TraLaiXuLy(string mscv, byte maDV, string lyDo)
    {
        if (!CoQuyen("CongVanDen.TraLai") && !await LaLanhDaoDonViAsync(maDV))
            return Json(new { ok = false, msg = "Chỉ lãnh đạo đơn vị (hoặc Admin/Văn thư) mới được trả lại." });
        if (string.IsNullOrWhiteSpace(lyDo))
            return Json(new { ok = false, msg = "Vui lòng nhập lý do trả lại." });

        await _db.TraLaiXuLyDVAsync(mscv, maDV, MaNV, lyDo.Trim());
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> UploadFileXuLy(int id, string mscv, List<IFormFile> files)
    {
        if (files == null || files.Count == 0 || files.All(f => f.Length == 0))
        {
            TempData["Error"] = "Vui lòng chọn ít nhất 1 file để upload";
            return RedirectToAction("ChiTiet", new { id = mscv.Trim() });
        }
        var row = await _db.GetXuLyDVByIdAsync(id);
        if (row == null) return NotFound();
        if (!await CoTheXuLyDonViAsync(row.MaDV))
        {
            TempData["Error"] = "Không có quyền upload file cho đơn vị này";
            return RedirectToAction("ChiTiet", new { id = mscv.Trim() });
        }
        int soLuong = 0;
        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            var relPath = await _fileSvc.SaveAsync(file, "xulydv");
            await _db.ThemXuLyDVFileAsync(id, file.FileName, relPath, MaNV);
            soLuong++;
        }
        TempData["Success"] = soLuong == 1 ? "Đã lưu 1 file." : $"Đã lưu {soLuong} file.";
        return RedirectToAction("ChiTiet", new { id = mscv.Trim() });
    }

    [HttpPost]
    public async Task<IActionResult> XoaFileXuLy(int fileId, string mscv)
    {
        var f = await _db.GetXuLyDVFileByIdAsync(fileId);
        if (f == null) return NotFound();
        var row = await _db.GetXuLyDVByIdAsync(f.XuLyDVID);
        if (row == null) return NotFound();
        if (!await CoTheXuLyDonViAsync(row.MaDV))
        {
            TempData["Error"] = "Không có quyền xóa file này";
            return RedirectToAction("ChiTiet", new { id = mscv.Trim() });
        }
        _fileSvc.Delete(f.DuongDan);
        await _db.XoaXuLyDVFileAsync(fileId);
        TempData["Success"] = "Đã xóa file.";
        return RedirectToAction("ChiTiet", new { id = mscv.Trim() });
    }

    public async Task<IActionResult> TaiFileXuLy(int fileId)
    {
        // Không giới hạn theo đơn vị — giữ đúng hành vi cũ (ai xem được trang chi tiết văn bản đều
        // tải được file kết quả xử lý, không riêng đơn vị đang xử lý dòng đó).
        var f = await _db.GetXuLyDVFileByIdAsync(fileId);
        if (f == null) return NotFound();
        var fullPath = _fileSvc.GetFullPath(f.DuongDan);
        if (!System.IO.File.Exists(fullPath)) return NotFound();
        return PhysicalFile(fullPath, "application/octet-stream", f.TenFile);
    }

    [HttpPost]
    public async Task<IActionResult> XacNhanHoanThanhAll()
    {
        if (!CoQuyen("CongVanDen.XacNhanHoanThanh")) return Forbid();
        var count = await _db.XacNhanHoanThanhTatCaQuaHanAsync(MaNV);
        TempData["Success"] = count > 0
            ? $"Đã xác nhận hoàn thành {count} công văn quá hạn."
            : "Không có công văn quá hạn nào cần xác nhận.";
        return RedirectToAction("Index");
    }

    private async Task<string> SaveFileAsync(IFormFile file)
        => await _fileSvc.SaveAsync(file, "congvanden");
}

