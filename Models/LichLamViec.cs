namespace CongVan.Models;

public class LichLamViec
{
    public int MaLich { get; set; }
    public string TieuDe { get; set; } = "";
    public string? NoiDung { get; set; }
    public DateTime ThoiGianBatDau { get; set; }
    public DateTime? ThoiGianKetThuc { get; set; }
    public string? DiaDiem { get; set; }
    public short MaNVLanhDao { get; set; }
    public string? NguoiKemTheo { get; set; } // MaNV cách dấu phẩy — ẩn ở mức tổng quan, chỉ hiện khi xem chi tiết ngày
    public byte LoaiSuKien { get; set; } // 1=Họp,2=Công tác,3=Sự kiện,4=Khác
    public short MaNVTao { get; set; }
    public DateTime NgayTao { get; set; }

    // Gộp Lịch công tác vào đây (migration 035): 1=Lịch lãnh đạo, 2=Lịch công tác
    public byte LoaiLich { get; set; } = 1;
    public byte? MaDV { get; set; }        // chỉ dùng khi LoaiLich=2; NULL = toàn trường
    public byte? MaPhong { get; set; }
    public int? MaLichCongTacGoc { get; set; }

    // Hiển thị
    public string? TenNVLanhDao { get; set; }
    public string? TenDV { get; set; }
    public string? TenPhong { get; set; }

    public bool LaLichCongTac => LoaiLich == 2;
    public bool ToanTruong => LoaiLich == 2 && !MaDV.HasValue;
    public string TenLoaiLich => LoaiLich == 2 ? "Lịch công tác" : "Lịch lãnh đạo";

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
}
