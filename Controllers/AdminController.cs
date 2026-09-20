using Microsoft.AspNetCore.Mvc;
using CongVan.Models;
using CongVan.Services;
using System.ComponentModel.DataAnnotations;

namespace CongVan.Controllers;

public class AdminController : BaseController
{
    private readonly DbService _db;
    private readonly EmailService _email;

    public AdminController(DbService db, EmailService email)
    {
        _db = db;
        _email = email;
    }

    // Trang tổng quan: mỗi mục trước đây là 1 tab nay là 1 trang riêng có link riêng trên menu;
    // Index() chỉ còn là màn hình tổng hợp/điều hướng nhanh + vài số liệu tổng quan. Vào được
    // hub nếu là admin/văn thư hoặc được cấp ít nhất 1 chức năng Admin.* — từng trang con vẫn
    // tự gác cổng theo đúng mã chức năng của nó khi thực sự mở ra.
    public async Task<IActionResult> Index()
    {
        bool coHub = Quyen.Contains(0) || ChucNang.Any(c => c.StartsWith("Admin."));
        if (!coHub) return Forbid();
        var nhanVien = await _db.GetAllNhanVienAsync();
        var donVi = await _db.GetDonViAsync(chiLayConHoatDong: false);
        var phanQuyen = await _db.GetPhanQuyenAsync();
        ViewBag.SoNhanVien = nhanVien.Count;
        ViewBag.SoDonViHD = donVi.Count(d => !d.DaGiaiThe);
        ViewBag.SoBGH = phanQuyen.Where(p => p.MaQuyen == 12).Select(p => p.MaNV).Distinct().Count();
        ViewBag.SoSoVanBan = (await _db.GetSoCVAsync()).Count;
        return View();
    }

