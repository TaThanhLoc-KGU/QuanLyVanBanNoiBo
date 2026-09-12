using Microsoft.AspNetCore.Mvc;
using CongVan.Models;
using CongVan.Services;

namespace CongVan.Controllers;

public class AccountController : Controller
{
    private readonly DbService _db;
    public AccountController(DbService db) => _db = db;

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        var nv = await _db.LoginAsync(model.Username, model.Password);
        if (nv == null)
        {
            ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng.");
            return View(model);
        }

        var quyen = await _db.GetQuyenAsync(nv.MaNV);
        var chucNang = await _db.GetChucNangHieuLucAsync(nv.MaNV);
        bool laVanThuDonVi = await _db.LaVanThuDonViAsync(nv.MaNV, nv.MaDV);
        bool laLanhDaoDonVi = await _db.LaLanhDaoDonViAsync(nv.MaNV, nv.MaDV);
        bool tinhNangMoiCongKhai = await _db.GetTinhNangMoiCongKhaiAsync();
        HttpContext.Session.SetInt32("login-MaNV", nv.MaNV);
        HttpContext.Session.SetInt32("login-MaDV", nv.MaDV);
        HttpContext.Session.SetString("login-HoTen", nv.HoTen);
        HttpContext.Session.SetString("login-TenDV", nv.TenDV ?? "");
        HttpContext.Session.SetString("login-Quyen", string.Join(",", quyen.Select(q => q.MaQuyen)));
        HttpContext.Session.SetString("login-ChucNang", string.Join(",", chucNang));
        HttpContext.Session.SetString("login-LaVanThuDonVi", laVanThuDonVi ? "1" : "0");
        HttpContext.Session.SetString("login-LaLanhDaoDonVi", laLanhDaoDonVi ? "1" : "0");
        HttpContext.Session.SetString("login-TinhNangMoiCongKhai", tinhNangMoiCongKhai ? "1" : "0");

        return RedirectToAction("Index", "CongVanDen");
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult DoiMatKhau()
    {
        SetViewBag();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> DoiMatKhau(ChangePasswordViewModel model)
    {
        SetViewBag();
        if (!string.IsNullOrWhiteSpace(model.MatKhauMoi) && model.MatKhauMoi != model.NhapLaiMatKhau)
        {
            ModelState.AddModelError("NhapLaiMatKhau", "Mật khẩu nhập lại không khớp.");
            return View(model);
        }

        var maNV = (short)(HttpContext.Session.GetInt32("login-MaNV") ?? 0);
        if (maNV == 0) return RedirectToAction("Login");

        var ok = await _db.ChangePasswordAsync(maNV, model.MatKhauCu,
            string.IsNullOrWhiteSpace(model.MatKhauMoi) ? model.MatKhauCu : model.MatKhauMoi,
            model.Email);
        if (!ok)
        {
            ModelState.AddModelError("MatKhauCu", "Mật khẩu cũ không đúng.");
            return View(model);
        }

        TempData["Success"] = "Đổi mật khẩu thành công!";
        return RedirectToAction("DoiMatKhau");
    }

    private void SetViewBag()
    {
        var hoTen = HttpContext.Session.GetString("login-HoTen");
        if (!string.IsNullOrEmpty(hoTen)) ViewBag.HoTen = hoTen;
        ViewBag.TenDV = HttpContext.Session.GetString("login-TenDV");
        var s = HttpContext.Session.GetString("login-Quyen") ?? "";
        ViewBag.QuyenList = s.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(x => int.TryParse(x, out var n) ? n : -1)
                             .Where(x => x >= 0).ToList();
    }
}
