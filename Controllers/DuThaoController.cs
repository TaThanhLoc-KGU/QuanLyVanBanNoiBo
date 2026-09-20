using Microsoft.AspNetCore.Mvc;
using CongVan.Models;
using CongVan.Services;

namespace CongVan.Controllers;

// VĂN BẢN ĐI (soạn → duyệt → ban hành) — mô phỏng VNPT iOffice của Tỉnh.
//
// Hai PHẠM VI ban hành:
//  1. CẤP TRƯỜNG  — văn thư cấp 1 (văn thư trường) ban hành, cấp số ở sổ văn bản đi. Văn thư cấp 1 soạn thì ban hành NGAY;
//                   người khác (kể cả văn thư cấp 2) trình duyệt/ký NHIỀU CẤP (chuỗi người duyệt tự chọn, mặc định lãnh đạo
//                   đơn vị) rồi chuyển văn thư cấp 1 ban hành.
//  2. NỘI BỘ ĐƠN VỊ — chỉ trong đơn vị: lãnh đạo đơn vị duyệt, văn thư cấp 2 (văn thư đơn vị) ban hành vào "Văn bản nội
//                   bộ đơn vị" (số hiệu riêng của đơn vị). Văn thư cấp 2 soạn thì ban hành NGAY.
// Chuỗi người xử lý dùng lại VanBan_XuLy (LoaiVB=2, MSCV = ID dự thảo) nên hiện chung ở "Văn bản của tôi".
public class DuThaoController : BaseController
{
    private readonly DbService _db;
    private readonly FileService _fileSvc;
    private const byte Loai = VanBanXuLy.LoaiDuThao;

    public DuThaoController(DbService db, FileService fileSvc) { _db = db; _fileSvc = fileSvc; }

    // Văn thư cấp 1 = văn thư trường (có quyền vào sổ văn bản đi). Văn thư cấp 2 = văn thư của đơn vị soạn thảo.
    private bool LaVanThuCap1 => CoQuyen("CongVanDi.Nhap");
    private bool LaVanThuCap2Cua(byte maDV) => LaVanThuDonVi && MaDV == maDV;

    public async Task<IActionResult> Index(string? tuKhoa)
    {
        ViewBag.TuKhoa = tuKhoa;
        ViewData["DemChoDi"] = await _db.DemChoTheoLoaiAsync(MaNV, Loai);
        return View(await _db.GetDuThaoCuaToiAsync(MaNV, tuKhoa));
    }

    // ── Soạn / sửa ─────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Nhap(int? id, string? traLoi)
    {
        DuThao d;
        if (id.HasValue)
        {
            d = await _db.GetDuThaoAsync(id.Value) ?? new DuThao();
            if (d.ID == 0) return NotFound();
            if (!await CoTheSuaAsync(d)) return Forbid();
        }
        else
        {
            d = new DuThao { MaDVSoan = MaDV, MaNVSoan = MaNV, HanXuLy = null, MSCVDen = traLoi, PhamVi = 1 };
            // Soạn để trả lời 1 văn bản đến: gợi ý sẵn "V/v trả lời ..." — chỉ khi người soạn xem được văn bản đó.
            if (!string.IsNullOrWhiteSpace(traLoi))
            {
                var den = await _db.GetCongVanDenByIdAsync(traLoi.Trim());
                bool xemDuoc = den != null && (den.DungChung || CoQuyen("CongVanDen.Nhap") || LaVanThuDonVi || LaLanhDaoDonVi
                    || await _db.CoLienQuanVanBanAsync(VanBanXuLy.LoaiDen, traLoi.Trim(), MaNV));
                if (den != null && xemDuoc) d.TrichYeu = "V/v trả lời " + den.TrichYeu;
                else d.MSCVDen = null;
            }
            // Mặc định chuỗi duyệt: lãnh đạo đơn vị (nếu không phải chính người soạn)
            var ld = await _db.GetLanhDaoDonViAsync(MaDV);
            if (ld != null && ld.MaNV != MaNV) d.ChuoiDuyet = ld.MaNV.ToString();
        }
        return View(await TaoFormAsync(d));
    }

