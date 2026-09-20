using Microsoft.AspNetCore.Mvc;
using CongVan.Models;
using CongVan.Services;
using ClosedXML.Excel;

namespace CongVan.Controllers;

public class CongVanDiController : BaseController
{
    private readonly DbService _db;
    private readonly FileService _fileSvc;
    private readonly EmailService _email;

    public CongVanDiController(DbService db, FileService fileSvc, EmailService email)
    {
        _db = db;
        _fileSvc = fileSvc;
        _email = email;
    }

    public async Task<IActionResult> Index(int? nam, byte? maSCV, string? tuKhoa, bool chiCongKhai = false)
    {
        nam ??= DateTime.Now.Year;
        var vm = new CongVanDiFilterViewModel
        {
            Nam = nam, MaSCV = maSCV, TuKhoa = tuKhoa, ChiCongKhai = chiCongKhai,
            // Viên chức thường chỉ xem văn bản đi đã gắn dấu công khai (văn bản đi của họ nằm ở "Dự thảo văn bản đi").
            DanhSach = await _db.GetCongVanDiAsync(nam, maSCV, tuKhoa, chiCongKhai || LaVienChucThuong),
            DanhSachSoCV = await _db.GetSoCVAsync(),
            DanhSachDonVi = await _db.GetDonViAsync(chiLayConHoatDong: false)
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Nhap(string? id, int? duThao)
    {
        if (!CoQuyen("CongVanDi.Nhap") && !LaVanThuDonVi) return Forbid();
        var vm = new CongVanDiFormViewModel
        {
            CongVanDi = id != null ? (await _db.GetCongVanDiByIdAsync(id) ?? new()) : new CongVanDi { NgayCongVan = DateTime.Today, NgayBanHanh = DateTime.Today },
            DanhSachSoCV = await _db.GetSoCVAsync(),
            DanhSachLoaiVB = await _db.GetLoaiVBAsync(chiHienThi: true),
            DanhSachNhomCV = await _db.GetNhomCVAsync(),
            DanhSachLanhDao = await _db.GetLanhDaoAsync(),
            DanhSachDonVi = await _db.GetDonViAsync()
        };
        // Ban hành từ DỰ THẢO đã duyệt: điền sẵn thông tin để văn thư chỉ việc chọn sổ, cấp số và lưu.
        if (duThao.HasValue && string.IsNullOrEmpty(id))
        {
            var dt = await _db.GetDuThaoAsync(duThao.Value);
            if (dt == null || dt.TrangThai == DuThao.DaBanHanh || dt.PhamVi == 2) return NotFound(); // PhamVi=2 (nội bộ đơn vị) ban hành ở Văn bản nội bộ
            var dongGiu = await _db.GetDongDangChoAsync(VanBanXuLy.LoaiDuThao, dt.ID.ToString(), MaNV);
            if (dongGiu == null && !CoQuyen("CongVanDi.Nhap")) return Forbid();
            var cvNhap = vm.CongVanDi;
            cvNhap.TrichYeu = dt.TrichYeu;
            cvNhap.MaLVB = dt.MaLVB ?? 0;
            cvNhap.MaNCV = dt.MaNCV ?? 0;
            cvNhap.MaLDKy = dt.MaNVKy ?? 0;
            cvNhap.NguoiSoanThao = dt.TenNVSoan;
            cvNhap.DonViSoanThao = dt.TenDVSoan;
            cvNhap.DonViNhan = dt.DonViNhan;
            cvNhap.NoiNhanCV = dt.NoiNhanKhac;
            cvNhap.MSCVDen = dt.MSCVDen;
            ViewBag.DuThaoId = dt.ID;
            ViewBag.DuThaoFiles = await _db.GetFileDuThaoAsync(dt.ID);
        }
        if (!string.IsNullOrEmpty(vm.CongVanDi.DonViNhan))
            vm.DanhSachDVNhan = vm.CongVanDi.DonViNhan.Split(',')
                .Where(x => byte.TryParse(x.Trim(), out _))
                .Select(x => byte.Parse(x.Trim())).ToList();
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> GetNextSoKyHieu(byte maSCV, short? maLVB)
    {
        var (stt, soKyHieu) = await _db.GetNextSoKyHieuDiAsync(maSCV, maLVB == 0 ? null : maLVB);
        return Json(new { stt, soKyHieu });
    }

    public async Task<IActionResult> ChiTiet(string id)
    {
        var cv = await _db.GetCongVanDiByIdAsync(id);
        if (cv == null) return NotFound();
        if (LaVienChucThuong && !cv.CongKhai && !await _db.DuThaoCuaNguoiNayAsync(id.Trim(), MaNV)) return Forbid();
        ViewBag.DonVi = await _db.GetDonViAsync(chiLayConHoatDong: false);
        ViewBag.TrinhKyDangHoatDong = await _db.GetTrinhKyDangHoatDongAsync(2, id);
        ViewBag.NhanVien = await _db.GetNguoiDuyetTrinhKyAsync(MaDV, MaNV);
        ViewBag.DanhSachLuong = await _db.GetLuongDuyetAsync(2);
        return View(cv);
    }

    [HttpPost]
    public async Task<IActionResult> Nhap(CongVanDiFormViewModel vm, IFormFile? fileDinhKem, List<byte> dvNhan, bool cungLaVanBanDieuHanh, bool congKhai = false, int? duThaoId = null)
    {
        if (!CoQuyen("CongVanDi.Nhap") && !LaVanThuDonVi) return Forbid();
        var cv = vm.CongVanDi;
        cv.DonViNhan = dvNhan.Count > 0 ? string.Join(",", dvNhan) : null;

        if (string.IsNullOrWhiteSpace(cv.TrichYeu) || cv.MaSCV == 0 || cv.MaLVB == 0)
        {
            TempData["Error"] = string.IsNullOrWhiteSpace(cv.TrichYeu)
                ? "Trích yếu không được để trống."
                : "Vui lòng chọn đầy đủ Sổ văn bản và Loại văn bản.";
            vm.DanhSachSoCV = await _db.GetSoCVAsync();
            vm.DanhSachLoaiVB = await _db.GetLoaiVBAsync(chiHienThi: true);
            vm.DanhSachNhomCV = await _db.GetNhomCVAsync();
            vm.DanhSachLanhDao = await _db.GetLanhDaoAsync();
            vm.DanhSachDonVi = await _db.GetDonViAsync();
            vm.DanhSachDVNhan = dvNhan;
            return View(vm);
        }

        bool coFileMoi = fileDinhKem != null && fileDinhKem.Length > 0;
        if (coFileMoi)
            cv.FileDinhKem = await SaveFileAsync(fileDinhKem);
        else if (duThaoId.HasValue && string.IsNullOrEmpty(cv.MSCV))
            cv.FileDinhKem = (await _db.GetFileDuThaoAsync(duThaoId.Value)).FirstOrDefault()?.DuongDan; // dùng file của dự thảo

        bool isNew = string.IsNullOrEmpty(cv.MSCV);
        if (isNew)
            cv.MSCV = await _db.ThemCongVanDiAsync(cv, MaNV);
        else
            await _db.SuaCongVanDiAsync(cv);

        var cvFull = await _db.GetCongVanDiByIdAsync(cv.MSCV) ?? cv;
        if (duThaoId.HasValue && isNew) await _db.DanhDauDuThaoDaBanHanhAsync(duThaoId.Value, cv.MSCV!.Trim(), MaNV);

        // Văn bản đi gửi cho đơn vị nội bộ = văn bản đến của đơn vị đó (trừ văn bản đến
        // thật từ cơ quan ngoài trường, do văn thư nhập riêng) — tự tạo văn bản đến cho từng
        // đơn vị được chọn, dùng lại toàn bộ hạ tầng công văn đến (xử lý, đã xem, email).
        // Khi SỬA: các đơn vị đã có mirror từ trước chỉ được cập nhật nội dung (không tạo lại,
        // không xóa nếu đơn vị đó bị bỏ chọn) để không mất lịch sử xử lý đã ghi nhận; chỉ đơn vị
        // vừa thêm mới ở lần sửa này mới được tạo mirror + gửi email mới.
        var mirrorsHienCo = isNew
            ? new List<(string MSCV, byte MaDVXL)>()
            : await _db.GetVanBanDenMirrorsAsync(cvFull.MSCV!);
        var donViDaCoMirror = mirrorsHienCo.Select(m => m.MaDVXL).ToHashSet();

        foreach (var m in mirrorsHienCo)
            await _db.CapNhatVanBanDenTuVanBanDiAsync(m.MSCV, cvFull, coFileMoi);

        // Không tự gửi email nữa — mỗi mirror là 1 văn bản đến riêng, văn thư/đơn vị bấm nút
        // "Gửi email thông báo" trên trang chi tiết văn bản đến (CongVanDenController.GuiEmail)
        // khi sẵn sàng gửi, thay vì hệ thống tự gửi ngay lúc lưu.
        foreach (var maDV in dvNhan.Distinct().Where(d => !donViDaCoMirror.Contains(d)))
            await _db.TaoVanBanDenTuVanBanDiAsync(cvFull, maDV, MaNV);

        // Tick "Cũng là văn bản điều hành": nếu đã có bản ghi từ trước (do lần lưu trước đã tick)
        // thì chỉ cập nhật nội dung; chưa có mà giờ mới tick thì tạo mới; bỏ tick không tự xóa.
        var vbdhHienCo = isNew ? null : await _db.GetVanBanDieuHanhByMscvDiAsync(cvFull.MSCV!);
        if (vbdhHienCo != null)
            await _db.CapNhatVanBanDieuHanhTuCongVanDiAsync(vbdhHienCo.MSCV!.Trim(), cvFull);
        else if (cungLaVanBanDieuHanh)
            await _db.ThemVanBanDieuHanhTuCongVanDiAsync(cvFull, MaNV);

        // Dấu công khai — chỉ đổi khi trạng thái trên form khác với hiện tại (để giữ NgayCongKhai cũ).
        if ((CoQuyen("CongVanDi.CongKhai") || CoQuyen("CongVanDi.Nhap")) && congKhai != cvFull.CongKhai)
            await _db.DatCongKhaiCongVanDiAsync(cvFull.MSCV!, congKhai, MaNV);

        TempData["Success"] = "Lưu thành công!";
        return RedirectToAction("Index");
    }

    // Gắn / gỡ "dấu công khai" — văn bản đi công khai lộ ra qua API api/v1/vanban-cong-khai
    [HttpPost]
    public async Task<IActionResult> DatCongKhai(string mscv, bool congKhai)
    {
        if (!CoQuyen("CongVanDi.CongKhai") && !CoQuyen("CongVanDi.Nhap")) return Forbid();
        var cv = await _db.GetCongVanDiByIdAsync(mscv);
        if (cv == null) { TempData["Error"] = "Không tìm thấy văn bản."; return RedirectToAction("Index"); }
        await _db.DatCongKhaiCongVanDiAsync(mscv, congKhai, MaNV);
        TempData["Success"] = congKhai
            ? "Đã gắn dấu công khai — văn bản này giờ hiển thị được trên cổng công khai."
            : "Đã gỡ dấu công khai — văn bản này không còn hiển thị ra ngoài.";
        return RedirectToAction("ChiTiet", new { id = mscv });
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(string mscv)
    {
        if (!CoQuyen("CongVanDi.Xoa")) return Forbid();
        var cv = await _db.GetCongVanDiByIdAsync(mscv);
        if (cv == null) { TempData["Error"] = "Không tìm thấy văn bản cần xóa."; return RedirectToAction("Index"); }

        // Gom đường dẫn file (của văn bản đi + của các văn bản đến mirror) để xóa khỏi ổ đĩa sau khi
        // xóa DB thành công.
        var filePaths = new List<string?> { cv.FileDinhKem };
        foreach (var m in await _db.GetVanBanDenMirrorsAsync(mscv))
            filePaths.AddRange((await _db.GetFileDinhKemAsync(m.MSCV)).Select(f => f.DuongDan));

        try
        {
            await _db.XoaCongVanDiAsync(mscv);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Không xóa được văn bản — có thể văn bản đang được tham chiếu ở nơi khác. "
                              + "Chi tiết: " + ex.Message;
            return RedirectToAction("ChiTiet", new { id = mscv });
        }

        foreach (var p in filePaths.Where(p => !string.IsNullOrEmpty(p)))
            _fileSvc.Delete(p!);

        TempData["Success"] = "Đã xóa văn bản và mọi bản ghi liên quan (văn bản đến ở đơn vị nhận, văn bản điều hành).";
        return RedirectToAction("Index");
    }

    // Gửi email thông báo cho TẤT CẢ đơn vị nội bộ đã nhận (mirror) văn bản đi này —
    // gộp gửi 1 lần thay vì phải vào từng trang chi tiết văn bản đến để bấm từng cái.
    [HttpPost]
    public async Task<IActionResult> GuiEmailTatCa(string mscv)
    {
        if (!CoQuyen("CongVanDi.GuiEmail")) return Forbid();
        var mirrors = await _db.GetVanBanDenMirrorsAsync(mscv);
        if (mirrors.Count == 0) return Json(new { ok = false, msg = "Văn bản này chưa gửi cho đơn vị nội bộ nào" });

        int thanhCong = 0;
        var loi = new List<string>();
        foreach (var m in mirrors)
        {
            var cvDen = await _db.GetCongVanDenByIdAsync(m.MSCV);
            if (cvDen == null || !cvDen.MaDVXL.HasValue) continue;
            var (tenDV, emailDV) = await _db.GetDonViEmailAsync(cvDen.MaDVXL.Value);
            if (string.IsNullOrWhiteSpace(emailDV)) { loi.Add($"{tenDV ?? m.MaDVXL.ToString()}: chưa cấu hình email"); continue; }

            var danhSachFile = await _db.GetFileDinhKemAsync(m.MSCV);
            var attachments = danhSachFile.Select(f => (f.TenFile, _fileSvc.GetFullPath(f.DuongDan))).ToList();
            cvDen.TenDVXL = tenDV;
            var (ok, msg) = await _email.GuiThongBaoCongVanDenAsync(cvDen, tenDV ?? "", emailDV, attachments);
            if (ok) { await _db.DanhDauDaGuiEmailAsync(m.MSCV, MaNV); thanhCong++; }
            else loi.Add($"{tenDV}: {msg}");
        }

        return Json(new
        {
            ok = thanhCong > 0,
            msg = $"Đã gửi {thanhCong}/{mirrors.Count} đơn vị." + (loi.Count > 0 ? " Lỗi: " + string.Join("; ", loi) : "")
        });
    }

    // ── Ký số ─────────────────────────────────────────────────────────────
    // Không gọi trực tiếp API của phần mềm ký (VGCA/VNPT-CA/VNPT-CA Token) vì không có tài liệu
    // kỹ thuật xác thực — văn thư tải file cần ký về, ký bằng phần mềm sẵn có trên máy, rồi tải
    // file đã ký lên đây; hệ thống chỉ ghi nhận trạng thái + thay thế file chính thức.
    //
    // Nếu văn bản đang có 1 chuỗi "Trình ký nhiều cấp" hoạt động (xem bên dưới), việc tải file đã
    // ký lên CHỈ được thực hiện bởi người ở CẤP CUỐI của chuỗi đó, và chỉ khi mọi cấp trước đã
    // duyệt — không cho tải trực tiếp song song để tránh lặp lại kiểu "2 cơ chế đá nhau" đã từng
    // xảy ra ở luồng xử lý văn bản đến. Không có chuỗi nào đang hoạt động thì vẫn tải trực tiếp
    // như cũ (CongVanDi.KySo) — chuỗi nhiều cấp là tùy chọn, không bắt buộc cho mọi văn bản.
    [HttpPost]
    public async Task<IActionResult> TaiFileDaKy(string mscv, IFormFile fileDaKy, string loaiChungThu)
    {
        var active = await _db.GetTrinhKyDangHoatDongAsync(2, mscv);
        TrinhKyCap? capCuoi = null;
        if (active != null)
        {
            // Bước ký = bước có cờ LaBuocKy, nếu không có thì là bước cuối cùng.
            capCuoi = active.DanhSachCap.Where(c => c.LaBuocKy).OrderBy(c => c.ThuTu).LastOrDefault()
                      ?? active.DanhSachCap.OrderBy(c => c.ThuTu).LastOrDefault();
            var capDangCho = active.DanhSachCap.Where(c => c.TrangThai == 0).OrderBy(c => c.ThuTu).FirstOrDefault();
            if (capDangCho == null || capCuoi == null || capDangCho.ID != capCuoi.ID)
            {
                TempData["Error"] = "Văn bản đang trình ký nhiều bước — chưa đến lượt ký (còn bước chưa duyệt).";
                return RedirectToAction("ChiTiet", new { id = mscv });
            }
            if (!CoTheXuLyCap(capCuoi))
            {
                TempData["Error"] = "Bạn không phải người phụ trách bước ký của chuỗi trình ký này.";
                return RedirectToAction("ChiTiet", new { id = mscv });
            }
        }
        else if (!CoQuyen("CongVanDi.KySo"))
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
        var relPath = await _fileSvc.SaveAsync(fileDaKy, "congvandi");
        await _db.DanhDauDaKySoCongVanDiAsync(mscv, relPath, loaiChungThu, MaNV);
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

    // Người dùng hiện tại có được xử lý cấp duyệt này không: đúng người được chỉ định, HOẶC admin/
    // văn thư trường (CongVanDi.KySo), HOẶC khớp loại người duyệt của bước (chức năng / BGH).
    private bool CoTheXuLyCap(CongVan.Models.TrinhKyCap cap) =>
        cap.MaNVDuyet == MaNV
        || CoQuyen("CongVanDi.KySo")
        || (cap.LoaiNguoiDuyet == 3 && !string.IsNullOrWhiteSpace(cap.GiaTriNguoiDuyet) && CoQuyen(cap.GiaTriNguoiDuyet))
        || (cap.LoaiNguoiDuyet == 6 && Quyen.Contains(12));

    // Khởi tạo 1 chuỗi trình ký. Chọn 1 LUỒNG DUYỆT có sẵn (luongID) để hệ thống tự sinh các bước,
    // hoặc chọn tay danh sách người duyệt (nguoiDuyet). Văn thư đơn vị / người nhập văn bản đi đều
    // được phép trình. Không mở khi đã có 1 chuỗi đang hoạt động.
    [HttpPost]
    public async Task<IActionResult> TrinhKy(string mscv, int luongID, List<short> nguoiDuyet, string? ghiChu)
    {
        if (!CoQuyen("CongVanDi.Nhap") && !LaVanThuDonVi && !CoQuyen("CongVanDi.KySo")) return Forbid();

        var active = await _db.GetTrinhKyDangHoatDongAsync(2, mscv);
        if (active != null)
        {
            TempData["Error"] = "Văn bản đang có 1 chuỗi trình ký hoạt động — không thể mở chuỗi mới.";
            return RedirectToAction("ChiTiet", new { id = mscv });
        }

        var gc = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim();
        if (luongID > 0)
        {
            var (_, boQua) = await _db.KhoiTaoTrinhKyTheoLuongAsync(2, mscv, luongID, MaNV, gc);
            TempData["Success"] = "Đã trình ký theo luồng. Chờ lần lượt các bước duyệt."
                + (boQua.Count > 0 ? $" (Bỏ qua bước không tìm được người phụ trách: {string.Join(", ", boQua)})" : "");
        }
        else if (nguoiDuyet is { Count: > 0 })
        {
            await _db.TaoTrinhKyAsync(2, mscv, MaNV, nguoiDuyet, gc);
            TempData["Success"] = "Đã trình ký. Chờ lần lượt các cấp duyệt.";
        }
        else
        {
            TempData["Error"] = "Vui lòng chọn 1 luồng duyệt hoặc ít nhất 1 người duyệt.";
        }
        return RedirectToAction("ChiTiet", new { id = mscv });
    }

    // Duyệt 1 bước trung gian (không phải bước ký — bước ký thực hiện bằng cách tải file đã ký ở TaiFileDaKy).
    [HttpPost]
    public async Task<IActionResult> DuyetTrinhKy(string mscv, int capId, string? ghiChu)
    {
        var active = await _db.GetTrinhKyDangHoatDongAsync(2, mscv);
        var cap = active?.DanhSachCap.FirstOrDefault(c => c.ID == capId);
        if (active == null || cap == null) return NotFound();

        var capDangCho = active.DanhSachCap.Where(c => c.TrangThai == 0).OrderBy(c => c.ThuTu).FirstOrDefault();
        bool laBuocKy = cap.LaBuocKy || cap.ThuTu == active.DanhSachCap.Max(c => c.ThuTu);
        if (capDangCho == null || capDangCho.ID != capId || laBuocKy)
        {
            TempData["Error"] = "Không hợp lệ — bước này chưa đến lượt duyệt, hoặc đây là bước ký (tải file đã ký lên).";
            return RedirectToAction("ChiTiet", new { id = mscv });
        }
        if (!CoTheXuLyCap(cap))
        {
            TempData["Error"] = "Bạn không phải người phụ trách bước này.";
            return RedirectToAction("ChiTiet", new { id = mscv });
        }

        var okDuyet = await _db.DuyetCapTrinhKyAsync(capId, MaNV, string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim());
        TempData[okDuyet ? "Success" : "Error"] = okDuyet
            ? "Đã duyệt — chuyển sang bước tiếp theo."
            : "Bước này vừa được người khác xử lý (duyệt/trả lại/từ chối) — vui lòng tải lại trang.";
        return RedirectToAction("ChiTiet", new { id = mscv });
    }

    // Trả lại bước liền trước để làm lại (không hủy cả chuỗi). Chỉ hiện ở bước có ChoPhepTraLai.
    [HttpPost]
    public async Task<IActionResult> TraLaiTrinhKy(string mscv, int capId, string lyDo)
    {
        var active = await _db.GetTrinhKyDangHoatDongAsync(2, mscv);
        var cap = active?.DanhSachCap.FirstOrDefault(c => c.ID == capId);
        if (active == null || cap == null) return NotFound();

        var capDangCho = active.DanhSachCap.Where(c => c.TrangThai == 0).OrderBy(c => c.ThuTu).FirstOrDefault();
        if (capDangCho == null || capDangCho.ID != capId)
        {
            TempData["Error"] = "Không hợp lệ — bước này chưa đến lượt xử lý.";
            return RedirectToAction("ChiTiet", new { id = mscv });
        }
        if (!cap.ChoPhepTraLai)
        {
            TempData["Error"] = "Bước này không được cấu hình cho phép trả lại.";
            return RedirectToAction("ChiTiet", new { id = mscv });
        }
        if (!CoTheXuLyCap(cap)) { TempData["Error"] = "Bạn không phải người phụ trách bước này."; return RedirectToAction("ChiTiet", new { id = mscv }); }
        if (string.IsNullOrWhiteSpace(lyDo)) { TempData["Error"] = "Vui lòng nhập lý do trả lại."; return RedirectToAction("ChiTiet", new { id = mscv }); }

        var ok = await _db.TraLaiCapTrinhKyAsync(capId, MaNV, lyDo.Trim());
        TempData[ok ? "Success" : "Error"] = ok
            ? "Đã trả lại bước trước để làm lại."
            : "Không thể trả lại (đây là bước đầu — dùng \"Từ chối\" nếu muốn hủy chuỗi).";
        return RedirectToAction("ChiTiet", new { id = mscv });
    }

    [HttpPost]
    public async Task<IActionResult> TuChoiTrinhKy(string mscv, int capId, string lyDo)
    {
        var active = await _db.GetTrinhKyDangHoatDongAsync(2, mscv);
        var cap = active?.DanhSachCap.FirstOrDefault(c => c.ID == capId);
        if (active == null || cap == null) return NotFound();

        var capDangCho = active.DanhSachCap.Where(c => c.TrangThai == 0).OrderBy(c => c.ThuTu).FirstOrDefault();
        if (capDangCho == null || capDangCho.ID != capId)
        {
            TempData["Error"] = "Không hợp lệ — bước này chưa đến lượt xử lý.";
            return RedirectToAction("ChiTiet", new { id = mscv });
        }
        if (!CoTheXuLyCap(cap))
        {
            TempData["Error"] = "Bạn không phải người phụ trách bước này.";
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
            : "Bước này vừa được người khác xử lý — vui lòng tải lại trang.";
        return RedirectToAction("ChiTiet", new { id = mscv });
    }

    // ── Nhập hàng loạt từ Excel ────────────────────────────────────────────
    // Thứ tự cột (khớp với NhapExcel bên dưới) — mọi cột "đơn vị" đều có ô xổ xuống
    // lấy từ sheet "DanhMuc" khi mở bằng Excel:
    //  1 Sổ văn bản(*)  2 Loại văn bản(*)  3 STT  4 Số ký hiệu  5 Ngày văn bản(*)
    //  6 Ngày ban hành  7 Người ký  8 Trích yếu(*)  9 Đơn vị soạn thảo  10 Người soạn thảo
    // 11-13 Đơn vị nhận (1..3)  14 Đơn vị nhận khác  15 Nơi nhận ngoài trường
    // 16 Số lượng  17 Ghi chú
    // Giá trị đặc biệt cho cột "Đơn vị nhận" — gõ (hoặc chọn từ dropdown) để gửi hàng loạt.
    private const string TatCaDonVi = "(TẤT CẢ ĐƠN VỊ)";
    private const string TatCaKhoa  = "(TẤT CẢ KHOA)";
    private const string TatCaPhong = "(TẤT CẢ PHÒNG/BAN/TT)";

    private static readonly string[] ExcelHeaders =
    {
        "Sổ văn bản (*)", "Loại văn bản (*)", "STT", "Số ký hiệu", "Ngày văn bản (dd/MM/yyyy) (*)",
        "Ngày ban hành (dd/MM/yyyy)", "Người ký", "Trích yếu (*)",
        "Đơn vị soạn thảo", "Người soạn thảo",
        "Đơn vị nhận (1)", "Đơn vị nhận (2)", "Đơn vị nhận (3)",
        "Đơn vị nhận khác (mã/tên, cách nhau dấu ;)", "Nơi nhận ngoài trường",
        "Số lượng", "Ghi chú"
    };

    public async Task<IActionResult> TaiMauExcel()
    {
        var soCV    = await _db.GetSoCVAsync();
        var loaiVB  = await _db.GetLoaiVBAsync(chiHienThi: true);
        var lanhDao = await _db.GetLanhDaoAsync();
        var donVi   = await _db.GetDonViAsync();

        using var wb = new XLWorkbook();

        // Tên hiển thị + dùng để nhập cho mỗi đơn vị: ưu tiên tên viết tắt, không có thì tên đầy đủ.
        string TenChonDV(DonVi d) => string.IsNullOrWhiteSpace(d.TenTat) ? d.TenDV : d.TenTat.Trim();

        // ── Sheet "DanhMuc": tra cứu + nguồn cho ô chọn xổ xuống ──
        var dm = wb.Worksheets.Add("DanhMuc");
        dm.Cell(1, 1).Value = "Sổ văn bản";
        dm.Cell(1, 2).Value = "Loại văn bản";
        dm.Cell(1, 3).Value = "Người ký";
        dm.Cell(1, 4).Value = "Đơn vị nhận (chọn — có cả 'gửi tất cả')";
        dm.Cell(1, 5).Value = "Mã đơn vị";
        dm.Cell(1, 6).Value = "Tên đơn vị đầy đủ";
        dm.Row(1).Style.Font.Bold = true;
        for (int i = 0; i < soCV.Count; i++)    dm.Cell(i + 2, 1).Value = soCV[i].TenSCV;
        for (int i = 0; i < loaiVB.Count; i++)  dm.Cell(i + 2, 2).Value = loaiVB[i].TenLVB;
        for (int i = 0; i < lanhDao.Count; i++) dm.Cell(i + 2, 3).Value = lanhDao[i].HoTen;
        // Cột D: 3 lựa chọn "gửi tất cả" ở đầu, rồi tới danh sách đơn vị.
        dm.Cell(2, 4).Value = TatCaDonVi;
        dm.Cell(3, 4).Value = TatCaKhoa;
        dm.Cell(4, 4).Value = TatCaPhong;
        for (int i = 0; i < donVi.Count; i++)
        {
            dm.Cell(i + 5, 4).Value = TenChonDV(donVi[i]);
            dm.Cell(i + 5, 5).Value = donVi[i].MaDV;
            dm.Cell(i + 5, 6).Value = donVi[i].TenDV;
        }
        dm.Columns().AdjustToContents();

        // Đặt tên vùng (defined name) cho từng danh mục — dùng tên vùng trong ô chọn thay vì
        // tham chiếu chéo sheet trực tiếp, vì cách này Excel/LibreOffice/WPS đều hiện mũi tên
        // xổ xuống ổn định (tham chiếu "=DanhMuc!$A$2:$A$..." nhiều bản Excel không dựng dropdown).
        void DatTen(string ten, int col, int firstRow, int lastRow)
        {
            if (lastRow < firstRow) return;
            wb.DefinedNames.Add(ten, dm.Range(firstRow, col, lastRow, col));
        }
        DatTen("DS_SoVB",     1, 2, soCV.Count + 1);
        DatTen("DS_LoaiVB",   2, 2, loaiVB.Count + 1);
        DatTen("DS_NguoiKy",  3, 2, lanhDao.Count + 1);
        DatTen("DS_DonVi",    4, 5, donVi.Count + 4);            // chỉ tên đơn vị (cho cột Đơn vị soạn thảo)
        DatTen("DS_DonViNhan", 4, 2, donVi.Count + 4);           // gồm cả 3 lựa chọn "gửi tất cả"

        // ── Sheet mẫu nhập ──
        var ws = wb.Worksheets.Add("Mau nhap van ban di");
        for (int i = 0; i < ExcelHeaders.Length; i++) ws.Cell(1, i + 1).Value = ExcelHeaders[i];
        ws.Row(1).Style.Font.Bold = true;
        ws.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F5FF");
        ws.SheetView.FreezeRows(1);

        ws.Cell(2, 1).Value = soCV.FirstOrDefault()?.TenSCV ?? "Sổ công văn";
        ws.Cell(2, 2).Value = loaiVB.FirstOrDefault()?.TenLVB ?? "Quyết định";
        ws.Cell(2, 3).Value = 1;
        ws.Cell(2, 4).Value = "01/QĐ-ĐHKG";
        ws.Cell(2, 5).Value = "02/01/2026";
        ws.Cell(2, 6).Value = "02/01/2026";
        ws.Cell(2, 7).Value = lanhDao.FirstOrDefault()?.HoTen ?? "Nguyễn Văn A";
        ws.Cell(2, 8).Value = "Ví dụ: Về việc ...";
        ws.Cell(2, 9).Value = donVi.FirstOrDefault() is { } dv0 ? TenChonDV(dv0) : "";
        ws.Cell(2, 10).Value = "Nguyễn Văn A";
        ws.Cell(2, 11).Value = TatCaDonVi;   // ví dụ: gửi cho tất cả đơn vị
        ws.Cell(2, 15).Value = "Kho bạc Nhà nước tỉnh Kiên Giang";
        ws.Cell(2, 16).Value = 1;

        void AddList(int col, string tenVung, int count)
        {
            if (count <= 0) return;
            var v = ws.Range(2, col, 2000, col).CreateDataValidation();
            v.List("=" + tenVung, true);   // true = hiện mũi tên xổ xuống trong ô
            v.IgnoreBlanks = true;
        }
        AddList(1,  "DS_SoVB",    soCV.Count);     // Sổ văn bản
        AddList(2,  "DS_LoaiVB",  loaiVB.Count);   // Loại văn bản
        AddList(7,  "DS_NguoiKy", lanhDao.Count);  // Người ký
        AddList(9,  "DS_DonVi",    donVi.Count);    // Đơn vị soạn thảo (chỉ tên đơn vị)
        AddList(11, "DS_DonViNhan", donVi.Count);   // Đơn vị nhận (1) — kèm "gửi tất cả"
        AddList(12, "DS_DonViNhan", donVi.Count);   // Đơn vị nhận (2)
        AddList(13, "DS_DonViNhan", donVi.Count);   // Đơn vị nhận (3)

        ws.Columns().AdjustToContents();
        ws.Column(8).Width = 45;
        for (int c = 9; c <= 15; c++) ws.Column(c).Width = 24;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Mau_NhapVanBanDi.xlsx");
    }

    [HttpPost]
    public async Task<IActionResult> NhapExcel(IFormFile? file, bool taoVanBanDen = true)
    {
        if (!CoQuyen("CongVanDi.Excel")) return Forbid();
        var ketQua = new KetQuaNhapExcelViewModel();
        if (file == null || file.Length == 0)
        {
            ketQua.Loi.Add("Vui lòng chọn file Excel để nhập.");
            return View("KetQuaNhapExcel", ketQua);
        }

        var danhSachDonVi   = await _db.GetDonViAsync(chiLayConHoatDong: false);
        var danhSachDonViHD  = await _db.GetDonViAsync(chiLayConHoatDong: true);   // đang hoạt động — cho "gửi tất cả"

        using var stream = file.OpenReadStream();
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First(w => !w.Name.Equals("DanhMuc", StringComparison.OrdinalIgnoreCase));

        int dong = 1;
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            dong++;
            var tenSCV    = row.Cell(1).GetString().Trim();
            var tenLVB    = row.Cell(2).GetString().Trim();
            var sttText   = row.Cell(3).GetString().Trim();
            var stt1      = row.Cell(4).GetString().Trim();
            var ngayVB    = DocNgayExcel(row.Cell(5));
            var ngayBH    = DocNgayExcel(row.Cell(6));
            var nguoiKy   = row.Cell(7).GetString().Trim();
            var trichYeu  = row.Cell(8).GetString().Trim();
            var dvSoanTx  = row.Cell(9).GetString().Trim();
            var nguoiSoan = row.Cell(10).GetString().Trim();
            var dvNhanTx  = string.Join(";", new[] { 11, 12, 13, 14 }.Select(c => row.Cell(c).GetString().Trim()));
            var noiNhan   = row.Cell(15).GetString().Trim();
            var soLuongTx = row.Cell(16).GetString().Trim();
            var ghiChu    = row.Cell(17).GetString().Trim();

            if (string.IsNullOrWhiteSpace(trichYeu) && string.IsNullOrWhiteSpace(stt1) && string.IsNullOrWhiteSpace(tenSCV))
                continue; // dòng trống hoàn toàn, bỏ qua âm thầm

            if (string.IsNullOrWhiteSpace(trichYeu))
            {
                ketQua.Loi.Add($"Dòng {dong}: thiếu Trích yếu, đã bỏ qua.");
                continue;
            }
            if (!ngayVB.HasValue)
            {
                ketQua.Loi.Add($"Dòng {dong}: Ngày văn bản trống hoặc sai định dạng (dd/MM/yyyy), đã bỏ qua.");
                continue;
            }

            // Phân giải "Đơn vị nhận": chấp nhận mã số, tên, tên viết tắt, hoặc từ khóa gửi hàng loạt
            // ("(TẤT CẢ ĐƠN VỊ)" / "TẤT CẢ" / "ALL", "(TẤT CẢ KHOA)", "(TẤT CẢ PHÒNG/BAN/TT)").
            var maDVNhan = new List<byte>();
            void ThemDV(IEnumerable<DonVi> ds) { foreach (var d in ds) if (!maDVNhan.Contains(d.MaDV)) maDVNhan.Add(d.MaDV); }
            foreach (var phan in dvNhanTx.Split(new[] { ';', ',', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = phan.Trim();
                if (p.Length == 0) continue;
                var key = BoDau(p).ToUpperInvariant().Replace("(", "").Replace(")", "").Trim();

                if (key is "TAT CA" or "TAT CA DON VI" or "ALL" or "*" or "TATCA")
                { ThemDV(danhSachDonViHD); continue; }
                if (key is "TAT CA KHOA" or "TAT CA CAC KHOA" or "CAC KHOA")
                { ThemDV(danhSachDonViHD.Where(d => string.Equals(d.LoaiDV?.Trim(), "K", StringComparison.OrdinalIgnoreCase))); continue; }
                if (key.StartsWith("TAT CA PHONG") || key is "CAC PHONG" or "PHONG BAN TT")
                { ThemDV(danhSachDonViHD.Where(d => new[] { "P", "B", "T" }.Contains((d.LoaiDV ?? "").Trim().ToUpperInvariant()))); continue; }

                DonVi? dv = byte.TryParse(p, out var ma)
                    ? danhSachDonVi.FirstOrDefault(d => d.MaDV == ma)
                    : danhSachDonVi.FirstOrDefault(d =>
                          string.Equals(d.TenDV?.Trim(), p, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(d.TenTat?.Trim(), p, StringComparison.OrdinalIgnoreCase));
                if (dv != null) { if (!maDVNhan.Contains(dv.MaDV)) maDVNhan.Add(dv.MaDV); }
                else ketQua.Loi.Add($"Dòng {dong}: không nhận ra đơn vị \"{p}\" (bỏ qua đơn vị này).");
            }

            try
            {
                var maSCV = await _db.FindOrCreateSoCVAsync(tenSCV);
                var maLVB = await _db.FindOrCreateLoaiVBAsync(tenLVB);
                var maLDKy = await _db.FindNhanVienTheoTenAsync(nguoiKy);
                if (!string.IsNullOrWhiteSpace(nguoiKy) && maLDKy == null)
                    ketQua.Loi.Add($"Dòng {dong}: không tìm thấy người ký \"{nguoiKy}\" — để trống.");

                var cv = new CongVanDi
                {
                    MaSCV = maSCV,
                    MaLVB = maLVB,
                    STT = double.TryParse(sttText, out var sttVal) ? sttVal : 0,
                    STT1 = string.IsNullOrWhiteSpace(stt1) ? null : stt1,
                    NgayCongVan = ngayVB.Value,
                    NgayBanHanh = ngayBH ?? ngayVB.Value,
                    MaLDKy = maLDKy ?? 0,
                    TrichYeu = trichYeu,
                    DonViSoanThao = string.IsNullOrWhiteSpace(dvSoanTx) ? null : dvSoanTx,
                    NguoiSoanThao = string.IsNullOrWhiteSpace(nguoiSoan) ? null : nguoiSoan,
                    DonViNhan = maDVNhan.Count > 0 ? string.Join(",", maDVNhan) : null,
                    NoiNhanCV = string.IsNullOrWhiteSpace(noiNhan) ? null : noiNhan,
                    SoLuong = short.TryParse(soLuongTx, out var slVal) ? slVal : (short)1,
                    GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu
                };
                var mscv = await _db.ThemCongVanDiAsync(cv, MaNV);

                // Tạo văn bản đến cho từng đơn vị nội bộ được chọn (không tự gửi email).
                if (taoVanBanDen && maDVNhan.Count > 0)
                {
                    var cvFull = await _db.GetCongVanDiByIdAsync(mscv) ?? cv;
                    cvFull.MSCV ??= mscv;
                    foreach (var maDV in maDVNhan)
                        await _db.TaoVanBanDenTuVanBanDiAsync(cvFull, maDV, MaNV);
                }

                ketQua.SoDongThanhCong++;
            }
            catch (Exception ex)
            {
                ketQua.Loi.Add($"Dòng {dong}: lỗi khi lưu — {ex.Message}");
            }
        }

        return View("KetQuaNhapExcel", ketQua);
    }

    // Bỏ dấu tiếng Việt để so khớp từ khóa "gửi tất cả" bất kể người dùng gõ có dấu hay không.
    private static string BoDau(string s)
    {
        var norm = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in norm)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Replace('đ', 'd').Replace('Đ', 'D').Normalize(System.Text.NormalizationForm.FormC);
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
        var list = await _db.GetCongVanDiAsync(nam, maSCV, tuKhoa);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sổ văn bản đi");
        ws.Cell(1, 1).Value = "Số CV"; ws.Cell(1, 2).Value = "Ngày CV";
        ws.Cell(1, 3).Value = "Ngày ban hành"; ws.Cell(1, 4).Value = "Loại VB";
        ws.Cell(1, 5).Value = "Trích yếu"; ws.Cell(1, 6).Value = "Người ký";
        ws.Cell(1, 7).Value = "Nơi nhận"; ws.Cell(1, 8).Value = "Số lượng";
        int row = 2;
        foreach (var cv in list)
        {
            ws.Cell(row, 1).Value = cv.STT1 ?? cv.STT.ToString();
            ws.Cell(row, 2).Value = cv.NgayCongVan.ToString("dd/MM/yyyy");
            ws.Cell(row, 3).Value = cv.NgayBanHanh.ToString("dd/MM/yyyy");
            ws.Cell(row, 4).Value = cv.TenLVB;
            ws.Cell(row, 5).Value = cv.TrichYeu;
            ws.Cell(row, 6).Value = cv.TenLDKy;
            ws.Cell(row, 7).Value = cv.NoiNhanCV;
            ws.Cell(row, 8).Value = cv.SoLuong;
            row++;
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"CongVanDi_{nam}.xlsx");
    }

    private async Task<string> SaveFileAsync(IFormFile file)
        => await _fileSvc.SaveAsync(file, "congvandi");
}
