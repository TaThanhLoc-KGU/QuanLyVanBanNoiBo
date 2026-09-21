using Microsoft.AspNetCore.Mvc;
using CongVan.Models;
using CongVan.Services;

namespace CongVan.Controllers;

// VĂN BẢN ĐI (soạn → duyệt → ký → ban hành) — mô phỏng VNPT iOffice của Tỉnh.
//
// Hai PHẠM VI ban hành, mỗi phạm vi có CHUỖI BƯỚC riêng (lưu ở DuThao.ChuoiDuyet, bước hiện tại ở BuocIdx):
//  1. CẤP TRƯỜNG   : Người soạn → Lãnh đạo đơn vị duyệt → Văn thư (cấp 1) kiểm tra thể thức → Lãnh đạo trường KÝ
//                    → Văn thư cấp 1 ban hành (vào sổ văn bản đi, cấp số). "Người ký dự kiến" = lãnh đạo trường.
//  2. NỘI BỘ ĐƠN VỊ: Người soạn → Lãnh đạo đơn vị duyệt/KÝ → Văn thư cấp 2 ban hành (vào "Văn bản nội bộ đơn vị").
//                    "Người ký" = lãnh đạo đơn vị.
// Bước nào người soạn chính là người của bước đó (vd soạn bởi lãnh đạo đơn vị, hoặc văn thư cấp 1) thì bỏ qua bước đó.
// Văn thư đúng cấp soạn thì được "ban hành ngay". Mọi bước có: xem file, ý kiến, duyệt/ký (kèm file đã ký), TRẢ LẠI (lùi 1 bước).
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
        }
        return View(await TaoFormAsync(d));
    }

    [HttpPost]
    public async Task<IActionResult> Nhap(DuThaoFormViewModel vm, List<byte> dvNhan, List<IFormFile> files, string? hanhDong)
    {
        var d = vm.DuThao;
        bool moi = d.ID == 0;
        DuThao? goc = null;
        if (!moi)
        {
            goc = await _db.GetDuThaoAsync(d.ID);
            if (goc == null) return NotFound();
            if (!await CoTheSuaAsync(goc)) return Forbid();
            d.MaDVSoan = goc.MaDVSoan; d.MaNVSoan = goc.MaNVSoan;
        }
        else { d.MaDVSoan = MaDV; d.MaNVSoan = MaNV; }

        bool laTacGia = d.MaNVSoan == MaNV;
        if (goc is { DaKhoaCauHinh: true })
        {
            // Đã trình: KHÔNG đổi phạm vi / người ký / chuỗi duyệt (tránh phá luồng đang chạy) — chỉ sửa nội dung, file, nơi nhận.
            d.PhamVi = goc.PhamVi; d.MaNVKy = goc.MaNVKy; d.ChuoiDuyet = goc.ChuoiDuyet;
            hanhDong = "luu";
        }
        else
        {
            d.PhamVi = d.PhamVi == 2 ? (byte)2 : (byte)1;
            var ld = await _db.GetLanhDaoDonViAsync(d.MaDVSoan);
            if (d.PhamVi == 2)
                d.MaNVKy = ld != null && ld.MaNV != d.MaNVSoan ? ld.MaNV : (short?)null;   // nội bộ: người ký = lãnh đạo đơn vị
            else
            {
                var ky = (await _db.GetLanhDaoAsync()).Select(n => n.MaNV).ToHashSet();    // cấp trường: người ký = lãnh đạo trường
                if (d.MaNVKy is null or 0 || !ky.Contains(d.MaNVKy.Value)) d.MaNVKy = null;
            }
            d.ChuoiDuyet = string.Join(",", await XayChuoiAsync(d));
            if (!laTacGia) hanhDong = "luu";
        }

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
        if (string.IsNullOrWhiteSpace(d.ChuoiDuyet)) d.ChuoiDuyet = null;
        if (string.IsNullOrWhiteSpace(d.MSCVDen)) d.MSCVDen = null;

        d.ID = await _db.LuuDuThaoAsync(d);
        foreach (var f in files.Where(f => f.Length > 0))
        {
            var rel = await _fileSvc.SaveAsync(f, "duthao");
            await _db.ThemFileDuThaoAsync(d.ID, f.FileName, rel);
        }
        if (!moi) await _db.GhiNhatKyDuThaoAsync(d.ID, MaNV, "ChinhSua", laTacGia ? "Người soạn chỉnh sửa nội dung" : "Người xử lý chỉnh sửa nội dung");

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
        TempData["Success"] = moi ? "Đã lưu nháp." : "Đã lưu chỉnh sửa.";
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

        var chuoi = d.ChuoiBuoc;
        int k = d.BuocIdx, n = chuoi.Count;
        bool chuaXong = d.TrangThai != DuThao.DaBanHanh;
        bool laSoan = d.MaNVSoan == MaNV;
        bool dangGiu = dong is { VaiTro: VanBanXuLy.VaiTroChinh };
        bool laVanThuPhuHop = d.PhamVi == 2 ? LaVanThuCap2Cua(d.MaDVSoan) : LaVanThuCap1;
        bool dangDuyet = chuaXong && k >= 0 && k < n;
        bool dangBanHanh = chuaXong && k >= n && k >= 0;

        string buoc = !chuaXong ? "xong" : k < 0 ? "soan" : dangDuyet ? "duyet" : "banhanh";

        var tatCaNv = await _db.GetAllNhanVienAsync(baoGomNghiViec: true);
        string Ten(string tok) => tok == DuThao.BuocVanThu1 ? "Văn thư trường"
            : short.TryParse(tok, out var m) ? (tatCaNv.FirstOrDefault(x => x.MaNV == m)?.HoTen ?? "NV#" + m) : tok;
        var donVi = await _db.GetDonViAsync(chiLayConHoatDong: false);
        var ld = await _db.GetLanhDaoDonViAsync(d.MaDVSoan);

        // Sơ đồ luồng theo đúng chuỗi bước của văn bản này
        var flow = new List<FlowNode> { new() { Ten = "Người soạn", Phu = d.TenNVSoan ?? "", Icon = "pencil", Trang = !chuaXong || k >= 0 ? "done" : "on" } };
        for (int i = 0; i < n; i++)
        {
            var tok = chuoi[i];
            bool laLDDV = ld != null && tok == ld.MaNV.ToString();
            string tenBuoc = tok == DuThao.BuocVanThu1 ? "Văn thư trường kiểm tra thể thức"
                : laLDDV ? (d.PhamVi == 2 ? "Lãnh đạo đơn vị duyệt / ký" : "Lãnh đạo đơn vị duyệt")
                : "Lãnh đạo trường ký";
            flow.Add(new FlowNode
            {
                Ten = tenBuoc, Phu = Ten(tok), Icon = tok == DuThao.BuocVanThu1 ? "journal-check" : "person-check",
                Trang = !chuaXong || i < k ? "done" : i == k ? "on" : ""
            });
        }
        flow.Add(new FlowNode
        {
            Ten = d.PhamVi == 2 ? "Văn thư đơn vị ban hành" : "Văn thư trường ban hành", Icon = "journal-check",
            Phu = d.PhamVi == 2 ? "Vào sổ nội bộ đơn vị" : "Vào sổ, cấp số ký hiệu", Trang = !chuaXong ? "done" : dangBanHanh ? "on" : ""
        });
        flow.Add(new FlowNode
        {
            Ten = "Đã ban hành", Icon = "check-circle", Trang = !chuaXong ? "done" : "",
            Phu = !string.IsNullOrEmpty(d.MSCVDi) ? "Số " + d.MSCVDi.Trim() : d.MaVBNoiBo.HasValue ? "Nội bộ #" + d.MaVBNoiBo : "—"
        });

        string tokHienTai = dangDuyet ? chuoi[k] : "";
        bool laBuocKy = dangDuyet && tokHienTai != DuThao.BuocVanThu1 && !(ld != null && tokHienTai == ld.MaNV.ToString() && d.PhamVi == 1);
        var holders = tatCa.Where(x => x.VaiTro == VanBanXuLy.VaiTroChinh && x.DangCho).ToList();

        var vm = new DuThaoChiTietViewModel
        {
            DuThao = d, Files = await _db.GetFileDuThaoAsync(id), LaNguoiSoan = laSoan, BuocHienTai = buoc,
            TenLanhDaoDonVi = ld?.HoTen ?? "", Flow = flow,
            CoTheTrinh = laSoan && chuaXong && k < 0,
            CoTheBanHanhNgay = laSoan && chuaXong && k < 0 && laVanThuPhuHop,
            CoTheDuyet = dangDuyet && dangGiu,
            CoTheBanHanh = dangBanHanh && dangGiu && laVanThuPhuHop,
            CoTheTraLai = chuaXong && dangGiu && k >= 0 && !laSoan,
            CoTheSua = chuaXong && (laSoan || dangGiu || CoQuyen("Admin.NhanVien")),
            NhanNutDuyet = tokHienTai == DuThao.BuocVanThu1 ? "Đã kiểm tra — chuyển lãnh đạo ký"
                : laBuocKy ? "Ký duyệt" : "Duyệt & chuyển tiếp",
            MoTaBuocHienTai = tokHienTai == DuThao.BuocVanThu1 ? "Kiểm tra thể thức, số ký hiệu, nơi nhận rồi chuyển lãnh đạo trường ký."
                : laBuocKy ? "Xem xét nội dung và file, cho ý kiến rồi ký duyệt (có thể tải lên file đã ký)."
                : "Xem xét nội dung và file, cho ý kiến rồi duyệt để chuyển bước tiếp theo.",
            LinkBanHanhController = d.PhamVi == 2 ? "VanBanNoiBo" : "CongVanDi",
            TenVanThu = d.PhamVi == 2 ? "Văn thư đơn vị" : "Văn thư trường",
            CapVanThu = d.PhamVi == 2 ? "cấp 2" : "cấp 1",
            TenChuoiConLai = chuoi.Select(Ten).ToList(),
            TenNguoiDangGiu = string.Join(", ", holders.Select(h => h.TenNVNhan)),
            TenDVNhan = (d.DonViNhan ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => byte.TryParse(x, out var b) ? donVi.FirstOrDefault(v => v.MaDV == b)?.TenDV : null)
                .Where(x => x != null).Select(x => x!).ToList(),
            Panel = new XuLyPanelViewModel
            {
                LoaiVB = Loai, MSCV = mscv, MaNV = MaNV, DongCuaToi = dong, TatCa = tatCa, AnNut = true,
                NhatKy = await _db.GetNhatKyVanBanAsync(Loai, mscv), CoTheChuyenGoc = false
            }
        };
        return View(vm);
    }

    // ── Trình / duyệt / trả lại ────────────────────────────────────────
    [HttpPost]
    public Task<IActionResult> Trinh(int id) => TrinhAsync(id);

    private async Task<IActionResult> TrinhAsync(int id)
    {
        var d = await _db.GetDuThaoAsync(id);
        if (d == null) return NotFound();
        if (d.MaNVSoan != MaNV || d.TrangThai == DuThao.DaBanHanh || d.BuocIdx >= 0) return Forbid();

        // Dựng lại chuỗi theo cấu hình hiện tại (lãnh đạo đơn vị có thể đã đổi từ lúc lưu nháp)
        d.ChuoiDuyet = string.Join(",", await XayChuoiAsync(d));
        await _db.DatChuoiDuyetAsync(id, string.IsNullOrEmpty(d.ChuoiDuyet) ? null : d.ChuoiDuyet);

        var dongSoan = await _db.LayHoacTaoDongNguoiSoanAsync(id, MaNV);
        var (ok, msg) = await DiTiepAsync(d, dongSoan, "Trình duyệt");
        TempData[ok ? "Success" : "Error"] = msg;
        if (ok) await _db.DatTrangThaiDuThaoAsync(id, DuThao.DangXuLy);
        return RedirectToAction("ChiTiet", new { id });
    }

    // Người đang xử lý bấm "Duyệt / Ký": kèm ý kiến và (tuỳ chọn) file đã ký; chuyển sang bước kế tiếp.
    [HttpPost]
    public async Task<IActionResult> Duyet(int id, string? noiDung, IFormFile? fileKy)
    {
        var d = await _db.GetDuThaoAsync(id);
        var dong = await _db.GetDongDangChoAsync(Loai, id.ToString(), MaNV);
        if (d == null || dong is not { VaiTro: VanBanXuLy.VaiTroChinh } || d.BuocIdx < 0 || d.BuocIdx >= d.ChuoiBuoc.Count) return Forbid();

        if (fileKy != null && fileKy.Length > 0)
        {
            var rel = await _fileSvc.SaveAsync(fileKy, "duthao");
            await _db.ThemFileDuThaoAsync(id, "[Đã ký] " + fileKy.FileName, rel);
        }
        var (ok, msg) = await DiTiepAsync(d, dong.ID, string.IsNullOrWhiteSpace(noiDung) ? "Đã duyệt" : "Đã duyệt: " + noiDung.Trim());
        TempData[ok ? "Success" : "Error"] = msg;
        return RedirectToAction("ChiTiet", new { id });
    }

    // Trả lại bước liền trước (kèm lý do) — người soạn/người bước trước sửa rồi trình lại.
    [HttpPost]
    public async Task<IActionResult> TraLai(int id, string? lyDo)
    {
        var d = await _db.GetDuThaoAsync(id);
        var dong = await _db.GetDongDangChoAsync(Loai, id.ToString(), MaNV);
        if (d == null || dong is not { VaiTro: VanBanXuLy.VaiTroChinh } || d.MaNVSoan == MaNV) return Forbid();
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            TempData["Error"] = "Vui lòng nhập lý do trả lại để người soạn biết cần sửa gì.";
            return RedirectToAction("ChiTiet", new { id });
        }
        TempData[await _db.TraLaiXuLyAsync(dong.ID, MaNV, lyDo.Trim()) ? "Success" : "Error"] = "Đã trả lại — người ở bước trước sẽ nhận lại trong hộp thư để chỉnh sửa và trình lại.";
        return RedirectToAction("ChiTiet", new { id });
    }

    // Chuyển bước kế tiếp trong chuỗi (BuocIdx+1): người cụ thể, nhóm văn thư cấp 1 ("VT1"), hoặc — hết chuỗi — văn thư ban hành.
    private async Task<(bool Ok, string Msg)> DiTiepAsync(DuThao d, int idDongCuaToi, string ghiChu)
    {
        var chuoi = d.ChuoiBuoc;
        int next = d.BuocIdx + 1;

        List<short> nguoiNhan;
        string ai;
        var ten = await _db.GetAllNhanVienAsync(baoGomNghiViec: true);
        if (next < chuoi.Count)
        {
            var tok = chuoi[next];
            if (tok == DuThao.BuocVanThu1)
            {
                nguoiNhan = (await _db.GetVanThuCap1Async()).Where(m => m != MaNV).ToList();
                if (nguoiNhan.Count == 0) return (false, "Chưa có văn thư cấp 1 (văn thư trường) để kiểm tra thể thức — nhờ Admin cấp quyền văn thư trường.");
                ai = "Đã chuyển văn thư trường kiểm tra thể thức";
            }
            else
            {
                if (!short.TryParse(tok, out var m)) return (false, "Chuỗi duyệt không hợp lệ — hãy mở Sửa và lưu lại.");
                nguoiNhan = new List<short> { m };
                ai = $"Đã chuyển {ten.FirstOrDefault(x => x.MaNV == m)?.HoTen} " + (next == chuoi.Count - 1 && d.PhamVi == 1 ? "ký" : "duyệt");
            }
        }
        else
        {
            nguoiNhan = await VanThuCuoiAsync(d);
            if (nguoiNhan.Count == 0)
                return (false, d.PhamVi == 2
                    ? "Đơn vị chưa có văn thư cấp 2 để ban hành nội bộ — nhờ Admin thêm văn thư đơn vị."
                    : "Chưa có văn thư cấp 1 (văn thư trường) để ban hành — nhờ Admin cấp quyền văn thư trường.");
            ai = d.PhamVi == 2 ? "Đã chuyển văn thư đơn vị ban hành" : "Đã chuyển văn thư trường ban hành";
        }

        await _db.DongCungBuocAsync(d.ID, idDongCuaToi);
        var so = await _db.ChuyenXuLyAsync(Loai, d.ID.ToString(), MaNV,
            nguoiNhan.Select(m => new NguoiNhanXuLy { MaNV = m, VaiTro = VanBanXuLy.VaiTroChinh }).ToList(), ghiChu, d.HanXuLy, idDongCuaToi);
        if (so == 0) return (false, "Không chuyển được — người nhận không hợp lệ hoặc đang giữ văn bản rồi.");
        await _db.DatBuocDuThaoAsync(d.ID, next);
        return (true, ai + ".");
    }

    // Xây chuỗi bước theo phạm vi; bỏ bước nào mà chính người soạn đã là người của bước đó.
    private async Task<List<string>> XayChuoiAsync(DuThao d)
    {
        var chuoi = new List<string>();
        var ld = await _db.GetLanhDaoDonViAsync(d.MaDVSoan);
        bool tacGiaLaLD = ld != null && ld.MaNV == d.MaNVSoan;
        bool tacGiaLaVT1 = await _db.LaVanThuCap1Async(d.MaNVSoan);
        if (ld != null && !tacGiaLaLD) chuoi.Add(ld.MaNV.ToString());
        if (d.PhamVi == 1)
        {
            if (!tacGiaLaVT1) chuoi.Add(DuThao.BuocVanThu1);
            if (d.MaNVKy.HasValue && d.MaNVKy != d.MaNVSoan && !chuoi.Contains(d.MaNVKy.Value.ToString())) chuoi.Add(d.MaNVKy.Value.ToString());
        }
        return chuoi;
    }

    // Văn thư nhận bước "ban hành": cấp 1 (phạm vi cấp trường) hoặc cấp 2 của đơn vị soạn (phạm vi nội bộ).
    private async Task<List<short>> VanThuCuoiAsync(DuThao d)
        => (d.PhamVi == 2 ? await _db.GetVanThuDonViAsync(d.MaDVSoan) : await _db.GetVanThuCap1Async()).Distinct().ToList();

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
    // SỬA được khi chưa ban hành: người soạn (bất kỳ lúc nào), người đang giữ văn bản ở bước hiện tại (sửa câu chữ trước khi duyệt),
    // hoặc Admin. Đã trình thì phạm vi / người ký / chuỗi duyệt bị khóa (xem Nhap POST).
    private async Task<bool> CoTheSuaAsync(DuThao d)
    {
        if (d.TrangThai == DuThao.DaBanHanh) return false;
        if (d.MaNVSoan == MaNV || CoQuyen("Admin.NhanVien")) return true;
        return await _db.GetDongDangChoAsync(Loai, d.ID.ToString(), MaNV) is { VaiTro: VanBanXuLy.VaiTroChinh };
    }

    // Xem khi không nằm trong chuỗi xử lý: người soạn, lãnh đạo/văn thư đơn vị soạn, văn thư có quyền, Admin.
    private async Task<bool> CoTheXemAsync(DuThao d) =>
        d.MaNVSoan == MaNV || CoQuyen("CongVanDi.Nhap") || CoQuyen("Global.XemToanTruong")
        || await _db.LaLanhDaoDonViAsync(MaNV, d.MaDVSoan) || await _db.LaVanThuDonViAsync(MaNV, d.MaDVSoan);

    private async Task<DuThaoFormViewModel> TaoFormAsync(DuThao d)
    {
        var donVi = await _db.GetDonViAsync();
        var tenDV = (await _db.GetDonViAsync(chiLayConHoatDong: false)).FirstOrDefault(x => x.MaDV == d.MaDVSoan)?.TenDV ?? "";
        var ld = await _db.GetLanhDaoDonViAsync(d.MaDVSoan);
        return new DuThaoFormViewModel
        {
            DuThao = d,
            DanhSachLoaiVB = await _db.GetLoaiVBAsync(chiHienThi: true),
            DanhSachNhomCV = await _db.GetNhomCVAsync(),
            DanhSachDonVi = donVi,
            DanhSachKyTruong = await _db.GetLanhDaoAsync(),
            LanhDaoDonVi = ld,
            TacGiaLaLanhDaoDonVi = ld != null && ld.MaNV == d.MaNVSoan,
            Khoa = d.DaKhoaCauHinh,
            NguoiSuaLaTacGia = d.MaNVSoan == MaNV,
            LaVanThuCap1 = LaVanThuCap1,
            LaVanThuCap2 = LaVanThuCap2Cua(d.MaDVSoan),
            Files = d.ID > 0 ? await _db.GetFileDuThaoAsync(d.ID) : new(),
            DVNhan = (d.DonViNhan ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Where(x => byte.TryParse(x, out _)).Select(byte.Parse).ToList(),
            TenDVSoan = tenDV
        };
    }
}
