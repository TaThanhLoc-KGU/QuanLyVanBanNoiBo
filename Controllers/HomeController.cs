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
        byte? filterMaDV = CoQuyen("Global.XemToanTruong") ? null : (byte?)MaDV;
        ViewBag.Stats = await _db.GetDashboardStatsAsync(nam);
        ViewBag.Nam = nam;
        ViewBag.Thang = DateTime.Now.Month;
        ViewBag.CVDenMoi = (await _db.GetCongVanDenAsync(nam, null, null, filterMaDV, MaNV)).Take(8).ToList();
        ViewBag.DonViChuaXem = (await _db.GetVanBanDonViChuaXemAsync(filterMaDV)).Take(8).ToList();
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
