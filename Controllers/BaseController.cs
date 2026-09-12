using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using CongVan.Models;

namespace CongVan.Controllers;

public class BaseController : Controller
{
    protected short MaNV => (short)(HttpContext.Session.GetInt32("login-MaNV") ?? 0);
    protected byte MaDV => (byte)(HttpContext.Session.GetInt32("login-MaDV") ?? 0);
    protected string HoTen => HttpContext.Session.GetString("login-HoTen") ?? "";
    // Là văn thư của chính đơn vị mình (bảng DonVi_VanThu), cache lúc đăng nhập — xem AccountController.Login.
    protected bool LaVanThuDonVi => HttpContext.Session.GetString("login-LaVanThuDonVi") == "1";
    // Là lãnh đạo phụ trách chính đơn vị mình (DonVi.MaNV_TDV), cache lúc đăng nhập — dùng để hiện/ẩn
    // các mục "nội bộ đơn vị" (phân quyền, lịch, văn bản) mà không cần query DB mỗi request.
    protected bool LaLanhDaoDonVi => HttpContext.Session.GetString("login-LaLanhDaoDonVi") == "1";
    // Cong tac chung cac tinh nang moi (ngoai cong van den/di) — xem DbService.GetTinhNangMoiCongKhaiAsync.
    // Admin (quyen 0) luon thay de test truoc ke ca khi con dang tat cho nguoi khac.
    protected bool TinhNangMoiCongKhai => HttpContext.Session.GetString("login-TinhNangMoiCongKhai") == "1";
    protected bool XemDuocTinhNangMoi => Quyen.Contains(0) || TinhNangMoiCongKhai;

    // Cac controller thuoc "tinh nang moi" — chan truy cap truc tiep qua URL khi con dang tat va
    // nguoi dung khong phai Admin, khong chi an tren menu. Them ten controller vao day khi xay
    // tinh nang moi muon giu kin truoc khi cong bo rong.
    private static readonly HashSet<string> ControllerTinhNangMoi = new(StringComparer.OrdinalIgnoreCase)
    {
        "VanBanDieuHanh", "CongViec", "HoSoCongViec", "LichCongTac", "LichLamViec",
        "ThongBao", "LichNoiBoDonVi", "VanBanNoiBo"
    };
    protected List<int> Quyen
    {
        get
        {
            var s = HttpContext.Session.GetString("login-Quyen") ?? "";
            return s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        }
    }

    // Tập mã chức năng chi tiết hiệu lực của người dùng hiện tại (từ vai trò + cấp trực tiếp),
    // cache trong session lúc đăng nhập — xem DbService.GetChucNangHieuLucAsync.
    protected HashSet<string> ChucNang
    {
        get
        {
            var s = HttpContext.Session.GetString("login-ChucNang") ?? "";
            return s.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        }
    }

    // RBAC v3: chỉ Admin (quyền 0) còn là "siêu người dùng" tự động qua mọi cổng kiểm tra chi tiết.
    // Văn thư (99) và BGH (12) KHÔNG còn tự động bypass nữa — quyền của họ đến từ Vai trò họ được
    // gán (hoặc cấp trực tiếp qua NhanVien_ChucNang) — xem migrations/017_rbac_v3.sql: mọi người
    // đang giữ quyền 99/12 đã được auto-gán vào vai trò "Văn thư"/"Lãnh đạo trường" tương ứng với
    // đầy đủ quyền tương đương hôm nay, nên không ai mất quyền khi đổi luật này.
    protected bool CoQuyen(string maChucNang) => ChucNangDangKy.CoQuyen(Quyen, ChucNang, maChucNang);

    protected new IActionResult Forbid() => StatusCode(403);

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (MaNV == 0)
        {
            context.Result = RedirectToAction("Login", "Account");
            return;
        }
        if (!XemDuocTinhNangMoi)
        {
            var tenController = (context.RouteData.Values["controller"] as string) ?? "";
            if (ControllerTinhNangMoi.Contains(tenController))
            {
                context.Result = StatusCode(404);
                return;
            }
        }
        ViewBag.HoTen = HoTen;
        ViewBag.TenDV = HttpContext.Session.GetString("login-TenDV");
        ViewBag.MaNV = MaNV;
        ViewBag.MaDV = MaDV;
        ViewBag.Quyen = Quyen;
        ViewBag.QuyenList = Quyen;
        ViewBag.ChucNang = ChucNang;
        ViewBag.LaVanThuDonVi = LaVanThuDonVi;
        ViewBag.LaLanhDaoDonVi = LaLanhDaoDonVi;
        ViewBag.XemDuocTinhNangMoi = XemDuocTinhNangMoi;
        base.OnActionExecuting(context);
    }
}