    // Xuất danh sách tài khoản theo CẤP: Lãnh đạo trường, Lãnh đạo đơn vị, Văn thư cấp 1, Văn thư cấp 2, Chuyên viên.
    // Mỗi cấp 1 sheet (1 người giữ nhiều vai trò xuất hiện ở nhiều sheet), thêm sheet "Tất cả" (mỗi người 1 dòng, cấp cao nhất).
    // Mật khẩu KHÔNG xuất (lưu dạng băm 1 chiều, không khôi phục được).
    [HttpGet]
    public async Task<IActionResult> XuatTaiKhoanExcel(bool baoGomNghiViec = false)
    {
        if (!CoQuyen("Admin.NhanVien")) return Forbid();
        var ds = await _db.GetTaiKhoanTheoCapAsync(baoGomNghiViec);

        using var wb = new ClosedXML.Excel.XLWorkbook();
        string[] tieuDe = { "STT", "Họ và tên", "Tên đăng nhập", "Email", "Đơn vị", "Cấp cao nhất", "Các vai trò", "Trạng thái" };

        void VeSheet(string ten, IEnumerable<TaiKhoanCap> rows)
        {
            var ws = wb.Worksheets.Add(ten);
            for (int i = 0; i < tieuDe.Length; i++) ws.Cell(1, i + 1).Value = tieuDe[i];
            var hdr = ws.Row(1);
            hdr.Style.Font.Bold = true;
            hdr.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            hdr.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#1B6580");
            int r = 2, stt = 1;
            foreach (var t in rows)
            {
                ws.Cell(r, 1).Value = stt++;
                ws.Cell(r, 2).Value = t.HoTen;
                ws.Cell(r, 3).Value = t.Username ?? "";
                ws.Cell(r, 4).Value = t.Email ?? "";
                ws.Cell(r, 5).Value = t.TenDV ?? "";
                ws.Cell(r, 6).Value = t.CapCaoNhat;
                ws.Cell(r, 7).Value = t.CacVaiTro;
                ws.Cell(r, 8).Value = t.NgayNghiViec.HasValue ? $"Đã nghỉ việc ({t.NgayNghiViec:dd/MM/yyyy})" : "Đang làm việc";
                r++;
            }
            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();
            ws.Column(2).Width = Math.Max(ws.Column(2).Width, 24);
        }

        VeSheet("Tất cả", ds);
        VeSheet("Lãnh đạo trường", ds.Where(t => t.LdTruong));
        VeSheet("Lãnh đạo đơn vị", ds.Where(t => t.LdDonVi));
        VeSheet("Văn thư cấp 1", ds.Where(t => t.VtCap1));
        VeSheet("Văn thư cấp 2", ds.Where(t => t.VtCap2));
        VeSheet("Chuyên viên", ds.Where(t => t.ChuyenVien));

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"TaiKhoan_TheoCap_{DateTime.Today:yyyyMMdd}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> NhanVien()
    {
        if (!CoQuyen("Admin.NhanVien")) return Forbid();
        ViewBag.NhanVien = await _db.GetAllNhanVienAsync(baoGomNghiViec: true);
        ViewBag.DonVi = await _db.GetDonViAsync(chiLayConHoatDong: false);
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> PhanQuyen()
    {
        if (!CoQuyen("Admin.PhanQuyen")) return Forbid();
        ViewBag.NhanVien = await _db.GetAllNhanVienAsync();
        ViewBag.DonVi = await _db.GetDonViAsync(chiLayConHoatDong: false);
        ViewBag.QuyenXL = await _db.GetQuyenXLAsync();
        ViewBag.VaiTro = await _db.GetVaiTroAsync();
        ViewBag.ChucNangDangKy = ChucNangDangKy.TatCa;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> LanhDaoPhong()
    {
        if (!CoQuyen("Admin.LanhDaoPhong")) return Forbid();
        // baoGomNghiViec:true — nếu 1 đơn vị đang gán lãnh đạo/văn thư là người đã nghỉ việc (chưa
        // kịp đổi), trang này phải CHO THẤY rõ để admin biết mà gán lại, không được âm thầm ẩn đi.
        ViewBag.NhanVien = await _db.GetAllNhanVienAsync(baoGomNghiViec: true);
        ViewBag.DonVi = await _db.GetDonViAsync(chiLayConHoatDong: false);
        ViewBag.VanThuDonViMap = await _db.GetVanThuDonViMapAsync();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SetVanThuDonVi(byte maDV, short maNV, bool add)
    {
        if (!CoQuyen("Admin.LanhDaoPhong")) return Forbid();
        await _db.SetVanThuDonViAsync(maDV, maNV, add);
        return Json(new { ok = true });
    }

    // ── API Key theo đơn vị (cho web riêng của đơn vị gọi API Văn bản nội bộ) ────────────────────
    [HttpGet]
    public async Task<IActionResult> ApiKey()
    {
        if (!CoQuyen("Admin.ApiKey")) return Forbid();
        var vm = new DonViApiKeyFormViewModel
        {
            DanhSach = await _db.GetApiKeysAsync(),
            DanhSachDonVi = await _db.GetDonViAsync(chiLayConHoatDong: true)
        };
        if (TempData["KeyMoiTao"] is string k) vm.KeyMoiTao = k;
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> TaoApiKey(byte maDV, string tenKey)
    {
        if (!CoQuyen("Admin.ApiKey")) return Forbid();
        if (string.IsNullOrWhiteSpace(tenKey))
        {
            TempData["Error"] = "Vui lòng nhập tên/mô tả cho API Key (vd: \"Web Phòng CTSV\").";
            return RedirectToAction("ApiKey");
        }

        var keyBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var apiKeyPlain = Convert.ToHexString(keyBytes).ToLowerInvariant();
        var apiKeyHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(apiKeyPlain))).ToLowerInvariant();

        await _db.TaoApiKeyAsync(maDV, tenKey.Trim(), apiKeyHash, MaNV);
        TempData["Success"] = "Đã tạo API Key mới.";
        TempData["KeyMoiTao"] = apiKeyPlain; // chỉ hiện đúng 1 lần ở trang ApiKey ngay sau redirect
        return RedirectToAction("ApiKey");
    }

    [HttpPost]
    public async Task<IActionResult> ThuHoiApiKey(int id)
    {
        if (!CoQuyen("Admin.ApiKey")) return Forbid();
        await _db.ThuHoiApiKeyAsync(id);
        TempData["Success"] = "Đã thu hồi API Key.";
        return RedirectToAction("ApiKey");
    }

    [HttpGet]
    public async Task<IActionResult> BanGiamHieu()
    {
        if (!CoQuyen("Admin.BanGiamHieu")) return Forbid();
        ViewBag.NhanVien = await _db.GetAllNhanVienAsync(baoGomNghiViec: true);
        ViewBag.DonVi = await _db.GetDonViAsync(chiLayConHoatDong: false);
        ViewBag.PhanQuyen = await _db.GetPhanQuyenAsync();
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> DonVi()
    {
        if (!CoQuyen("Admin.DonVi")) return Forbid();
        ViewBag.NhanVien = await _db.GetAllNhanVienAsync(baoGomNghiViec: true);
        ViewBag.DonVi = await _db.GetDonViAsync(chiLayConHoatDong: false);
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> CoQuan()
    {
        if (!CoQuyen("Admin.CoQuan")) return Forbid();
        ViewBag.CoQuan = await _db.GetCoQuanAsync(chiHienThi: false);
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> LoaiVanBan()
    {
        if (!CoQuyen("Admin.LoaiVanBan")) return Forbid();
        ViewBag.LoaiVB = await _db.GetLoaiVBAsync(chiHienThi: false);
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> SoVanBan()
    {
        if (!CoQuyen("Admin.SoVanBan")) return Forbid();
        ViewBag.SoCV = await _db.GetSoCVAsync();
        ViewBag.LoaiVB = await _db.GetLoaiVBAsync(chiHienThi: false);
        ViewBag.MauSoKyHieu = await _db.GetMauSoKyHieuAsync();
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> CauHinhEmail()
    {
        if (!CoQuyen("Admin.CauHinhEmail")) return Forbid();
        ViewBag.EmailConfig = await _db.GetEmailConfigAsync();
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> LuongXuLy()
    {
        if (!CoQuyen("Admin.LuongXuLy")) return Forbid();
        ViewBag.DonVi = await _db.GetDonViAsync(chiLayConHoatDong: false);
        ViewBag.LuongXuLy = await _db.GetLuongXuLyAsync();
        return View();
    }

    // ── Cấu hình luồng duyệt / trình ký ──────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> LuongDuyet()
    {
        if (!CoQuyen("Admin.LuongDuyet")) return Forbid();
        var ds = await _db.GetLuongDuyetAsync(chiHienThi: false);
        foreach (var l in ds)
            l.DanhSachBuoc = (await _db.GetLuongDuyetChiTietAsync(l.ID))?.DanhSachBuoc ?? new();
        ViewBag.DanhSach = ds;
        ViewBag.ChucNang = CongVan.Models.ChucNangDangKy.TatCa;
        ViewBag.VaiTro = await _db.GetVaiTroAsync();
        ViewBag.NhanVien = await _db.GetLanhDaoTruongVaDonViAsync();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> LuuLuongDuyet([FromBody] LuuLuongDuyetRequest req)
    {
        if (!CoQuyen("Admin.LuongDuyet")) return Forbid();
        if (string.IsNullOrWhiteSpace(req.Ten) || req.Buoc == null || req.Buoc.Count == 0)
            return Json(new { ok = false, msg = "Cần tên luồng và ít nhất 1 bước." });

        var luong = new CongVan.Models.LuongDuyet
        {
            ID = req.ID, Ten = req.Ten.Trim(), LoaiVanBan = req.LoaiVanBan,
            MoTa = string.IsNullOrWhiteSpace(req.MoTa) ? null : req.MoTa.Trim(),
            MacDinh = req.MacDinh, HienThi = req.HienThi
        };
        var buoc = req.Buoc.Select(b => new CongVan.Models.LuongDuyetBuoc
        {
            TenBuoc = (b.TenBuoc ?? "").Trim(),
            LoaiNguoiDuyet = b.LoaiNguoiDuyet,
            GiaTri = string.IsNullOrWhiteSpace(b.GiaTri) ? null : b.GiaTri.Trim(),
            ChoPhepTraLai = b.ChoPhepTraLai,
            LaBuocKy = b.LaBuocKy
        }).ToList();
        var id = await _db.LuuLuongDuyetAsync(luong, buoc);
        return Json(new { ok = true, id });
    }

    [HttpPost]
    public async Task<IActionResult> XoaLuongDuyet(int id)
    {
        if (!CoQuyen("Admin.LuongDuyet")) return Forbid();
        await _db.XoaLuongDuyetAsync(id);
        return Json(new { ok = true });
    }

    public record LuuLuongDuyetRequest(int ID, string Ten, byte LoaiVanBan, string? MoTa,
        bool MacDinh, bool HienThi, List<LuuBuocRequest> Buoc);
    public record LuuBuocRequest(string? TenBuoc, byte LoaiNguoiDuyet, string? GiaTri, bool ChoPhepTraLai, bool LaBuocKy);

    [HttpGet]
    public async Task<IActionResult> PhongHop()
    {
        if (!CoQuyen("Admin.PhongHop")) return Forbid();
        ViewBag.PhongHop = await _db.GetPhongHopAsync(chiHienThi: false);
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ThemPhongHop(string tenPhong, string? viTri, short? sucChua, string? ghiChu)
    {
        if (!CoQuyen("Admin.PhongHop")) return Forbid();
        if (string.IsNullOrWhiteSpace(tenPhong))
            return Json(new { ok = false, msg = "Tên phòng không được để trống" });
        var maPhong = await _db.ThemPhongHopAsync(tenPhong.Trim(), string.IsNullOrWhiteSpace(viTri) ? null : viTri.Trim(),
            sucChua, string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim());
        return Json(new { ok = true, maPhong });
    }

    [HttpPost]
    public async Task<IActionResult> SuaPhongHop(byte maPhong, string tenPhong, string? viTri, short? sucChua, string? ghiChu)
    {
        if (!CoQuyen("Admin.PhongHop")) return Forbid();
        if (string.IsNullOrWhiteSpace(tenPhong))
            return Json(new { ok = false, msg = "Tên phòng không được để trống" });
        await _db.SuaPhongHopAsync(maPhong, tenPhong.Trim(), string.IsNullOrWhiteSpace(viTri) ? null : viTri.Trim(),
            sucChua, string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim());
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> SetHienThiPhongHop(byte maPhong, bool hienThi)
    {
        if (!CoQuyen("Admin.PhongHop")) return Forbid();
        await _db.SetHienThiPhongHopAsync(maPhong, hienThi);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> ThemLuongXuLy(byte maDVNguon, byte maDVDich, string? ghiChu)
    {
        if (!CoQuyen("Admin.LuongXuLy")) return Forbid();
        if (maDVNguon == maDVDich)
            return Json(new { ok = false, msg = "Đơn vị nguồn và đích phải khác nhau" });
        await _db.ThemLuongXuLyAsync(maDVNguon, maDVDich, string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim());
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> XoaLuongXuLy(int id)
    {
        if (!CoQuyen("Admin.LuongXuLy")) return Forbid();
        await _db.XoaLuongXuLyAsync(id);
        return Json(new { ok = true });
    }

    // ── Phân quyền ─────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> SetPhanQuyen(short maNV, byte maDV, byte maQuyen, bool add)
    {
        // Dùng chung bởi trang Phân quyền (mọi MaQuyen) và trang Ban Giám Hiệu (chỉ MaQuyen=12)
        if (!CoQuyen("Admin.PhanQuyen") && !(maQuyen == 12 && CoQuyen("Admin.BanGiamHieu"))) return Forbid();
        await _db.SetPhanQuyenAsync(maNV, maDV, maQuyen, add);
        return Json(new { ok = true });
    }

    public async Task<IActionResult> GetPhanQuyen(short maNV)
    {
        if (!CoQuyen("Admin.PhanQuyen")) return Forbid();
        var pq = await _db.GetPhanQuyenAsync(maNV);
        return Json(pq.Select(p => (int)p.MaQuyen).ToList());
    }

    // ── Vai trò (RBAC) ───────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> ThemVaiTro(string tenVaiTro, string? ghiChu)
    {
        if (!CoQuyen("Admin.PhanQuyen")) return Forbid();
        if (string.IsNullOrWhiteSpace(tenVaiTro))
            return Json(new { ok = false, msg = "Tên vai trò không được để trống" });
        var maVaiTro = await _db.ThemVaiTroAsync(tenVaiTro.Trim(), string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim());
        return Json(new { ok = true, maVaiTro });
    }

    [HttpPost]
    public async Task<IActionResult> XoaVaiTro(int maVaiTro)
    {
        if (!CoQuyen("Admin.PhanQuyen")) return Forbid();
        await _db.XoaVaiTroAsync(maVaiTro);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> SetVaiTroChucNang(int maVaiTro, string maChucNang, bool add)
    {
        if (!CoQuyen("Admin.PhanQuyen")) return Forbid();
        await _db.SetVaiTroChucNangAsync(maVaiTro, maChucNang, add);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> SetNhanVienVaiTro(short maNV, int maVaiTro, bool add)
    {
        if (!CoQuyen("Admin.PhanQuyen")) return Forbid();
        await _db.SetNhanVienVaiTroAsync(maNV, maVaiTro, add);
        return Json(new { ok = true });
    }

    public async Task<IActionResult> GetVaiTroCuaNhanVien(short maNV)
    {
        if (!CoQuyen("Admin.PhanQuyen")) return Forbid();
        return Json(await _db.GetVaiTroCuaNhanVienAsync(maNV));
    }

    // ── Hiển thị CoQuan / LoaiVB ───────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> SetHienThiCoQuan(short maCQ, bool hienThi)
    {
        if (!CoQuyen("Admin.CoQuan")) return Forbid();
        await _db.SetHienThiCoQuanAsync(maCQ, hienThi);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> SetHienThiLoaiVB(short maLVB, bool hienThi)
    {
        if (!CoQuyen("Admin.LoaiVanBan")) return Forbid();
        await _db.SetHienThiLoaiVBAsync(maLVB, hienThi);
        return Json(new { ok = true });
    }

    // Loại văn bản mặc định "dùng chung nội bộ" (văn thư nhập văn bản đến loại này thì tự tick dùng chung).
    [HttpPost]
    public async Task<IActionResult> SetDungChungLoaiVB(short maLVB, bool dungChung)
    {
        if (!CoQuyen("Admin.LoaiVanBan")) return Forbid();
        await _db.SetLoaiVBDungChungAsync(maLVB, dungChung);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> SetKyHieuLoaiVB(short maLVB, string? kyHieu)
    {
        if (!CoQuyen("Admin.LoaiVanBan")) return Forbid();
        await _db.SetKyHieuLoaiVBAsync(maLVB, string.IsNullOrWhiteSpace(kyHieu) ? null : kyHieu.Trim());
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> ThemLoaiVB(string tenLVB, string? kyHieu)
    {
        if (!CoQuyen("Admin.LoaiVanBan")) return Forbid();
        if (string.IsNullOrWhiteSpace(tenLVB))
            return Json(new { ok = false, msg = "Tên loại văn bản không được để trống" });
        var maLVB = await _db.ThemLoaiVBAsync(tenLVB.Trim(), string.IsNullOrWhiteSpace(kyHieu) ? null : kyHieu.Trim());
        return Json(new { ok = true, maLVB });
    }

    // ── Sổ văn bản & mẫu số ký hiệu ────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> ThemSoCV(string tenSCV, string? moTa)
    {
        if (!CoQuyen("Admin.SoVanBan")) return Forbid();
        if (string.IsNullOrWhiteSpace(tenSCV))
            return Json(new { ok = false, msg = "Tên sổ không được để trống" });
        var maSCV = await _db.ThemSoCVAsync(tenSCV.Trim(), string.IsNullOrWhiteSpace(moTa) ? null : moTa.Trim());
        return Json(new { ok = true, maSCV });
    }

    [HttpPost]
    public async Task<IActionResult> SuaSoCV(byte maSCV, string tenSCV, string? moTa)
    {
        if (!CoQuyen("Admin.SoVanBan")) return Forbid();
        if (string.IsNullOrWhiteSpace(tenSCV))
            return Json(new { ok = false, msg = "Tên sổ không được để trống" });
        await _db.SuaSoCVAsync(maSCV, tenSCV.Trim(), string.IsNullOrWhiteSpace(moTa) ? null : moTa.Trim());
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> SetDemRiengTheoLoai(byte maSCV, bool val)
    {
        if (!CoQuyen("Admin.SoVanBan")) return Forbid();
        await _db.SetDemRiengTheoLoaiAsync(maSCV, val);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> ThemMauSoKyHieu(byte maSCV, short? maLVB, string mauChuoi)
    {
        if (!CoQuyen("Admin.SoVanBan")) return Forbid();
        if (string.IsNullOrWhiteSpace(mauChuoi))
            return Json(new { ok = false, msg = "Mẫu chuỗi không được để trống" });
        await _db.ThemMauSoKyHieuAsync(maSCV, maLVB == 0 ? null : maLVB, mauChuoi.Trim());
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> XoaMauSoKyHieu(int id)
    {
        if (!CoQuyen("Admin.SoVanBan")) return Forbid();
        await _db.XoaMauSoKyHieuAsync(id);
        return Json(new { ok = true });
    }

    // ── Nhân viên CRUD ─────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> ThemNhanVien(NhanVien nv, string matKhau)
    {
        if (!CoQuyen("Admin.NhanVien")) return Forbid();
        if (string.IsNullOrWhiteSpace(nv.HoNV) || string.IsNullOrWhiteSpace(nv.TenNV))
            return Json(new { ok = false, msg = "Họ và tên không được để trống" });
        if (string.IsNullOrWhiteSpace(matKhau)) matKhau = "123456";
        var maNV = await _db.ThemNhanVienAsync(nv, matKhau);
        return Json(new { ok = true, maNV });
    }

    [HttpPost]
    public async Task<IActionResult> SuaNhanVien(NhanVien nv)
    {
        // Dùng chung bởi trang Nhân viên (sửa đầy đủ) và trang Ban Giám Hiệu (chỉ sửa họ tên/email)
        if (!CoQuyen("Admin.NhanVien") && !CoQuyen("Admin.BanGiamHieu")) return Forbid();
        await _db.SuaNhanVienAsync(nv);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> XoaNhanVien(short maNV)
    {
        if (!CoQuyen("Admin.NhanVien")) return Forbid();
        if (maNV == MaNV) return Json(new { ok = false, msg = "Không thể xóa tài khoản đang đăng nhập" });
        try
        {
            await _db.XoaNhanVienAsync(maNV);
        }
        catch (Microsoft.Data.SqlClient.SqlException)
        {
            return Json(new { ok = false, msg = "Không thể xóa vĩnh viễn — nhân viên này đã có dữ liệu liên quan (văn bản, công việc...). Dùng \"Thôi việc/Nghỉ việc\" để ẩn thay vì xóa." });
        }
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> NghiViecNhanVien(short maNV, DateTime? ngayNghiViec, string? lyDo)
    {
        if (!CoQuyen("Admin.NhanVien")) return Forbid();
        if (maNV == MaNV) return Json(new { ok = false, msg = "Không thể đặt thôi việc cho tài khoản đang đăng nhập" });
        await _db.NghiViecNhanVienAsync(maNV, ngayNghiViec ?? DateTime.Today, string.IsNullOrWhiteSpace(lyDo) ? null : lyDo.Trim());
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> KhoiPhucNhanVien(short maNV)
    {
        if (!CoQuyen("Admin.NhanVien")) return Forbid();
        await _db.KhoiPhucNhanVienAsync(maNV);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> DoiMatKhau(short maNV, string matKhauMoi)
    {
        if (!CoQuyen("Admin.NhanVien")) return Forbid();
        if (string.IsNullOrWhiteSpace(matKhauMoi))
            return Json(new { ok = false, msg = "Mật khẩu không được để trống" });
        await _db.DoiMatKhauAdminAsync(maNV, matKhauMoi);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> SetLanhDaoPhong(byte maDV, short? maNV_TDV)
    {
        if (!CoQuyen("Admin.LanhDaoPhong")) return Forbid();
        await _db.SetLanhDaoPhoangAsync(maDV, maNV_TDV == 0 ? null : maNV_TDV);
        return Json(new { ok = true });
    }

    // ── Đơn vị CRUD ────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> ThemDonVi(DonVi dv)
    {
        if (!CoQuyen("Admin.DonVi")) return Forbid();
        if (string.IsNullOrWhiteSpace(dv.TenDV))
            return Json(new { ok = false, msg = "Tên đơn vị không được để trống" });
        await _db.ThemDonViAsync(dv);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> SuaDonVi(DonVi dv)
    {
        if (!CoQuyen("Admin.DonVi")) return Forbid();
        if (string.IsNullOrWhiteSpace(dv.TenDV))
            return Json(new { ok = false, msg = "Tên đơn vị không được để trống" });
        await _db.SuaDonViAsync(dv);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> GiaiTheDonVi(byte maDV)
    {
        if (!CoQuyen("Admin.DonVi")) return Forbid();
        await _db.GiaiTheDonViAsync(maDV);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> KhoiPhucDonVi(byte maDV)
    {
        if (!CoQuyen("Admin.DonVi")) return Forbid();
        await _db.KhoiPhucDonViAsync(maDV);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> GopDonVi(string maDVNguon, byte maDVDich)
    {
        if (!Quyen.Contains(0)) return Forbid(); // chỉ admin mới gộp
        var nguonIds = maDVNguon.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => byte.TryParse(s.Trim(), out var b) ? b : (byte)0)
            .Where(b => b > 0 && b != maDVDich).Distinct().ToArray();
        if (nguonIds.Length == 0) return Json(new { ok = false, msg = "Chưa chọn đơn vị nguồn" });
        await _db.GopDonViAsync(nguonIds, maDVDich);
        return Json(new { ok = true });
    }

    // ── Email phòng ban ─────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> SetEmailDonVi(byte maDV, string? email)
    {
        if (!CoQuyen("Admin.LanhDaoPhong")) return Forbid();
        await _db.SetEmailDonViAsync(maDV, string.IsNullOrWhiteSpace(email) ? null : email.Trim());
        return Json(new { ok = true });
    }

    // ── Cấu hình email ──────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> SaveEmailConfig(EmailConfig cfg)
    {
        if (!Quyen.Contains(0)) return Forbid(); // chỉ admin (QuyenXL=0)
        await _db.SaveEmailConfigAsync(cfg);
        TempData["Success"] = "Đã lưu cấu hình email.";
        return RedirectToAction("CauHinhEmail");
    }

    [HttpPost]
    public async Task<IActionResult> TestEmail(string toEmail)
    {
        if (!Quyen.Contains(0)) return Forbid();
        var (ok, msg) = await _email.TestEmailAsync(toEmail);
        return Json(new { ok, msg });
    }

    // ── Cong tac tinh nang moi ────────────────────────────────────────────
    // Khong gan link o menu — chi Admin (quyen 0) biet route ma vao. Bat cong khai thi TAT CA
    // nguoi dang nhap sau do (hoac dang nhap lai) moi thay; nguoi dang online truoc do phai
    // dang xuat/dang nhap lai vi co cache o session luc dang nhap.
    [HttpGet]
    public async Task<IActionResult> TinhNangMoi()
    {
        if (!Quyen.Contains(0)) return Forbid();
        ViewBag.DangBat = await _db.GetTinhNangMoiCongKhaiAsync();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> TinhNangMoi(bool bat)
    {
        if (!Quyen.Contains(0)) return Forbid();
        await _db.SetTinhNangMoiCongKhaiAsync(bat);
        TempData["Success"] = bat
            ? "Đã công khai tính năng mới cho toàn bộ người dùng."
            : "Đã tắt — chỉ tài khoản Admin còn thấy các tính năng mới.";
        return RedirectToAction("TinhNangMoi");
    }
}
