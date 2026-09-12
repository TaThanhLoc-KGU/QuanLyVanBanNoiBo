using Microsoft.AspNetCore.Mvc;
using CongVan.Models;
using CongVan.Services;

namespace CongVan.Controllers;

public class ThongBaoController : BaseController
{
    private readonly DbService _db;
    private readonly FileService _fileSvc;

    public ThongBaoController(DbService db, FileService fileSvc)
    {
        _db = db;
        _fileSvc = fileSvc;
    }

    // Đăng thông báo là kênh phát tin chính thức — admin luôn được đăng; văn thư/BGH được đăng vì
    // vai trò "Văn thư"/"Lãnh đạo trường" của họ đã có sẵn mã "ThongBao.Dang" (xem migration RBAC
    // v3), hoặc bất kỳ ai khác được cấp riêng chức năng này qua hệ vai trò/phân quyền chi tiết.
    private bool DuocDangThongBao => CoQuyen("ThongBao.Dang");

    public async Task<IActionResult> Index(byte? mucDo)
    {
        var vm = new ThongBaoNoiBoFilterViewModel
        {
            MucDo = mucDo,
            DanhSach = await _db.GetThongBaoNoiBoAsync(MaDV, Quyen, MaNV, mucDo)
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Nhap()
    {
        if (!DuocDangThongBao) return Forbid();
        var vm = new ThongBaoNoiBoFormViewModel
        {
            ThongBaoNoiBo = new ThongBaoNoiBo { MucDo = 1 },
            DanhSachDonVi = await _db.GetDonViAsync(chiLayConHoatDong: false),
            DanhSachQuyen = await _db.GetQuyenXLAsync()
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Nhap(ThongBaoNoiBoFormViewModel vm, List<byte> dvNhan, List<byte> quyenNhan, IFormFile? fileDinhKem)
    {
        if (!DuocDangThongBao) return Forbid();
        var tb = vm.ThongBaoNoiBo;

        if (string.IsNullOrWhiteSpace(tb.TieuDe) || string.IsNullOrWhiteSpace(tb.NoiDung))
        {
            TempData["Error"] = "Vui lòng nhập đầy đủ Tiêu đề và Nội dung.";
            vm.DanhSachDonVi = await _db.GetDonViAsync(chiLayConHoatDong: false);
            vm.DanhSachQuyen = await _db.GetQuyenXLAsync();
            vm.DanhSachDVNhan = dvNhan;
            vm.DanhSachQuyenNhan = quyenNhan;
            return View(vm);
        }

        tb.DonViNhan = tb.DoiTuong == 1 && dvNhan.Count > 0 ? string.Join(",", dvNhan) : null;
        tb.QuyenNhan = tb.DoiTuong == 2 && quyenNhan.Count > 0 ? string.Join(",", quyenNhan) : null;

        if (fileDinhKem != null && fileDinhKem.Length > 0)
            tb.FileDinhKem = await _fileSvc.SaveAsync(fileDinhKem, "thongbao");

        var maTB = await _db.ThemThongBaoNoiBoAsync(tb, MaNV);
        TempData["Success"] = "Đã đăng thông báo!";
        return RedirectToAction("ChiTiet", new { id = maTB });
    }

    public async Task<IActionResult> ChiTiet(int id)
    {
        var tb = await _db.GetThongBaoNoiBoByIdAsync(id, MaNV);
        if (tb == null) return NotFound();
        await _db.DanhDauDaXemThongBaoAsync(id, MaNV);
        return View(tb);
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(int maTB)
    {
        var tb = await _db.GetThongBaoNoiBoByIdAsync(maTB, MaNV);
        if (tb == null) return NotFound();
        if (!DuocDangThongBao && tb.MaNVDang != MaNV) return Forbid();

        if (!string.IsNullOrEmpty(tb.FileDinhKem)) _fileSvc.Delete(tb.FileDinhKem);
        await _db.XoaThongBaoNoiBoAsync(maTB);
        TempData["Success"] = "Đã xóa thông báo.";
        return RedirectToAction("Index");
    }
}