    [HttpPost]
    public async Task<IActionResult> Nhap(DuThaoFormViewModel vm, List<byte> dvNhan, List<IFormFile> files, string? hanhDong, string? chuoiDuyetCsv)
    {
        var d = vm.DuThao;
        bool moi = d.ID == 0;
        if (!moi)
        {
            var goc = await _db.GetDuThaoAsync(d.ID);
            if (goc == null) return NotFound();
            if (!await CoTheSuaAsync(goc)) return Forbid();
            d.MaDVSoan = goc.MaDVSoan; d.MaNVSoan = goc.MaNVSoan;
        }
        else { d.MaDVSoan = MaDV; d.MaNVSoan = MaNV; }

        d.PhamVi = d.PhamVi == 2 ? (byte)2 : (byte)1;
        // Chuỗi duyệt: chỉ nhận người thuộc danh sách hợp lệ (lãnh đạo đơn vị + lãnh đạo trường), giữ đúng thứ tự đã chọn.
        var hopLe = (await _db.GetNguoiDuyetTrinhKyAsync(d.MaDVSoan, d.MaNVSoan)).Select(n => n.MaNV).Where(m => m != d.MaNVSoan).ToHashSet();
        var chuoi = (chuoiDuyetCsv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => short.TryParse(x, out _)).Select(short.Parse).Where(hopLe.Contains).Distinct().ToList();
        if (d.PhamVi == 2)
        {
            var ld = await _db.GetLanhDaoDonViAsync(d.MaDVSoan);
            chuoi = ld != null && ld.MaNV != d.MaNVSoan ? new List<short> { ld.MaNV } : new List<short>(); // nội bộ: chỉ lãnh đạo đơn vị
        }
        d.ChuoiDuyet = chuoi.Count > 0 ? string.Join(",", chuoi) : null;

        if (string.IsNullOrWhiteSpace(d.TrichYeu) || (d.MaLVB ?? 0) == 0)
        {
            TempData["Error"] = string.IsNullOrWhiteSpace(d.TrichYeu) ? "Trích yếu không được để trống." : "Vui lòng chọn Loại văn bản.";
            vm = await TaoFormAsync(d);
            vm.DVNhan = dvNhan;
            return View(vm);
        }

        d.TrichYeu = d.TrichYeu.Trim();
        d.DonViNhan = dvNhan.Count > 0 ? string.Join(",", dvNhan.Distinct()) : null;
        d.NoiNhanKhac = string.IsNullOrWhiteSpace(d.NoiNhanKhac) ? null : d.NoiNhanKhac.Trim();
        d.NoiDungXuLy = string.IsNullOrWhiteSpace(d.NoiDungXuLy) ? null : d.NoiDungXuLy.Trim();
        if (d.MaNCV == 0) d.MaNCV = null;
        if (d.MaNVKy == 0) d.MaNVKy = null;
        if (string.IsNullOrWhiteSpace(d.MSCVDen)) d.MSCVDen = null;

        d.ID = await _db.LuuDuThaoAsync(d);
        foreach (var f in files.Where(f => f.Length > 0))
        {
            var rel = await _fileSvc.SaveAsync(f, "duthao");
            await _db.ThemFileDuThaoAsync(d.ID, f.FileName, rel);
        }

        if (hanhDong == "trinh") return await TrinhAsync(d.ID);
        if (hanhDong == "banhanh")
        {
            // Văn thư đúng cấp (cấp 1 với văn bản cấp trường, cấp 2 với văn bản nội bộ đơn vị) ban hành ngay, không cần duyệt.
            bool duocBanHanhNgay = d.PhamVi == 2 ? LaVanThuCap2Cua(d.MaDVSoan) : LaVanThuCap1;
            if (duocBanHanhNgay)
                return d.PhamVi == 2
                    ? RedirectToAction("Nhap", "VanBanNoiBo", new { duThao = d.ID })
                    : RedirectToAction("Nhap", "CongVanDi", new { duThao = d.ID });
            TempData["Error"] = "Bạn không phải văn thư của cấp ban hành này nên không ban hành ngay được — hãy trình duyệt.";
            return RedirectToAction("ChiTiet", new { id = d.ID });
        }
        TempData["Success"] = moi ? "Đã lưu nháp." : "Đã cập nhật.";
        return RedirectToAction("ChiTiet", new { id = d.ID });
    }

