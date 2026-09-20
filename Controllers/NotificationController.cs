using Microsoft.AspNetCore.Mvc;
using CongVan.Services;

namespace CongVan.Controllers;

public class NotificationController : BaseController
{
    private readonly DbService _db;
    public NotificationController(DbService db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetBadge()
    {
        if (MaNV == 0 || CoQuyen("Global.XemToanTruong"))
            return Json(new { count = 0, items = Array.Empty<object>(), eventCount = 0, events = Array.Empty<object>() });

        // Viên chức thường: chuông KHÔNG liệt kê văn bản đến theo đơn vị (họ chỉ thấy văn bản được chuyển cho mình —
        // xem ở "Văn bản của tôi"), chỉ giữ các thông báo việc mới giao/chuyển đến.
        var count = LaVienChucThuong ? 0 : await _db.DemCVChuaXemAsync(MaDV, MaNV);
        var items = LaVienChucThuong ? new List<Models.CongVanDen>() : await _db.GetCVMoiChuaXemAsync(MaDV, MaNV, 5);
        var eventCount = await _db.DemThongBaoChuaXemAsync(MaNV, MaDV);
        var events = await _db.GetThongBaoCaNhanAsync(MaNV, MaDV, 8);
        return Json(new {
            count,
            items = items.Select(cv => new {
                mscv  = cv.MSCV?.Trim(),
                soCV  = cv.STT1 ?? cv.STT.ToString(),
                trichYeu = cv.TrichYeu?.Length > 80 ? cv.TrichYeu[..80] + "…" : cv.TrichYeu,
                ngayDen  = cv.NgayDen.ToString("dd/MM"),
                tenCQ    = cv.TenCQ
            }),
            eventCount,
            events = events.Select(e => new {
                id = e.ID,
                mscv = e.MSCV?.Trim(),
                noiDung = e.NoiDung,
                icon = e.IconLoai,
                ngayTao = e.NgayTao.ToString("dd/MM HH:mm"),
                daXem = e.DaXem
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> DanhDauDaXemThongBao(int id)
    {
        await _db.DanhDauDaXemThongBaoAsync(id);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> DanhDauTatCaDaXemThongBao()
    {
        await _db.DanhDauTatCaDaXemThongBaoAsync(MaNV, MaDV);
        return Json(new { ok = true });
    }
}
