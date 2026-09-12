namespace CongVan.Models;

// Sổ văn bản nội bộ riêng của 1 đơn vị — song song với CongVanDen/CongVanDi (sổ toàn trường), nhưng
// đơn vị tự quản lý hoàn toàn (tự đánh STT, tự đặt số hiệu), không đi qua văn thư trung tâm. Cũng là
// đối tượng dữ liệu chính mà API Key theo đơn vị (DonVi_ApiKey) cấp quyền đọc/ghi cho web riêng.
public class VanBanNoiBo
{
    public int ID { get; set; }
    public byte MaDV { get; set; }
    public int STT { get; set; }
    public string? SoHieu { get; set; }
    public string TieuDe { get; set; } = "";
    public string? NoiDung { get; set; }
    public DateTime NgayBanHanh { get; set; }
    public string? NguoiKy { get; set; }
    public short? MaNVKy { get; set; }
    public byte TrangThai { get; set; } // 0=Dự thảo,1=Đã ban hành,2=Thu hồi
    public short MaNVTao { get; set; }
    public DateTime NgayTao { get; set; }
    public byte NguonTao { get; set; } // 0=Web nội bộ,1=API ngoài
    public string? LoaiVanBanNoiBo { get; set; } // Mã loại tự đặt của đơn vị (vd "QĐ"), liên kết VanBanNoiBo_MauSo — NULL nếu số hiệu nhập tay

    // Hiển thị
    public string? TenDV { get; set; }
    public string? TenNVTao { get; set; }
    public string? TenNVKy { get; set; }
    public int SoFile { get; set; }

    public string TenTrangThai => TrangThai switch
    {
        0 => "Dự thảo",
        2 => "Thu hồi",
        _ => "Đã ban hành"
    };

    public string CssTrangThai => TrangThai switch
    {
        0 => "pending",
        2 => "late",
        _ => "done"
    };
}

public class VanBanNoiBoFile
{
    public int ID { get; set; }
    public int VanBanID { get; set; }
    public string TenFile { get; set; } = "";
    public string DuongDan { get; set; } = "";
    public DateTime NgayUpload { get; set; }
}

// Mẫu số hiệu tự đặt riêng của 1 đơn vị (xem migrations/027_vanbannoibo_tudong_so.sql) — đơn vị tự
// định nghĩa "loại" của mình (không dùng chung LoaiVB/MauSoKyHieu toàn trường), tự sinh số hiệu như
// "01/QĐ-P.QTCSVC" mà không cần Admin trung tâm cấu hình giúp.
public class VanBanNoiBoMauSo
{
    public int ID { get; set; }
    public byte MaDV { get; set; }
    public string MaLoai { get; set; } = "";
    public string TenLoai { get; set; } = "";
    public string MauChuoi { get; set; } = "";
    public DateTime NgayTao { get; set; }
}
