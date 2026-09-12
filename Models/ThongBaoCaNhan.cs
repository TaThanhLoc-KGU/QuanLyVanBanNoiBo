namespace CongVan.Models;

// Thông báo sự kiện xử lý — sinh tự động khi 1 văn bản được giao mới hoặc chuyển xử lý đến 1 đơn vị,
// khác với ThongBaoNoiBo (bảng tin nội bộ do người dùng tự đăng thủ công).
public class ThongBaoCaNhan
{
    public int ID { get; set; }
    public byte MaDV { get; set; }
    public short? MaNV { get; set; } // NULL = cả đơn vị
    public byte Loai { get; set; }
    public string NoiDung { get; set; } = "";
    public string? MSCV { get; set; }
    public DateTime NgayTao { get; set; }
    public bool DaXem { get; set; }
    public DateTime? NgayXem { get; set; }

    public string IconLoai => Loai switch
    {
        2 => "bi-arrow-right-circle-fill",
        3 => "bi-person-check-fill",
        4 => "bi-question-circle-fill",
        5 => "bi-arrow-return-left",
        6 => "bi-chat-left-quote-fill",
        _ => "bi-inbox-fill"
    };
}