    // ── Chi tiết + luồng ───────────────────────────────────────────────
    public async Task<IActionResult> ChiTiet(int id)
    {
        var d = await _db.GetDuThaoAsync(id);
        if (d == null) return NotFound();
        var mscv = id.ToString();
        var tatCa = await _db.GetXuLyCuaVanBanAsync(Loai, mscv);
        bool lienQuan = tatCa.Any(x => x.MaNVNhan == MaNV && x.TrangThai != VanBanXuLy.BiThuHoi);
        if (!lienQuan && !await CoTheXemAsync(d)) return Forbid();

        var dong = await _db.GetDongDangChoAsync(Loai, mscv, MaNV);
        if (dong != null && dong.NgayXem == null) { await _db.MoXemXuLyAsync(dong.ID, MaNV); dong = await _db.GetDongDangChoAsync(Loai, mscv, MaNV); }

        var vanThuCuoi = await VanThuCuoiAsync(d, null);
        var holders = tatCa.Where(x => x.VaiTro == VanBanXuLy.VaiTroChinh && x.DangCho).ToList();
        bool laSoan = d.MaNVSoan == MaNV;
        bool dangGiu = dong is { VaiTro: VanBanXuLy.VaiTroChinh };
        bool laVanThuPhuHop = d.PhamVi == 2 ? LaVanThuCap2Cua(d.MaDVSoan) : LaVanThuCap1;
        bool chuaXong = d.TrangThai != DuThao.DaBanHanh;

        string buoc = !chuaXong ? "xong"
            : holders.Count == 0 || holders.All(h => h.MaNVNhan == d.MaNVSoan) ? "soan"
            : holders.Any(h => vanThuCuoi.Contains(h.MaNVNhan)) ? "banhanh" : "duyet";

        var tatCaNv = await _db.GetAllNhanVienAsync(baoGomNghiViec: true);
        string Ten(short m) => tatCaNv.FirstOrDefault(n => n.MaNV == m)?.HoTen ?? ("NV#" + m);
        var donVi = await _db.GetDonViAsync(chiLayConHoatDong: false);
        var ld = await _db.GetLanhDaoDonViAsync(d.MaDVSoan);

        var vm = new DuThaoChiTietViewModel
        {
            DuThao = d, Files = await _db.GetFileDuThaoAsync(id), LaNguoiSoan = laSoan, BuocHienTai = buoc,
            TenLanhDaoDonVi = ld?.HoTen ?? "",
            CoTheTrinh = laSoan && chuaXong && buoc == "soan",
            // Văn thư đúng cấp soạn thì ban hành NGAY, không cần qua các bước duyệt.
            CoTheBanHanhNgay = laSoan && chuaXong && buoc == "soan" && laVanThuPhuHop,
            CoTheBanHanh = chuaXong && dangGiu && laVanThuPhuHop && !laSoan,
            CoTheDuyet = chuaXong && dangGiu && buoc == "duyet",
            LinkBanHanhController = d.PhamVi == 2 ? "VanBanNoiBo" : "CongVanDi",
            TenVanThu = d.PhamVi == 2 ? "Văn thư đơn vị" : "Văn thư trường",
            CapVanThu = d.PhamVi == 2 ? "cấp 2" : "cấp 1",
            TenChuoiConLai = d.ChuoiDuyetIds.Select(Ten).ToList(),
            TenNguoiDangGiu = string.Join(", ", holders.Select(h => h.TenNVNhan)),
            TenDVNhan = (d.DonViNhan ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => byte.TryParse(x, out var b) ? donVi.FirstOrDefault(v => v.MaDV == b)?.TenDV : null)
                .Where(x => x != null).Select(x => x!).ToList(),
            Panel = new XuLyPanelViewModel
            {
                LoaiVB = Loai, MSCV = mscv, MaNV = MaNV, DongCuaToi = dong, TatCa = tatCa,
                NhatKy = await _db.GetNhatKyVanBanAsync(Loai, mscv), CoTheChuyenGoc = false,
                NhanVien = dong is { VaiTro: not VanBanXuLy.VaiTroXemBiet } ? await _db.GetAllNhanVienAsync() : new List<NhanVien>()
            }
        };
        return View(vm);
    }

    // ── Trình / duyệt ──────────────────────────────────────────────────
    [HttpPost]
    public Task<IActionResult> Trinh(int id) => TrinhAsync(id);

