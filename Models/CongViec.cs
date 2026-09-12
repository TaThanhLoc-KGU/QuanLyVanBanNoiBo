namespace CongVan.Models;

public class CongViec
{
    public int MaCV { get; set; }
    public string TieuDe { get; set; } = "";
    public string? MoTa { get; set; }
    public short MaNVGiao { get; set; }
    public short MaNVChuTri { get; set; }
    public byte MaDV { get; set; }
    public string? NguoiPhoiHop { get; set; } // danh sách MaNV cách nhau dấu phẩy
    public DateTime NgayGiao { get; set; }
    public DateTime? HanXuLy { get; set; }
    public byte MucDoUuTien { get; set; } // 1=Thường,2=Cao,3=Khẩn
    public byte TrangThai { get; set; } // 0=Mới giao,1=Đang thực hiện,2=Hoàn thành,3=Huỷ
    public DateTime? NgayHoanThanh { get; set; }
    public byte? LoaiNguonGoc { get; set; } // 1=CongVanDen,2=CongVanDi,3=VanBanDieuHanh
    public string? MSCVGoc { get; set; }
    public string? GhiChu { get; set; }
    public short MaNVTao { get; set; }
    public DateTime NgayTao { get; set; }

    // Hiển thị
    public string? TenNVGiao { get; set; }
    public string? TenNVChuTri { get; set; }
    public string? TenDV { get; set; }

    public string TenMucDo => MucDoUuTien switch
    {
        3 => "Khẩn",
        2 => "Cao",
        _ => "Thường"
    };

    public string TenTrangThai => TrangThai switch
    {
        1 => "Đang thực hiện",
        2 => "Hoàn thành",
        3 => "Đã huỷ",
        _ => "Mới giao"
    };

    public bool QuaHan => TrangThai < 2 && HanXuLy.HasValue && HanXuLy.Value.Date < DateTime.Today;

    public string CssBadge => QuaHan ? "late" : TrangThai switch
    {
        1 => "pending",
        2 => "done",
        3 => "late",
        _ => "info"
    };
}

public class CongViecNhatKy
{
    public int ID { get; set; }
    public int MaCV { get; set; }
    public short MaNV { get; set; }
    public DateTime NgayGhi { get; set; }
    public string NoiDung { get; set; } = "";
    public byte? TrangThaiMoi { get; set; }
    public string? FileDinhKem { get; set; }

    public string? TenNV { get; set; }
}

public class CongViecBinhLuan
{
    public int ID { get; set; }
    public int MaCV { get; set; }
    public short MaNV { get; set; }
    public string NoiDung { get; set; } = "";
    public DateTime NgayBinhLuan { get; set; }

    public string? TenNV { get; set; }
}

public class CongViecFile
{
    public int ID { get; set; }
    public int MaCV { get; set; }
    public string TenFile { get; set; } = "";
    public string DuongDan { get; set; } = "";
    public DateTime NgayUpload { get; set; }
    public short MaNVUpload { get; set; }

    public string? TenNVUpload { get; set; }
}
