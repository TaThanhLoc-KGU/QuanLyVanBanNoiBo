namespace CongVan.Models;

public class CongVanDenChiDao
{
    public int ID { get; set; }
    public string MSCV { get; set; } = "";
    public byte MaDV { get; set; }
    public byte LoaiHanhDong { get; set; } // 1=Chỉ đạo & phân công, 2=Xin ý kiến BGH, 3=BGH cho ý kiến, 4=Trả lại văn thư, 5=Chuyên viên xin trả lại
    public short MaNV { get; set; }
    public short? MaNVNhan { get; set; }
    public short? MaNVPhanCong { get; set; }
    public int? MaCVLienKet { get; set; }
    public string? NoiDung { get; set; }
    public DateTime NgayTao { get; set; }

    // Hiển thị
    public string? TenDV { get; set; }
    public string? TenNV { get; set; }
    public string? TenNVNhan { get; set; }
    public string? TenNVPhanCong { get; set; }

    public string TenHanhDong => LoaiHanhDong switch
    {
        1 => "Chỉ đạo & phân công xử lý",
        2 => "Xin ý kiến chỉ đạo BGH",
        3 => "BGH cho ý kiến chỉ đạo",
        4 => "Trả lại văn thư",
        5 => "Xin trả lại/chuyển người khác",
        _ => "Khác"
    };

    public string IconHanhDong => LoaiHanhDong switch
    {
        1 => "bi-person-check-fill text-success",
        2 => "bi-question-circle-fill text-warning",
        3 => "bi-chat-left-quote-fill text-primary",
        4 => "bi-arrow-return-left text-danger",
        5 => "bi-arrow-return-left text-danger",
        _ => "bi-dot"
    };
}
