namespace CongVan.Models;

public class CongVanDenXuLyDV
{
    public int ID { get; set; }
    public string MSCV { get; set; } = "";
    public byte MaDV { get; set; }
    public string? TenDV { get; set; }
    public string? TenDVTat { get; set; }
    public byte LoaiDV { get; set; }   // 1=Chủ trì, 2=Phối hợp
    public byte TrangThai { get; set; } // 0-3
    public DateTime? NgayXem { get; set; }
    public DateTime? NgayTiepNhan { get; set; }
    public DateTime? NgayHoanThanh { get; set; }
    public DateTime? NgayCapNhat { get; set; }
    public string? GhiChu { get; set; }
    public string? FilePath { get; set; }
    public string? TenFile { get; set; }
    public short? MaNVCapNhat { get; set; }
    public byte? ChuyenToiMaDV { get; set; }

    // Hiển thị
    public string? TenChuyenToi { get; set; }
    public int SoFile { get; set; }

    public string TenLoaiDV => LoaiDV == 1 ? "Chủ trì" : "Phối hợp";

    public string TenTrangThai => TrangThai switch
    {
        1 => "Đã tiếp nhận",
        2 => "Đang xử lý",
        3 => "Hoàn thành",
        4 => "Đã chuyển tiếp",
        5 => "Đã trả lại",
        _ => "Chưa tiếp nhận"
    };

    public string CssBadge => TrangThai switch
    {
        1 => "info",
        2 => "pending",
        3 => "done",
        4 => "info",
        5 => "late",
        _ => "late"
    };

    public bool CoFile => !string.IsNullOrEmpty(FilePath);
}

// Nhiều file kết quả xử lý cho 1 dòng CongVanDenXuLyDV — thay cho 2 cột FilePath/TenFile cũ trên
// chính bảng đó (chỉ chứa được đúng 1 file, upload mới đè mất file cũ). Dữ liệu file đơn cũ đã được
// chuyển hết sang đây qua migrations/025_xulydv_multi_file.sql, không mất lịch sử.
public class CVDenXuLyDVFile
{
    public int ID { get; set; }
    public int XuLyDVID { get; set; }
    public string TenFile { get; set; } = "";
    public string DuongDan { get; set; } = "";
    public DateTime NgayUpload { get; set; }
    public short? MaNVUpload { get; set; }

    // Hiển thị
    public string? TenNVUpload { get; set; }
}
