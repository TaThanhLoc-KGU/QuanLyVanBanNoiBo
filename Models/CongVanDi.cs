namespace CongVan.Models;

public class CongVanDi
{
    public string MSCV { get; set; } = "";
    public double STT { get; set; }
    public string? STT1 { get; set; }
    public string? SoCVCT { get; set; }
    public DateTime NgayCongVan { get; set; }
    public DateTime NgayBanHanh { get; set; }
    public byte MaSCV { get; set; }
    public short MaNCV { get; set; }
    public short MaLVB { get; set; }
    public string TrichYeu { get; set; } = "";
    public short MaLDKy { get; set; }
    public string? NoiNhanCV { get; set; }
    public short MaVT { get; set; }
    public short SoLuong { get; set; }
    public string? FileDinhKem { get; set; }
    public string? GhiChu { get; set; }
    public string? MSCVDen { get; set; }
    public string? DonViNhan { get; set; } // Danh sách MaDV cách nhau dấu phẩy (đơn vị nội bộ nhận)
    public DateTime NgayNhap { get; set; }
    public bool DaKySo { get; set; }
    public DateTime? NgayKySo { get; set; }
    public short? MaNVKySo { get; set; }
    public string? LoaiChungThu { get; set; }
    public string? NguoiSoanThao { get; set; }  // Người soạn thảo (cá nhân) — không bắt buộc
    public string? DonViSoanThao { get; set; }   // Đơn vị soạn thảo (tên/tên viết tắt) — không bắt buộc

    // "Dấu công khai" — cho phép văn bản này lộ ra qua API công khai (api/v1/vanban-cong-khai)
    public bool CongKhai { get; set; }
    public DateTime? NgayCongKhai { get; set; }
    public short? MaNVCongKhai { get; set; }

    // Display
    public string? TenLVB { get; set; }
    public string? TenSCV { get; set; }
    public string? TenNCV { get; set; }
    public string? TenLDKy { get; set; }
    public string? TenNVKySo { get; set; }
}