    private async Task<IActionResult> TrinhAsync(int id)
    {
        var d = await _db.GetDuThaoAsync(id);
        if (d == null) return NotFound();
        if (d.MaNVSoan != MaNV || d.TrangThai == DuThao.DaBanHanh) return Forbid();

        var dongSoan = await _db.LayHoacTaoDongNguoiSoanAsync(id, MaNV);
        var (ok, msg) = await DiTiepAsync(d, dongSoan, "Trình duyệt");
        TempData[ok ? "Success" : "Error"] = msg;
        if (ok) await _db.DatTrangThaiDuThaoAsync(id, DuThao.DangXuLy);
        return RedirectToAction("ChiTiet", new { id });
    }

    // Người duyệt hiện tại bấm "Duyệt": chuyển người duyệt kế tiếp trong chuỗi; hết chuỗi thì tới văn thư ban hành.
    [HttpPost]
    public async Task<IActionResult> Duyet(int id, string? noiDung)
    {
        var d = await _db.GetDuThaoAsync(id);
        var dong = await _db.GetDongDangChoAsync(Loai, id.ToString(), MaNV);
        if (d == null || dong is not { VaiTro: VanBanXuLy.VaiTroChinh }) return Forbid();
        var (ok, msg) = await DiTiepAsync(d, dong.ID,
            string.IsNullOrWhiteSpace(noiDung) ? "Đã duyệt" : "Đã duyệt: " + noiDung.Trim());
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction("ChiTiet", new { id });
    }

    // Chuyển bước kế tiếp. ChuoiDuyet luôn là CHUỖI ĐẦY ĐỦ (không bao gồm người soạn): người soạn trình → người đầu chuỗi;
    // người duyệt thứ i duyệt → người thứ i+1; hết chuỗi → văn thư của cấp ban hành. Nhờ vậy khi bị trả lại rồi duyệt lại,
    // chuỗi không bị lệch. Phạm vi nội bộ: chuỗi chỉ gồm lãnh đạo đơn vị.
    private async Task<(bool Ok, string Msg)> DiTiepAsync(DuThao d, int idDongCuaToi, string ghiChu)
    {
        var chuoi = d.ChuoiDuyetIds;
        int idx = chuoi.IndexOf(MaNV);
        short? ke = d.MaNVSoan == MaNV ? (chuoi.Count > 0 ? chuoi[0] : null)
                  : idx >= 0 && idx + 1 < chuoi.Count ? chuoi[idx + 1] : null;

        List<NguoiNhanXuLy> nhan;
        string ai;
        if (ke.HasValue)
        {
            var ten = (await _db.GetAllNhanVienAsync()).FirstOrDefault(n => n.MaNV == ke.Value)?.HoTen ?? "";
            nhan = new() { new NguoiNhanXuLy { MaNV = ke.Value, VaiTro = VanBanXuLy.VaiTroChinh } };
            int conLai = chuoi.Count - (chuoi.IndexOf(ke.Value) + 1);
            ai = $"Đã chuyển {ten} duyệt" + (conLai > 0 ? $" (còn {conLai} cấp sau)" : "");
        }
        else
        {
            var vt = await VanThuCuoiAsync(d, MaNV);
            if (vt.Count == 0)
                return (false, d.PhamVi == 2
                    ? "Đơn vị chưa có văn thư cấp 2 để ban hành nội bộ — nhờ Admin thêm văn thư đơn vị, hoặc dùng Chuyển tiếp để chọn người nhận."
                    : "Chưa có văn thư cấp 1 (văn thư trường) để ban hành — nhờ Admin cấp quyền, hoặc dùng Chuyển tiếp để chọn người nhận.");
            nhan = vt.Select(m => new NguoiNhanXuLy { MaNV = m, VaiTro = VanBanXuLy.VaiTroChinh }).ToList();
            ai = d.PhamVi == 2 ? "Đã chuyển văn thư đơn vị ban hành" : "Đã chuyển văn thư trường ban hành";
        }

        var so = await _db.ChuyenXuLyAsync(Loai, d.ID.ToString(), MaNV, nhan, ghiChu, d.HanXuLy, idDongCuaToi);
        return so > 0 ? (true, ai + ".") : (false, "Không chuyển được — người nhận đang giữ văn bản rồi.");
    }

