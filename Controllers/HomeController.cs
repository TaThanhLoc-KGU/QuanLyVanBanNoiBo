using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using CongVan.Models;
using CongVan.Services;

namespace CongVan.Controllers;

public class HomeController : BaseController
{
    private readonly DbService _db;
    public HomeController(DbService db) => _db = db;

    public async Task<IActionResult> Index()
    {
        int nam = DateTime.Now.Year;
        if (LaVienChucThuong)
        {
            var (cho, xb) = await _db.DemHopThuAsync(MaNV);
            ViewBag.DemCho = cho; ViewBag.DemXemBiet = xb;
            ViewBag.HopThuMoi = (await _db.GetHopThuAsync(MaNV, "cho", null, 1, 6)).Items;
            ViewBag.DuThao = (await _db.GetDuThaoCuaToiAsync(MaNV, null)).Where(d => d.TrangThai != 3).Take(5).ToList();
            ViewBag.DungChung = (await _db.GetCongVanDenPagedAsync(nam, null, null, null, MaNV, null, null, null, null, 1, 6,
                chiCaNhanVaDungChung: true)).Items;
            return View("CaNhan");
        }
        byte? filterMaDV = CoQuyen("Global.XemToanTruong") ? null : (byte?)MaDV;
        ViewBag.Stats = await _db.GetDashboardStatsAsync(nam);
        ViewBag.Nam = nam;
        ViewBag.Thang = DateTime.Now.Month;
        // Chỉ lấy 8 dòng mới nhất (trước đây tải TOÀN BỘ văn bản đến cả năm rồi mới Take(8) — chậm và dễ timeout).
        ViewBag.CVDenMoi = (await _db.GetCongVanDenPagedAsync(nam, null, null, filterMaDV, MaNV, null, null, null, null, 1, 8)).Items;
        ViewBag.DonViChuaXem = (await _db.GetVanBanDonViChuaXemAsync(filterMaDV)).Take(8).ToList();
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
