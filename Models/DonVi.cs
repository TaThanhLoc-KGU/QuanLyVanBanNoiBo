namespace CongVan.Models;

public class DonVi
{
    public byte MaDV { get; set; }
    public byte? STT { get; set; }
    public string TenDV { get; set; } = "";
    public string? TenTat { get; set; }
    public short? MaNV_TheoDoiCV { get; set; }
    public short? MaNV_TDV { get; set; }
    public string? LoaiDV { get; set; }
    public DateTime? NgayGiaiThe { get; set; }
    public string? Email { get; set; }

    public bool DaGiaiThe => NgayGiaiThe.HasValue;
    public string TenDVHienThi => DaGiaiThe ? $"{TenDV} (đã giải thể)" : TenDV;
}
