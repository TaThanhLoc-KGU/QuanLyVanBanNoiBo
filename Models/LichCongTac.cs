namespace CongVan.Models;

public class LichCongTac
{
    public int MaLich { get; set; }
    public string TieuDe { get; set; } = "";
    public string? NoiDung { get; set; }
    public DateTime ThoiGianBatDau { get; set; }
    public DateTime? ThoiGianKetThuc { get; set; }
    public string? DiaDiem { get; set; }
    public byte? MaDV { get; set; } // NULL = sự kiện toàn trường
    public string? NguoiThamGia { get; set; } // MaNV cách dấu phẩy
    public byte LoaiSuKien { get; set; } // 1=Họp,2=Công tác,3=Sự kiện,4=Khác
    public byte? MaPhong { get; set; } // Phòng họp đã đăng ký (tùy chọn)
    public short MaNVTao { get; set; }
    public DateTime NgayTao { get; set; }

    // Hiển thị
    public string? TenDV { get; set; }
    public string? TenNVTao { get; set; }
    public string? TenPhong { get; set; }

    public string TenLoaiSuKien => LoaiSuKien switch
    {
        1 => "Họp",
        2 => "Công tác",
        3 => "Sự kiện",
        _ => "Khác"
    };

    public string IconLoaiSuKien => LoaiSuKien switch
    {
        1 => "bi-people-fill",
        2 => "bi-briefcase-fill",
        3 => "bi-star-fill",
        _ => "bi-calendar-event-fill"
    };

    public bool ToanTruong => !MaDV.HasValue;

    // Dùng để đánh dấu các mục lấy từ CongViec.HanXuLy (không lưu trong bảng này, chỉ hiển thị)
    public bool TuCongViec { get; set; }
    public int? MaCVLienKet { get; set; }
}
