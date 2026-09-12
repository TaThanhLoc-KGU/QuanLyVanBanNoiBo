namespace CongVan.Models;

// Lịch làm việc chung của nội bộ 1 đơn vị — khác LichLamViec (lịch cá nhân của riêng 1 lãnh đạo)
// và khác LichCongTac (sự kiện toàn trường hoặc liên đơn vị). Mọi nhân viên trong đơn vị xem được,
// tạo được cho đơn vị mình — không cần qua văn thư trung tâm hay lãnh đạo trường.
public class LichNoiBoDonVi
{
    public int MaLich { get; set; }
    public byte MaDV { get; set; }
    public string TieuDe { get; set; } = "";
    public string? NoiDung { get; set; }
    public DateTime ThoiGianBatDau { get; set; }
    public DateTime? ThoiGianKetThuc { get; set; }
    public string? DiaDiem { get; set; }
    public byte LoaiSuKien { get; set; } // 1=Họp,2=Công tác,3=Sự kiện,4=Khác
    public short MaNVTao { get; set; }
    public DateTime NgayTao { get; set; }

    // Hiển thị
    public string? TenDV { get; set; }
    public string? TenNVTao { get; set; }

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
