namespace CongVan.Models;

public class CoQuan
{
    public short MaCQ { get; set; }
    public string? TenCQ { get; set; }
    public bool HienThi { get; set; } = true;
}

public class LoaiVB
{
    public short MaLVB { get; set; }
    public string? TenLVB { get; set; }
    public string? KyHieu { get; set; }
    public bool HienThi { get; set; } = true;
    public bool DungChung { get; set; } // Văn bản đến loại này mặc định là "dùng chung" nội bộ (migration 037)
}

public class LoaiVBCVDi
{
    public short MaLVB { get; set; }
    public string? TenLVB { get; set; }
    public bool HienThi { get; set; } = true;
}

public class CVDenFile
{
    public int ID { get; set; }
    public string MSCV { get; set; } = "";
    public string TenFile { get; set; } = "";
    public string DuongDan { get; set; } = "";
    public DateTime NgayUpload { get; set; }
    public string? NoiDungTrichXuat { get; set; } // Chữ trích xuất từ PDF (đọc trực tiếp hoặc OCR) — dùng để tìm kiếm nội dung
    public byte TrangThaiTrichXuat { get; set; } // 0=Chưa xử lý,1=Xong,2=Lỗi
}

public class SoCV
{
    public byte MaSCV { get; set; }
    public string? TenSCV { get; set; }
    public string? MoTa { get; set; }
    public bool DemRiengTheoLoai { get; set; } // true = mỗi loại văn bản có số thứ tự riêng trong sổ này
}

public class MauSoKyHieu
{
    public int ID { get; set; }
    public byte MaSCV { get; set; }
    public short? MaLVB { get; set; } // NULL = mẫu mặc định của sổ khi loại văn bản không có mẫu riêng
    public string MauChuoi { get; set; } = "";
    public DateTime NgayTao { get; set; }

    // Hiển thị
    public string? TenSCV { get; set; }
    public string? TenLVB { get; set; }
}

public class NhomCV
{
    public short MaNCV { get; set; }
    public string TenNCV { get; set; } = "";
}

public class PhongHop
{
    public byte MaPhong { get; set; }
    public string TenPhong { get; set; } = "";
    public string? ViTri { get; set; }
    public short? SucChua { get; set; }
    public string? GhiChu { get; set; }
    public bool HienThi { get; set; } = true;
}

public class PhanQuyen
{
    public short MaNV { get; set; }
    public byte MaDV { get; set; }
    public byte MaQuyen { get; set; }
}

public class QuyenXL
{
    public byte MaQuyen { get; set; }
    public string? TenQuyen { get; set; }
}

public class CVDen_DV
{
    public string MSCV { get; set; } = "";
    public byte MaDV { get; set; }
    public short MaNV { get; set; }
    public DateTime NgayGiao { get; set; }
    public DateTime? NgayXem { get; set; }
    public DateTime? NgayYCHT { get; set; }
    public DateTime? NgayHT { get; set; }
    public DateTime? NgayXNHT { get; set; }
    public bool? KQXN { get; set; }
    public string? FileDinhKem { get; set; }
    public string? TenDV { get; set; }
    public string? HoTenNV { get; set; }
}

public class CVDen_QTXL
{
    public string MSCV { get; set; } = "";
    public byte MaDV { get; set; }
    public short MaNV { get; set; }
    public DateTime NgayXL { get; set; }
    public string? NoiDungXL { get; set; }
    public string? FileDinhKem { get; set; }
    public string? HoTenNV { get; set; }
    public string? TenDV { get; set; }
}
