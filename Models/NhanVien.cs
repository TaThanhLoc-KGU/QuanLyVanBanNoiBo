namespace CongVan.Models;

public class NhanVien
{
    public short MaNV { get; set; }
    public string HoNV { get; set; } = "";
    public string TenNV { get; set; } = "";
    public string? Email { get; set; }
    public string? MatKhau { get; set; }
    public string? AnhNV { get; set; }
    public byte MaDV { get; set; }
    public string? Username { get; set; }
    public DateTime? NgayNghiViec { get; set; }
    public string? LyDoNghiViec { get; set; }

    public string HoTen => $"{HoNV} {TenNV}".Trim();
    public string? TenDV { get; set; }
    public bool DaNghiViec => NgayNghiViec.HasValue;
}
