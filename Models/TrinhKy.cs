namespace CongVan.Models;

// Trình ký tuần tự nhiều cấp — mở rộng từ ký số đơn giản (Phase 10, "tải file đã ký lên") sang 1
// chuỗi duyệt có thứ tự trước khi đến bước ký chính thức. Chỉ 1 chuỗi đang hoạt động (TrangThai=0)
// cho mỗi văn bản tại 1 thời điểm — tránh lặp lại lỗi 2 cơ chế song song đã sửa ở luồng xử lý.
public class TrinhKy
{
    public int ID { get; set; }
    public byte LoaiVanBan { get; set; } // 2=CongVanDi, 3=VanBanDieuHanh (khớp quy ước HoSoCongViec_VanBan)
    public string MSCV { get; set; } = "";
    public byte TrangThai { get; set; } // 0=Đang trình ký,1=Hoàn tất (đã ký),2=Bị từ chối/thu hồi
    public short MaNVTrinh { get; set; }
    public DateTime NgayTrinh { get; set; }
    public string? GhiChu { get; set; }
    public int? LuongID { get; set; }

    public string? TenNVTrinh { get; set; }
    public string? TenLuong { get; set; }
    public List<TrinhKyCap> DanhSachCap { get; set; } = new();
    public List<TrinhKyNhatKy> NhatKy { get; set; } = new();
}

public class TrinhKyCap
{
    public int ID { get; set; }
    public int TrinhKyID { get; set; }
    public byte ThuTu { get; set; }
    public short MaNVDuyet { get; set; }
    public byte TrangThai { get; set; } // 0=Chờ,1=Đã duyệt,2=Từ chối,3=Đã ký,4=Đã trả lại(chờ bước trước làm lại)
    public DateTime? NgayXL { get; set; }
    public string? GhiChu { get; set; }
    public string? TenBuoc { get; set; }
    public bool ChoPhepTraLai { get; set; } = true;
    public bool LaBuocKy { get; set; }
    public byte? LoaiNguoiDuyet { get; set; }
    public string? GiaTriNguoiDuyet { get; set; }

    public string? TenNVDuyet { get; set; }

    public string TrangThaiText => TrangThai switch
    {
        1 => "Đã duyệt", 2 => "Từ chối", 3 => "Đã ký", 4 => "Đã trả lại", _ => "Chờ xử lý"
    };
}
