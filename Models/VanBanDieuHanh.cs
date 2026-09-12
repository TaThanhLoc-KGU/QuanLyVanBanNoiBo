namespace CongVan.Models;

public class VanBanDieuHanh
{
    public string MSCV { get; set; } = "";
    public int STT { get; set; }
    public string? STT1 { get; set; }
    public DateTime NgayBanHanh { get; set; }
    public byte MaSCV { get; set; }
    public short MaLVB { get; set; }
    public string TrichYeu { get; set; } = "";
    public short? MaLDKy { get; set; }
    public string? PhamViApDung { get; set; }
    public string? GhiChu { get; set; }
    public string? FileDinhKem { get; set; }
    public short MaVT { get; set; }
    public DateTime NgayNhap { get; set; }
    public string? MSCVDi { get; set; } // Liên kết ngược văn bản đi gốc, nếu được tạo từ đó
    public bool DaKySo { get; set; }
    public DateTime? NgayKySo { get; set; }
    public short? MaNVKySo { get; set; }
    public string? LoaiChungThu { get; set; }

    // Hiển thị
    public string? TenSCV { get; set; }
    public string? TenLVB { get; set; }
    public string? TenLDKy { get; set; }
    public string? TenVT { get; set; }
    public string? TenNVKySo { get; set; }
}
