using Microsoft.AspNetCore.Mvc;
using CongVan.Models;
using CongVan.Services;

namespace CongVan.Controllers;

// HỘP THƯ CÁ NHÂN "Văn bản của tôi" — mọi người dùng (kể cả viên chức thường) chỉ thấy văn bản được
// chuyển cho mình: Chờ xử lý / Đã xử lý / Xem để biết. Mô phỏng VNPT iOffice của Tỉnh: người nhận
// chuyển tiếp (chính / đồng xử lý / đồng gửi), cho ý kiến, trả lại, kết thúc xử lý — mọi bước có nhật ký.
public class HopThuController : BaseController
{
    private readonly DbService _db;
    public HopThuController(DbService db) => _db = db;

    public async Task<IActionResult> Index(string? tab, string? tuKhoa, byte? loai, int page = 1)
    {
        tab = tab is "da" or "xembiet" ? tab : "cho";
        if (loai is not (1 or 2)) loai = null;
        var (items, total) = await _db.GetHopThuAsync(MaNV, tab, tuKhoa, page, 30, loai);
        if (loai == 2) ViewData["DemChoDi"] = await _db.DemChoTheoLoaiAsync(MaNV, 2);
        var (cho, xb) = await _db.DemHopThuAsync(MaNV);
        return View(new HopThuViewModel
        {
            Tab = tab, TuKhoa = tuKhoa, Loai = loai, DanhSach = items, DemCho = cho, DemXemBiet = xb,
            Trang = new PageInfo { Page = page, PageSize = 30, TotalCount = total }
        });
    }

    // Người nhận (Xử lý chính / Đồng xử lý / Xem để biết) — form ở panel "Xử lý của tôi" gửi lên đây.
    [HttpPost]
    public async Task<IActionResult> Chuyen(byte loaiVB, string mscv, int? idDong, string? noiDung, DateTime? hanXuLy,
        short? nguoiChinh, List<short>? dongXuLy, List<short>? xemBiet, string? quayLai)
    {
        var nhan = new List<NguoiNhanXuLy>();
        if (nguoiChinh is > 0) nhan.Add(new NguoiNhanXuLy { MaNV = nguoiChinh.Value, VaiTro = VanBanXuLy.VaiTroChinh });
        foreach (var m in dongXuLy ?? new()) nhan.Add(new NguoiNhanXuLy { MaNV = m, VaiTro = VanBanXuLy.VaiTroDongXuLy });
        foreach (var m in xemBiet ?? new()) nhan.Add(new NguoiNhanXuLy { MaNV = m, VaiTro = VanBanXuLy.VaiTroXemBiet });
        if (nhan.Count == 0)
        {
            TempData["Error"] = "Vui lòng chọn ít nhất 1 người nhận (xử lý chính, đồng xử lý hoặc xem để biết).";
            return QuayLai(loaiVB, mscv, quayLai);
        }

        // Chuyển tiếp: phải đang giữ chính dòng đó. Chuyển gốc (idDong null): phải có quyền của văn thư/lãnh đạo.
        if (idDong.HasValue)
        {
            var dong = await _db.GetDongDangChoAsync(loaiVB, mscv, MaNV);
            if (dong == null || dong.ID != idDong.Value || dong.VaiTro == VanBanXuLy.VaiTroXemBiet)
                return Forbid();
        }
        else if (!await CoTheChuyenGocAsync(loaiVB, mscv)) return Forbid();

        var so = await _db.ChuyenXuLyAsync(loaiVB, mscv, MaNV, nhan, noiDung?.Trim(), hanXuLy, idDong);
        TempData[so > 0 ? "Success" : "Error"] = so > 0
            ? $"Đã chuyển cho {so} người."
            : "Không chuyển được — những người này đang giữ văn bản rồi.";
        return QuayLai(loaiVB, mscv, quayLai);
    }

    [HttpPost]
    public async Task<IActionResult> YKien(int id, byte loaiVB, string mscv, string? yKien)
    {
        if (string.IsNullOrWhiteSpace(yKien)) { TempData["Error"] = "Vui lòng nhập nội dung ý kiến."; return QuayLai(loaiVB, mscv, null); }
        TempData[await _db.ChoYKienAsync(id, MaNV, yKien.Trim()) ? "Success" : "Error"] =
            "Đã ghi ý kiến — văn bản chuyển sang mục Đã xử lý.";
        return QuayLai(loaiVB, mscv, null);
    }

    [HttpPost]
    public async Task<IActionResult> TraLai(int id, byte loaiVB, string mscv, string? lyDo)
    {
        if (string.IsNullOrWhiteSpace(lyDo)) { TempData["Error"] = "Vui lòng nhập lý do trả lại."; return QuayLai(loaiVB, mscv, null); }
        if (await _db.TraLaiXuLyAsync(id, MaNV, lyDo.Trim())) { TempData["Success"] = "Đã trả lại người chuyển."; return RedirectToAction("Index"); }
        TempData["Error"] = "Không trả lại được (văn bản không còn ở hộp thư của bạn).";
        return QuayLai(loaiVB, mscv, null);
    }

    [HttpPost]
    public async Task<IActionResult> KetThuc(int id, byte loaiVB, string mscv, string? ketQua)
    {
        if (string.IsNullOrWhiteSpace(ketQua)) { TempData["Error"] = "Vui lòng nhập kết quả xử lý."; return QuayLai(loaiVB, mscv, null); }
        TempData[await _db.KetThucXuLyAsync(id, MaNV, ketQua.Trim()) ? "Success" : "Error"] =
            "Đã kết thúc xử lý — văn bản chuyển sang mục Đã xử lý.";
        return QuayLai(loaiVB, mscv, null);
    }

    [HttpPost]
    public async Task<IActionResult> ThuHoi(int id, byte loaiVB, string mscv)
    {
        TempData[await _db.ThuHoiXuLyAsync(id, MaNV) ? "Success" : "Error"] =
            "Đã thu hồi (chỉ thu hồi được khi người nhận chưa mở xem).";
        return QuayLai(loaiVB, mscv, null);
    }

    private IActionResult QuayLai(byte loaiVB, string mscv, string? quayLai)
        => loaiVB == VanBanXuLy.LoaiDen
            ? RedirectToAction("ChiTiet", "CongVanDen", new { id = mscv })
            : RedirectToAction("ChiTiet", "DuThao", new { id = mscv });

    // Chuyển từ đầu (chưa ai giữ văn bản): văn thư / lãnh đạo có quyền văn bản đến, hoặc lãnh đạo/văn thư
    // của đơn vị xử lý văn bản đó.
    private async Task<bool> CoTheChuyenGocAsync(byte loaiVB, string mscv)
    {
        if (loaiVB != VanBanXuLy.LoaiDen) return false; // dự thảo: chỉ trình qua DuThao/Trinh
        if (CoQuyen("CongVanDen.Nhap") || CoQuyen("CongVanDen.ChiDaoPhanCong") || CoQuyen("CongVanDen.ChuyenXuLy")
            || CoQuyen("CongVanDen.XuLyDV") || CoQuyen("Global.XemToanTruong")) return true;
        var cv = await _db.GetCongVanDenByIdAsync(mscv);
        if (cv?.MaDVXL is not byte dv) return false;
        return await _db.LaLanhDaoDonViAsync(MaNV, dv) || await _db.LaVanThuDonViAsync(MaNV, dv);
    }
}
