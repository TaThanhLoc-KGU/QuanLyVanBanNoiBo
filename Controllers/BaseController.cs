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
        "ThongBao", "LichNoiBoDonVi", "VanBanNoiBo", "HopThu", "DuThao"
    };
    protected static readonly string[] QuyenVuotPhamViCongVanDen =
    {
        "Global.XemToanTruong", "CongVanDen.Nhap", "CongVanDen.Xoa", "CongVanDen.GuiEmail",
        "CongVanDen.XacNhanHoanThanh", "CongVanDen.XuLyDV", "CongVanDen.ChuyenXuLy",
        "CongVanDen.ChiDaoPhanCong", "CongVanDen.XinYKienBGH", "CongVanDen.TraLai", "CongVanDen.BGHTraLoiYKien"
    };

    // VIÊN CHỨC THƯỜNG: không giữ quyền "Văn bản đến" nào, không phải văn thư/lãnh đạo đơn vị — chỉ thấy văn bản
    // được chuyển cho mình hoặc dùng chung. Chỉ áp dụng khi Admin đã công bố tính năng mới (hộp thư dùng được).
    protected bool LaVienChucThuong =>
        TinhNangMoiCongKhai && !QuyenVuotPhamViCongVanDen.Any(CoQuyen) && !LaVanThuDonVi && !LaLanhDaoDonVi;
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

    // Không có quyền: KHÔNG hiện trang lỗi 403 trắng của trình duyệt. Yêu cầu AJAX nhận JSON 403 (giữ hành vi cũ cho script);
    // còn lại báo "không có quyền" bằng thông báo đỏ rồi đưa người dùng về trang họ vừa đứng (hoặc Tổng quan).
    protected new IActionResult Forbid() => TuChoi("Bạn không có quyền sử dụng chức năng này.");

    protected IActionResult TuChoi(string thongBao)
    {
        bool ajax = Request.Headers["X-Requested-With"] == "XMLHttpRequest"
            || Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);
        if (ajax) return StatusCode(403, new { ok = false, msg = thongBao });

        TempData["Error"] = thongBao;
        var hienTai = Request.Path + Request.QueryString;
        if (Uri.TryCreate(Request.Headers.Referer.ToString(), UriKind.Absolute, out var u)
            && string.Equals(u.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(u.PathAndQuery, hienTai, StringComparison.OrdinalIgnoreCase)
            && !u.AbsolutePath.StartsWith("/Account", StringComparison.OrdinalIgnoreCase))
            return Redirect(u.PathAndQuery);
        return RedirectToAction("Index", "Home");
    }

    // Số văn bản đang chờ trong hộp thư cá nhân — hiện badge ở menu (mọi trang đều cần).
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (MaNV > 0 && XemDuocTinhNangMoi)
        {
            try
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<CongVan.Services.DbService>();
                var (cho, xb) = await db.DemHopThuAsync(MaNV);
                ViewData["DemHopThu"] = cho + xb;
            }
            catch { /* badge chỉ là phụ — không để lỗi đếm làm hỏng cả trang */ }
        }
        await base.OnActionExecutionAsync(context, next);
    }

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
                context.Result = TuChoi("Chức năng này chưa được mở cho bạn.");
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
