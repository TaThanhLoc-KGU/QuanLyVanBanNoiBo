namespace CongVan.Models;

// Danh mục mã chức năng chi tiết dùng cho hệ Vai trò (RBAC) — định nghĩa trong code (không phải
// bảng DB) vì đây là các "điểm gate" cố định gắn với action cụ thể trong controller, tương tự
// cách policy được khai báo trong ASP.NET Core. Thêm mã mới ở đây khi retrofit thêm controller.
//
// RBAC v3 (Phase 12): mở rộng phạm vi từ chỉ trang Admin ra TOÀN BỘ hệ thống. Từ đây, quyền 99
// (văn thư) KHÔNG còn tự động qua mọi cổng kiểm tra — phải được cấp qua Vai trò/override như quyền
// khác. Quyền 0 (admin) vẫn là "siêu người dùng" duy nhất luôn qua mọi cổng — xem BaseController.CoQuyen.
// Các hành động có phạm vi RIÊNG THEO ĐƠN VỊ (vd chỉ đạo/phân công/xử lý văn bản đến của 1 đơn vị)
// KHÔNG dùng mã ở đây làm cổng chính — chúng dùng check theo ngữ cảnh (DonVi.MaNV_TDV = lãnh đạo
// đơn vị, bảng DonVi_VanThu = văn thư đơn vị); mã chức năng dưới đây chỉ cấp quyền "vượt phạm vi",
// tức làm thay được cho MỌI đơn vị (dành cho Văn thư trung tâm / Lãnh đạo trường).
public static class ChucNangDangKy
{
    public static readonly List<ChucNang> TatCa = new()
    {
        new ChucNang { Ma = "Admin.NhanVien",     Ten = "Quản lý nhân viên",              NhomChucNang = "Quản trị hệ thống" },
        new ChucNang { Ma = "Admin.PhanQuyen",    Ten = "Phân quyền / quản lý vai trò",   NhomChucNang = "Quản trị hệ thống" },
        new ChucNang { Ma = "Admin.LanhDaoPhong", Ten = "Lãnh đạo phòng & Email đơn vị",  NhomChucNang = "Quản trị hệ thống" },
        new ChucNang { Ma = "Admin.BanGiamHieu",  Ten = "Quản lý Ban Giám Hiệu",           NhomChucNang = "Quản trị hệ thống" },
        new ChucNang { Ma = "Admin.DonVi",        Ten = "Quản lý đơn vị",                  NhomChucNang = "Quản trị hệ thống" },
        new ChucNang { Ma = "Admin.CoQuan",       Ten = "Quản lý cơ quan ban hành",        NhomChucNang = "Quản trị hệ thống" },
        new ChucNang { Ma = "Admin.LoaiVanBan",   Ten = "Quản lý loại văn bản",             NhomChucNang = "Quản trị hệ thống" },
        new ChucNang { Ma = "Admin.SoVanBan",     Ten = "Quản lý sổ văn bản",               NhomChucNang = "Quản trị hệ thống" },
        new ChucNang { Ma = "Admin.CauHinhEmail", Ten = "Cấu hình Email",                  NhomChucNang = "Quản trị hệ thống" },
        new ChucNang { Ma = "Admin.LuongXuLy",    Ten = "Cấu hình luồng xử lý",             NhomChucNang = "Quản trị hệ thống" },
        new ChucNang { Ma = "Admin.LuongDuyet",   Ten = "Cấu hình luồng duyệt / trình ký",  NhomChucNang = "Quản trị hệ thống" },
        new ChucNang { Ma = "Admin.PhongHop",     Ten = "Quản lý danh mục phòng họp",       NhomChucNang = "Quản trị hệ thống" },
        new ChucNang { Ma = "Admin.ApiKey",       Ten = "Cấp/thu hồi API Key theo đơn vị",  NhomChucNang = "Quản trị hệ thống" },

        new ChucNang { Ma = "Global.XemToanTruong", Ten = "Xem dữ liệu toàn trường (không giới hạn theo đơn vị)", NhomChucNang = "Chung" },

        new ChucNang { Ma = "CongVanDen.Nhap",             Ten = "Vào sổ / sửa văn bản đến",                        NhomChucNang = "Văn bản đến" },
        new ChucNang { Ma = "CongVanDen.Xoa",               Ten = "Xóa văn bản đến",                                 NhomChucNang = "Văn bản đến" },
        new ChucNang { Ma = "CongVanDen.GuiEmail",          Ten = "Gửi email thông báo văn bản đến",                 NhomChucNang = "Văn bản đến" },
        new ChucNang { Ma = "CongVanDen.XacNhanHoanThanh",  Ten = "Xác nhận hoàn thành chính thức",                  NhomChucNang = "Văn bản đến" },
        new ChucNang { Ma = "CongVanDen.XuLyDV",            Ten = "Xử lý văn bản đến của đơn vị khác (ngoài đơn vị mình)", NhomChucNang = "Văn bản đến" },
        new ChucNang { Ma = "CongVanDen.ChuyenXuLy",        Ten = "Chuyển xử lý vượt giới hạn luồng đã cấu hình",    NhomChucNang = "Văn bản đến" },
        new ChucNang { Ma = "CongVanDen.ChiDaoPhanCong",    Ten = "Chỉ đạo & phân công xử lý (ngoài đơn vị mình)",   NhomChucNang = "Văn bản đến" },
        new ChucNang { Ma = "CongVanDen.XinYKienBGH",       Ten = "Xin ý kiến chỉ đạo BGH (ngoài đơn vị mình)",      NhomChucNang = "Văn bản đến" },
        new ChucNang { Ma = "CongVanDen.TraLai",            Ten = "Trả lại văn thư (ngoài đơn vị mình)",             NhomChucNang = "Văn bản đến" },
        new ChucNang { Ma = "CongVanDen.BGHTraLoiYKien",    Ten = "Cho ý kiến chỉ đạo (vai trò BGH)",                NhomChucNang = "Văn bản đến" },

        new ChucNang { Ma = "CongVanDi.Nhap",     Ten = "Vào sổ / sửa văn bản đi",       NhomChucNang = "Văn bản đi" },
        new ChucNang { Ma = "CongVanDi.Xoa",      Ten = "Xóa văn bản đi",                 NhomChucNang = "Văn bản đi" },
        new ChucNang { Ma = "CongVanDi.GuiEmail", Ten = "Gửi email thông báo văn bản đi", NhomChucNang = "Văn bản đi" },
        new ChucNang { Ma = "CongVanDi.KySo",     Ten = "Tải file đã ký số lên",          NhomChucNang = "Văn bản đi" },
        new ChucNang { Ma = "CongVanDi.Excel",    Ten = "Nhập văn bản đi từ Excel",       NhomChucNang = "Văn bản đi" },
        new ChucNang { Ma = "CongVanDi.CongKhai", Ten = "Gắn / gỡ dấu công khai văn bản đi", NhomChucNang = "Văn bản đi" },

        new ChucNang { Ma = "VanBanDieuHanh.Nhap",  Ten = "Ban hành / sửa văn bản điều hành", NhomChucNang = "Văn bản điều hành" },
        new ChucNang { Ma = "VanBanDieuHanh.Xoa",   Ten = "Xóa văn bản điều hành",             NhomChucNang = "Văn bản điều hành" },
        new ChucNang { Ma = "VanBanDieuHanh.KySo",  Ten = "Tải file đã ký số lên",             NhomChucNang = "Văn bản điều hành" },
        new ChucNang { Ma = "VanBanDieuHanh.Excel", Ten = "Nhập văn bản điều hành từ Excel",   NhomChucNang = "Văn bản điều hành" },

        new ChucNang { Ma = "CongViec.QuanLyTatCa",    Ten = "Xem/sửa/xóa mọi công việc (không chỉ việc liên quan)", NhomChucNang = "Văn phòng điện tử" },
        new ChucNang { Ma = "HoSoCongViec.QuanLyTatCa",Ten = "Xem/sửa/xóa mọi hồ sơ công việc",                       NhomChucNang = "Văn phòng điện tử" },
        new ChucNang { Ma = "LichCongTac.ToanTruong",  Ten = "Tạo sự kiện lịch công tác toàn trường",                 NhomChucNang = "Văn phòng điện tử" },
        new ChucNang { Ma = "LichCongTac.QuanLyTatCa", Ten = "Xem/sửa/xóa mọi sự kiện lịch công tác",                 NhomChucNang = "Văn phòng điện tử" },
        new ChucNang { Ma = "LichLamViec.Tao",         Ten = "Tạo lịch làm việc lãnh đạo (của riêng mình)",           NhomChucNang = "Văn phòng điện tử" },
        new ChucNang { Ma = "ThongBao.Dang",           Ten = "Đăng thông báo nội bộ",                                 NhomChucNang = "Văn phòng điện tử" },
        new ChucNang { Ma = "LichNoiBoDonVi.QuanLyTatCa", Ten = "Xem/sửa/xóa lịch nội bộ mọi đơn vị (không chỉ đơn vị mình)", NhomChucNang = "Nội bộ đơn vị" },
        new ChucNang { Ma = "VanBanNoiBo.QuanLyTatCa",    Ten = "Xem/sửa/xóa văn bản nội bộ mọi đơn vị (không chỉ đơn vị mình)", NhomChucNang = "Nội bộ đơn vị" },
    };

    public static readonly Dictionary<string, string> TenTheoMa = TatCa.ToDictionary(c => c.Ma, c => c.Ten);

    // Helper dùng chung cho Controller (qua BaseController.CoQuyen) và trực tiếp trong Razor view
    // (view không có session/HttpContext tiện lợi như controller, chỉ có ViewBag.QuyenList +
    // ViewBag.ChucNang do BaseController gán sẵn) — cùng 1 luật để 2 nơi không bao giờ lệch nhau:
    // chỉ quyền 0 (admin) mặc định qua hết, còn lại phải có đúng mã trong tập ChucNang hiệu lực.
    public static bool CoQuyen(List<int> quyen, HashSet<string> chucNang, string maChucNang) =>
        quyen.Contains(0) || chucNang.Contains(maChucNang);
}
