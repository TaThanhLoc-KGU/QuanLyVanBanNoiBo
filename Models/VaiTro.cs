namespace CongVan.Models;

public class VaiTro
{
    public int MaVaiTro { get; set; }
    public string TenVaiTro { get; set; } = "";
    public string? GhiChu { get; set; }
    public DateTime NgayTao { get; set; }

    public int SoNguoi { get; set; }
    public List<string> DanhSachChucNang { get; set; } = new();
}

public class ChucNang
{
    public string Ma { get; set; } = "";
    public string Ten { get; set; } = "";
    public string NhomChucNang { get; set; } = "";
}

public class LuongXuLy
{
    public int ID { get; set; }
    public byte MaDVNguon { get; set; }
    public byte MaDVDich { get; set; }
    public string? GhiChu { get; set; }
    public DateTime NgayTao { get; set; }

    public string? TenDVNguon { get; set; }
    public string? TenDVDich { get; set; }
}