    // Văn thư nhận bước "ban hành": cấp 1 (phạm vi cấp trường) hoặc cấp 2 của đơn vị soạn (phạm vi nội bộ).
    private async Task<List<short>> VanThuCuoiAsync(DuThao d, short? loaiTru)
    {
        var ds = d.PhamVi == 2 ? await _db.GetVanThuDonViAsync(d.MaDVSoan) : await _db.GetVanThuCap1Async();
        return ds.Where(m => m != loaiTru).Distinct().ToList();
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(int id)
    {
        var d = await _db.GetDuThaoAsync(id);
        if (d == null) return NotFound();
        if (d.MaNVSoan != MaNV || d.TrangThai != DuThao.Nhap) return Forbid();
        foreach (var f in await _db.GetFileDuThaoAsync(id)) _fileSvc.Delete(f.DuongDan);
        await _db.XoaDuThaoAsync(id);
        TempData["Success"] = "Đã xóa.";
        return RedirectToAction("Index");
    }

    // ── File đính kèm ──────────────────────────────────────────────────
    public async Task<IActionResult> TaiFile(int fileId)
    {
        var f = await _db.GetFileDuThaoByIdAsync(fileId);
        if (f == null) return NotFound();
        var d = await _db.GetDuThaoAsync(f.MaDT);
        if (d == null) return NotFound();
        if (!await _db.CoLienQuanVanBanAsync(Loai, d.ID.ToString(), MaNV) && !await CoTheXemAsync(d)) return Forbid();
        if (!_fileSvc.Exists(f.DuongDan)) return NotFound();
        return PhysicalFile(_fileSvc.GetFullPath(f.DuongDan), FileService.GetMimeType(f.TenFile), f.TenFile);
    }

    [HttpPost]
    public async Task<IActionResult> XoaFile(int fileId)
    {
        var f = await _db.GetFileDuThaoByIdAsync(fileId);
        if (f == null) return NotFound();
        var d = await _db.GetDuThaoAsync(f.MaDT);
        if (d == null || !await CoTheSuaAsync(d)) return Forbid();
        _fileSvc.Delete(f.DuongDan);
        await _db.XoaFileDuThaoAsync(fileId);
        return RedirectToAction("Nhap", new { id = d.ID });
    }

    // ── Quyền ──────────────────────────────────────────────────────────
    // Sửa: người soạn khi còn nháp, hoặc khi đang nằm trong hộp thư của chính họ (bị trả lại).
    private async Task<bool> CoTheSuaAsync(DuThao d)
    {
        if (d.TrangThai == DuThao.DaBanHanh || d.MaNVSoan != MaNV) return false;
        if (d.TrangThai == DuThao.Nhap) return true;
        return await _db.GetDongDangChoAsync(Loai, d.ID.ToString(), MaNV) != null;
    }

    // Xem khi không nằm trong chuỗi xử lý: người soạn, lãnh đạo/văn thư đơn vị soạn, văn thư có quyền, Admin.
    private async Task<bool> CoTheXemAsync(DuThao d) =>
        d.MaNVSoan == MaNV || CoQuyen("CongVanDi.Nhap") || CoQuyen("Global.XemToanTruong")
        || await _db.LaLanhDaoDonViAsync(MaNV, d.MaDVSoan) || await _db.LaVanThuDonViAsync(MaNV, d.MaDVSoan);

    private async Task<DuThaoFormViewModel> TaoFormAsync(DuThao d)
    {
        var donVi = await _db.GetDonViAsync();
        var tenDV = (await _db.GetDonViAsync(chiLayConHoatDong: false)).FirstOrDefault(x => x.MaDV == d.MaDVSoan)?.TenDV ?? "";
        return new DuThaoFormViewModel
        {
            DuThao = d,
            DanhSachLoaiVB = await _db.GetLoaiVBAsync(chiHienThi: true),
            DanhSachNhomCV = await _db.GetNhomCVAsync(),
            DanhSachDonVi = donVi,
            DanhSachNguoiKy = await _db.GetLanhDaoAsync(),
            DanhSachNguoiDuyet = (await _db.GetNguoiDuyetTrinhKyAsync(d.MaDVSoan, d.MaNVSoan)).Where(n => n.MaNV != d.MaNVSoan).ToList(),
            ChuoiDuyet = d.ChuoiDuyetIds,
            LaVanThuCap1 = LaVanThuCap1,
            LaVanThuCap2 = LaVanThuCap2Cua(d.MaDVSoan),
            Files = d.ID > 0 ? await _db.GetFileDuThaoAsync(d.ID) : new(),
            DVNhan = (d.DonViNhan ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Where(x => byte.TryParse(x, out _)).Select(byte.Parse).ToList(),
            TenDVSoan = tenDV
        };
    }
}
