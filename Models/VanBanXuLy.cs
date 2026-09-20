namespace CongVan.Models;

// Một bước chuyển văn bản tới MỘT người (hộp thư cá nhân) — xem migrations/037_hop_thu_ca_nhan.sql.
public class VanBanXuLy
{
    public const byte LoaiDen = 1;   // Văn bản đến
    public const byte LoaiDuThao = 2; // Dự thảo văn bản đi

    public const byte VaiTroChinh = 1;    // Xử lý chính
    public const byte VaiTroDongXuLy = 2; // Đồng xử lý (cho ý kiến)
    public const byte VaiTroXemBiet = 3;  // Đồng gửi (xem để biết)

    public const byte ChoXuLy = 0, DangXuLy = 1, DaChuyenTiep = 2, DaKetThuc = 3, DaTraLai = 4, BiThuHoi = 5;

    public int ID { get; set; }
    public byte LoaiVB { get; set; }
    public string MSCV { get; set; } = "";
    public short MaNVNhan { get; set; }
    public short? MaNVGui { get; set; }
    public byte VaiTro { get; set; }
    public byte TrangThai { get; set; }
    public string? NoiDungGui { get; set; }
    public string? YKien { get; set; }
    public DateTime? HanXuLy { get; set; }
    public DateTime NgayGui { get; set; }
    public DateTime? NgayXem { get; set; }
    public DateTime? NgayXuLy { get; set; }
    public int? IDCha { get; set; }

    // Hiển thị
    public string? TenNVNhan { get; set; }
    public string? TenNVGui { get; set; }
    public string? TrichYeu { get; set; }
    public string? SoKyHieu { get; set; }
    public string? TenLVB { get; set; }
    public string? TenCQ { get; set; }
    public DateTime? NgayVB { get; set; }

    public string TenVaiTro => VaiTro switch { 1 => "Xử lý chính", 2 => "Đồng xử lý", _ => "Xem để biết" };
    public string TenTrangThai => TrangThai switch
    {
        0 => "Chờ xử lý", 1 => "Đang xử lý", 2 => "Đã chuyển tiếp", 3 => "Đã kết thúc", 4 => "Đã trả lại", _ => "Bị thu hồi"
    };
    public bool DangCho => TrangThai <= 1;
}

public class VanBanNhatKy
{
    public int ID { get; set; }
    public byte LoaiVB { get; set; }
    public string MSCV { get; set; } = "";
    public short MaNV { get; set; }
    public string HanhDong { get; set; } = "";
    public string? NoiDung { get; set; }
    public short? MaNVLienQuan { get; set; }
    public DateTime Ngay { get; set; }
    public string? TenNV { get; set; }
    public string? TenNVLienQuan { get; set; }

    public string TenHanhDong => HanhDong switch
    {
        "ChuyenXuLy" => "Chuyển xử lý", "ChuyenTiep" => "Chuyển tiếp", "YKien" => "Cho ý kiến",
        "TraLai" => "Trả lại", "KetThuc" => "Kết thúc xử lý", "ThuHoi" => "Thu hồi", _ => HanhDong
    };
}

// 1 người nhận trong yêu cầu chuyển xử lý.
public class NguoiNhanXuLy
{
    public short MaNV { get; set; }
    public byte VaiTro { get; set; }
}
