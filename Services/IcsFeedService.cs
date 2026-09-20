using System.Security.Cryptography;
using System.Text;
using CongVan.Models;

namespace CongVan.Services;

// Xuất Lịch làm việc sang định dạng iCalendar (RFC 5545) để "liên kết" với Google Calendar/Outlook/
// Apple Calendar theo kiểu ĐĂNG KÝ (subscribe) 1 đường link — người dùng vào Google Calendar chọn
// "Cài đặt" → "Thêm lịch" → "Từ URL", dán link vào là xong, KHÔNG cần tạo project trên Google Cloud,
// không cần OAuth, không cần cấp quyền. Đánh đổi: chỉ 1 chiều (đọc), Google/Outlook tự làm mới theo
// chu kỳ riêng của họ (thường vài giờ/lần), không tức thời, và sửa trên Google không đẩy ngược lại
// hệ thống. Nếu sau này cần đồng bộ 2 chiều thật sự thì phải làm lại bằng Google Calendar API + OAuth.
public static class IcsFeedService
{
    // Chữ ký HMAC gắn liền với MaNV để tạo ra 1 URL feed riêng cho từng người mà không đoán được
    // nếu không biết khóa bí mật (IcsFeed:SecretKey trong appsettings) — tránh phải thêm cột token
    // riêng trong bảng NhanVien (không cần migration DB).
    // Khóa mẫu trong appsettings.json/.env.example (nằm công khai trong repo) hoặc quá ngắn coi như
    // CHƯA cấu hình — nếu không, ai đọc repo cũng tự ký được URL feed của bất kỳ nhân viên nào.
    public static string? LayKhoaHopLe(string? secretKey)
    {
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 16) return null;
        if (secretKey.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase) ||
            secretKey.StartsWith("DoiChuoiNgauNhien", StringComparison.OrdinalIgnoreCase)) return null;
        return secretKey;
    }

    public static bool ChuKyHopLe(int maNV, string chuKy, string secretKey) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(TinhChuKy(maNV, secretKey)),
            Encoding.ASCII.GetBytes((chuKy ?? "").ToLowerInvariant()));

    public static string TinhChuKy(int maNV, string secretKey)
    {
        var key = Encoding.UTF8.GetBytes(secretKey);
        var data = Encoding.UTF8.GetBytes(maNV.ToString());
        var hash = HMACSHA256.HashData(key, data);
        return Convert.ToHexString(hash)[..24].ToLowerInvariant();
    }

    private static string Escape(string? s) =>
        (s ?? "")
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n");

    // Giờ Việt Nam luôn = UTC+7, không có giờ mùa hè (DST) — quy đổi thẳng bằng phép trừ, không cần
    // khối VTIMEZONE phức tạp trong file .ics.
    private static string GioVNSangUtc(DateTime gioVN) => gioVN.AddHours(-7).ToString("yyyyMMddTHHmmssZ");
    private static string GioHienTaiUtc() => DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");

    // Không gấp dòng dài quá 75 octet theo đúng khuyến nghị RFC 5545 (để tránh cắt nhầm giữa 1 ký
    // tự UTF-8 nhiều byte) — các trình đọc phổ biến hiện nay (Google Calendar, Outlook, Apple
    // Calendar) đều chấp nhận dòng dài mà không đòi hỏi phải gấp dòng nghiêm ngặt.
    public static string TaoIcs(IEnumerable<LichLamViec> lichLamViec, IEnumerable<LichNoiBoDonVi> lichNoiBo, string tenLich)
    {
        var sb = new StringBuilder();
        sb.Append("BEGIN:VCALENDAR\r\n");
        sb.Append("VERSION:2.0\r\n");
        sb.Append("PRODID:-//CongVan VNKGU//Lich lam viec//VI\r\n");
        sb.Append("CALSCALE:GREGORIAN\r\n");
        sb.Append("METHOD:PUBLISH\r\n");
        sb.Append($"X-WR-CALNAME:{Escape(tenLich)}\r\n");
        sb.Append("X-WR-TIMEZONE:Asia/Ho_Chi_Minh\r\n");
        // Gợi ý chu kỳ làm mới cho client hỗ trợ (Google Calendar không đọc header này nhưng
        // Outlook/Thunderbird có đọc) — làm mới mỗi 3 giờ là đủ cho lịch nội bộ, không cần tức thời.
        sb.Append("X-PUBLISHED-TTL:PT3H\r\n");
        sb.Append("REFRESH-INTERVAL;VALUE=DURATION:PT3H\r\n");

        foreach (var ll in lichLamViec)
        {
            var ketThuc = ll.ThoiGianKetThuc ?? ll.ThoiGianBatDau.AddHours(1);
            var tieuDe = ll.LaLichCongTac
                ? (ll.ToanTruong ? $"[Công tác toàn trường] {ll.TieuDe}" : $"[Công tác - {ll.TenDV}] {ll.TieuDe}")
                : $"[{ll.TenNVLanhDao}] {ll.TieuDe}";
            sb.Append("BEGIN:VEVENT\r\n");
            sb.Append($"UID:lichlamviec-{ll.MaLich}@qlvb.vnkgu.edu.vn\r\n");
            sb.Append($"DTSTAMP:{GioHienTaiUtc()}\r\n");
            sb.Append($"DTSTART:{GioVNSangUtc(ll.ThoiGianBatDau)}\r\n");
            sb.Append($"DTEND:{GioVNSangUtc(ketThuc)}\r\n");
            sb.Append($"SUMMARY:{Escape(tieuDe)}\r\n");
            if (!string.IsNullOrWhiteSpace(ll.NoiDung)) sb.Append($"DESCRIPTION:{Escape(ll.NoiDung)}\r\n");
            if (!string.IsNullOrWhiteSpace(ll.DiaDiem)) sb.Append($"LOCATION:{Escape(ll.DiaDiem)}\r\n");
            else if (!string.IsNullOrWhiteSpace(ll.TenPhong)) sb.Append($"LOCATION:{Escape(ll.TenPhong)}\r\n");
            sb.Append("END:VEVENT\r\n");
        }

        foreach (var l in lichNoiBo)
        {
            var ketThuc = l.ThoiGianKetThuc ?? l.ThoiGianBatDau.AddHours(1);
            sb.Append("BEGIN:VEVENT\r\n");
            sb.Append($"UID:lichnoibo-{l.MaLich}@qlvb.vnkgu.edu.vn\r\n");
            sb.Append($"DTSTAMP:{GioHienTaiUtc()}\r\n");
            sb.Append($"DTSTART:{GioVNSangUtc(l.ThoiGianBatDau)}\r\n");
            sb.Append($"DTEND:{GioVNSangUtc(ketThuc)}\r\n");
            sb.Append($"SUMMARY:{Escape($"[Nội bộ - {l.TenDV}] {l.TieuDe}")}\r\n");
            if (!string.IsNullOrWhiteSpace(l.NoiDung)) sb.Append($"DESCRIPTION:{Escape(l.NoiDung)}\r\n");
            if (!string.IsNullOrWhiteSpace(l.DiaDiem)) sb.Append($"LOCATION:{Escape(l.DiaDiem)}\r\n");
            sb.Append("END:VEVENT\r\n");
        }

        sb.Append("END:VCALENDAR\r\n");
        return sb.ToString();
    }
}
