namespace CongVan.Models;

// API Key cấp cho 1 đơn vị để web riêng của đơn vị đó gọi vào API Văn bản nội bộ (xem
// Controllers/Api/VanBanNoiBoApiController.cs). Chỉ lưu hash SHA-256 của key — key thật (plaintext)
// chỉ hiện đúng 1 lần lúc tạo, không thể xem lại sau đó (giống access token của GitHub/Stripe...).
public class DonViApiKey
{
    public int ID { get; set; }
    public byte MaDV { get; set; }
    public string TenKey { get; set; } = "";
    public string ApiKeyHash { get; set; } = "";
    public bool HienThi { get; set; } = true;
    public DateTime NgayTao { get; set; }
    public short MaNVTao { get; set; }
    public DateTime? NgaySuDungCuoi { get; set; }

    // Hiển thị
    public string? TenDV { get; set; }
    public string? TenNVTao { get; set; }
}
