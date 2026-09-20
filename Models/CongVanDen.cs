namespace CongVan.Models;

public class CongVanDen
{
    public string MSCV { get; set; } = "";
    public string SoCV { get; set; } = "";
    public DateTime NgayDen { get; set; }
    public int STT { get; set; }
    public string? STT1 { get; set; }
    public short? MaCQ { get; set; }
    public DateTime NgayBanHanh { get; set; }
    public string TrichYeu { get; set; } = "";
    public string? NguoiKy { get; set; }
    public string? FileDinhKem { get; set; }
    public string? GhiChu { get; set; }
    public short? MaLVB { get; set; }
    public byte MaSCV { get; set; }
    public short? MaNCV { get; set; }
    public short? MaLDXem { get; set; }
    public byte? MaDVXL { get; set; }
    public string? BoPhanPhoiHop { get; set; }
    public DateTime? NgayGiao { get; set; }
    public DateTime? NgayYCHT { get; set; }
    public DateTime? NgayHT { get; set; }
    public short MaVT { get; set; }
    public DateTime NgayNhap { get; set; }
    public short? MaNVXNHTCV { get; set; }
    public DateTime? NgayXNHTCV { get; set; }
    public bool? KQXN { get; set; }
    public string? MSCVDi { get; set; }
    public DateTime? NgayGuiEmail { get; set; }
    public short? MaNVGuiEmail { get; set; }
    public bool DungChung { get; set; } // Văn bản dùng chung nội bộ — mọi viên chức đều xem được (migration 037)
    public DateTime? DonViDaXemLuc { get; set; } // Đơn vị xử lý chính đã mở xem văn bản này lúc nào (NULL = chưa)

    // Navigation / display
    public string? TenCQ { get; set; }
    public string? TenLVB { get; set; }
    public string? TenSCV { get; set; }
    public string? TenNCV { get; set; }
    public string? TenDVXL { get; set; }
    public string? TenDVTat { get; set; }   // Tên viết tắt đơn vị XL
    public string? TenLDXem { get; set; }   // BGH chỉ đạo
    public string? TenLDPhong { get; set; } // Lãnh đạo phòng XL (MaNV_TheodoiCV của DonVi)
    public string? TenVT { get; set; }       // Văn thư nhập
    public string? TenNVXNHT { get; set; }   // NV xác nhận hoàn thành
    public string? TenNVGuiEmail { get; set; }
    public bool CoFile { get; set; }
    public bool DaXem { get; set; }          // Người dùng hiện tại đã xem chưa
    public byte? MaxXuLyTrangThai { get; set; } // MAX(TrangThai) từ CongVanDenXuLyDV các đơn vị được giao

    private bool DaHoanThanh => NgayHT.HasValue || NgayXNHTCV.HasValue;
    private bool QuaHan => !DaHoanThanh && NgayYCHT.HasValue && NgayYCHT < DateTime.Now;

    // Trạng thái tổng hợp: ưu tiên hoàn thành/quá hạn ở mức công văn, sau đó mới xét
    // tiến độ xử lý thực tế của (các) đơn vị được giao (CongVanDenXuLyDV) thay vì chỉ
    // dựa vào việc có đặt hạn xử lý hay không — tránh kẹt ở "Mới" dù đơn vị đã tiếp nhận/xử lý.
    public string TrangThaiCode
    {
        get
        {
            if (DaHoanThanh) return "hoanthanh";
            if (QuaHan) return "quahan";
            if (MaxXuLyTrangThai >= 2) return "dangxuly";
            if (MaxXuLyTrangThai == 1) return "datiepnhan";
            if (NgayYCHT.HasValue) return "dangxuly";
            return "moi";
        }
    }

    public string TrangThaiLabel => TrangThaiCode switch
    {
        "hoanthanh"  => "Hoàn thành",
        "quahan"     => "Quá hạn",
        "dangxuly"   => "Đang xử lý",
        "datiepnhan" => "Đã tiếp nhận",
        _            => "Mới"
    };

    public string TrangThaiCss => TrangThaiCode switch
    {
        "hoanthanh"  => "done",
        "quahan"     => "late",
        "dangxuly"   => "pending",
        "datiepnhan" => "info",
        _            => "info"
    };

    public string TrangThaiIcon => TrangThaiCode switch
    {
        "hoanthanh"  => "bi-check-circle-fill",
        "quahan"     => "bi-alarm-fill",
        "dangxuly"   => "bi-hourglass",
        "datiepnhan" => "bi-check2-circle",
        _            => "bi-circle"
    };
}
