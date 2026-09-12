namespace CongVan.Models;

// Mẫu luồng duyệt (trình ký) do admin cấu hình — xem migrations/034_luong_duyet.sql
public class LuongDuyet
{
    public int ID { get; set; }
    public string Ten { get; set; } = "";
    public byte LoaiVanBan { get; set; }   // 2=Văn bản đi, 3=Văn bản điều hành
    public string? MoTa { get; set; }
    public bool MacDinh { get; set; }
    public bool HienThi { get; set; } = true;
    public DateTime NgayTao { get; set; }

    public List<LuongDuyetBuoc> DanhSachBuoc { get; set; } = new();
    public int SoBuoc { get; set; }
}

public class LuongDuyetBuoc
{
    public int ID { get; set; }
    public int LuongID { get; set; }
    public byte ThuTu { get; set; }
    public string TenBuoc { get; set; } = "";
    public byte LoaiNguoiDuyet { get; set; }
    public string? GiaTri { get; set; }
    public bool ChoPhepTraLai { get; set; } = true;
    public bool LaBuocKy { get; set; }

    public static readonly (byte Ma, string Ten)[] CacLoaiNguoiDuyet =
    {
        (1, "Người trình (người soạn)"),
        (2, "Lãnh đạo đơn vị của người trình"),
        (3, "Người có chức năng…"),
        (4, "Người thuộc vai trò…"),
        (5, "Người cụ thể…"),
        (6, "Ban Giám Hiệu"),
    };

    public string TenLoaiNguoiDuyet => LoaiNguoiDuyet switch
    {
        1 => "Người trình",
        2 => "Lãnh đạo đơn vị của người trình",
        3 => $"Người có chức năng: {GiaTri}",
        4 => $"Người thuộc vai trò #{GiaTri}",
        5 => $"Người #{GiaTri}",
        6 => "Ban Giám Hiệu",
        _ => "?"
    };
}

// Một dòng nhật ký của chuỗi trình ký
public class TrinhKyNhatKy
{
    public int ID { get; set; }
    public int TrinhKyID { get; set; }
    public int? CapID { get; set; }
    public string HanhDong { get; set; } = "";
    public short MaNV { get; set; }
    public string? NoiDung { get; set; }
    public DateTime NgayTao { get; set; }

    public string? TenNV { get; set; }
    public string HanhDongText => HanhDong switch
    {
        "trinh"     => "Trình ký",
        "duyet"     => "Đã duyệt",
        "tra_lai"   => "Trả lại",
        "tu_choi"   => "Từ chối",
        "ky"        => "Đã ký",
        "hoan_tat"  => "Hoàn tất",
        _ => HanhDong
    };
}
