namespace CongVan.Models;

public class HoSoCongViec
{
    public int MaHoSo { get; set; }
    public string TieuDe { get; set; } = "";
    public string? MoTa { get; set; }
    public byte MaDVPhuTrach { get; set; }
    public short MaNVPhuTrach { get; set; }
    public byte TrangThai { get; set; } // 0=Đang mở,1=Tạm đóng,2=Đã đóng
    public DateTime NgayMo { get; set; }
    public DateTime? NgayDong { get; set; }
    public string? GhiChu { get; set; }
    public short MaNVTao { get; set; }
    public DateTime NgayTao { get; set; }

    // Hiển thị
    public string? TenDVPhuTrach { get; set; }
    public string? TenNVPhuTrach { get; set; }

    public string TenTrangThai => TrangThai switch
    {
        1 => "Tạm đóng",
        2 => "Đã đóng",
        _ => "Đang mở"
    };

    public string CssBadge => TrangThai switch
    {
        1 => "pending",
        2 => "done",
        _ => "info"
    };
}

public class HoSoCongViecVanBan
{
    public int ID { get; set; }
    public int MaHoSo { get; set; }
    public byte LoaiVanBan { get; set; } // 1=CongVanDen,2=CongVanDi,3=VanBanDieuHanh
    public string MSCV { get; set; } = "";
    public DateTime NgayGan { get; set; }
    public short MaNVGan { get; set; }

    // Hiển thị (điền ở tầng C#, dispatch theo LoaiVanBan)
    public string? TieuDe { get; set; }
    public DateTime? NgayVanBan { get; set; }

    public string TenLoaiVanBan => LoaiVanBan switch
    {
        1 => "Văn bản đến",
        2 => "Văn bản đi",
        3 => "Văn bản điều hành",
        _ => "?"
    };

    public string UrlChiTiet => LoaiVanBan switch
    {
        1 => $"/CongVanDen/ChiTiet/{MSCV.Trim()}",
        2 => $"/CongVanDi/ChiTiet/{MSCV.Trim()}",
        3 => $"/VanBanDieuHanh/ChiTiet/{MSCV.Trim()}",
        _ => "#"
    };
}

public class HoSoCongViecFile
{
    public int ID { get; set; }
    public int MaHoSo { get; set; }
    public string TenFile { get; set; } = "";
    public string DuongDan { get; set; } = "";
    public DateTime NgayUpload { get; set; }
    public short MaNVUpload { get; set; }

    public string? TenNVUpload { get; set; }
}
