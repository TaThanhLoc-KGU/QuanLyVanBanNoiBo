namespace CongVan.Models;

public class ThongBaoNoiBo
{
    public int MaTB { get; set; }
    public string TieuDe { get; set; } = "";
    public string NoiDung { get; set; } = "";
    public byte DoiTuong { get; set; } // 0=Toàn trường,1=Theo đơn vị,2=Theo vai trò(quyền)
    public string? DonViNhan { get; set; }
    public string? QuyenNhan { get; set; }
    public byte MucDo { get; set; } // 1=Thường,2=Quan trọng,3=Khẩn
    public string? FileDinhKem { get; set; }
    public short MaNVDang { get; set; }
    public DateTime NgayDang { get; set; }
    public DateTime? HetHan { get; set; }
    // Hiện thêm ra trang công khai /cong-khai (không cần đăng nhập) — mặc định false, người đăng
    // phải chủ động tick (migration 036).
    public bool CongKhai { get; set; }

    // Hiển thị
    public string? TenNVDang { get; set; }
    public bool DaXem { get; set; }

    public string TenMucDo => MucDo switch
    {
        3 => "Khẩn",
        2 => "Quan trọng",
        _ => "Thường"
    };

    public string CssMucDo => MucDo switch
    {
        3 => "late",
        2 => "pending",
        _ => "info"
    };

    public string TenDoiTuong => DoiTuong switch
    {
        1 => "Theo đơn vị",
        2 => "Theo vai trò",
        _ => "Toàn trường"
    };
}
