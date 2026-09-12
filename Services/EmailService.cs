using System.Net;
using System.Net.Mail;
using CongVan.Models;

namespace CongVan.Services;

public class EmailService
{
    private readonly DbService _db;
    private readonly ILogger<EmailService> _logger;
    private const long MaxAttachBytes = 10 * 1024 * 1024; // 10 MB mỗi file

    public EmailService(DbService db, ILogger<EmailService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(bool ok, string msg)> GuiThongBaoCongVanDenAsync(
        CongVanDen cv,
        string tenDVXL,
        string emailDVXL,
        IReadOnlyList<(string TenFile, string FullPath)>? files = null)
    {
        var cfg = await _db.GetEmailConfigAsync();
        if (cfg == null || !cfg.IsActive)
            return (false, "Chưa bật/cấu hình gửi email trong Quản trị hệ thống");
        if (string.IsNullOrWhiteSpace(emailDVXL))
            return (false, "Đơn vị nhận chưa cấu hình email");

        // Phân loại: file đính kèm được (≤ 10 MB, tồn tại trên disk) và file quá lớn
        var dinh_kem = new List<(string TenFile, string FullPath)>();
        var qua_lon  = new List<string>();
        foreach (var (ten, path) in files ?? [])
        {
            if (!File.Exists(path)) continue;
            if (new FileInfo(path).Length <= MaxAttachBytes)
                dinh_kem.Add((ten, path));
            else
                qua_lon.Add(ten);
        }

        var subject = $"[Văn bản đến] {cv.TrichYeu}";
        var body    = BuildEmailBody(cv, tenDVXL, dinh_kem.Count, qua_lon);

        try
        {
            await SendAsync(cfg, emailDVXL, subject, body, dinh_kem);
            return (true, "Gửi thành công");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool ok, string msg)> TestEmailAsync(string toEmail)
    {
        var cfg = await _db.GetEmailConfigAsync();
        if (cfg == null || string.IsNullOrWhiteSpace(cfg.SmtpHost))
            return (false, "Chưa cấu hình SMTP");

        try
        {
            await SendAsync(cfg, toEmail, "Test email từ hệ thống VNKGU",
                "<p>Email test thành công từ Hệ thống Quản lý Văn bản VNKGU.</p>");
            return (true, "Gửi thành công");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task SendAsync(
        EmailConfig cfg,
        string toEmail,
        string subject,
        string body,
        IReadOnlyList<(string TenFile, string FullPath)>? attachments = null)
    {
        using var smtp = new SmtpClient(cfg.SmtpHost, cfg.SmtpPort)
        {
            Credentials = new NetworkCredential(cfg.SmtpUser, cfg.SmtpPassword),
            EnableSsl = cfg.UseSSL,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 120000
        };

        using var msg = new MailMessage
        {
            From = new MailAddress(cfg.SenderEmail, cfg.SenderName),
            Subject = subject,
            Body = body,
            IsBodyHtml = true,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8
        };

        foreach (var addr in toEmail.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var a = addr.Trim();
            if (!string.IsNullOrEmpty(a)) msg.To.Add(a);
        }
        if (msg.To.Count == 0) return;

        // Đính kèm file
        var streams = new List<FileStream>();
        try
        {
            foreach (var (tenFile, fullPath) in attachments ?? [])
            {
                var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                streams.Add(fs);
                var mime = GetMimeType(tenFile);
                msg.Attachments.Add(new Attachment(fs, tenFile, mime));
            }

            await smtp.SendMailAsync(msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi gửi email đến {To}", toEmail);
            throw;
        }
        finally
        {
            foreach (var att in msg.Attachments) att.Dispose();
            foreach (var fs in streams) fs.Dispose();
        }
    }

    private static string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf"  => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc"  => "application/msword",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls"  => "application/vnd.ms-excel",
            ".png"  => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _       => "application/octet-stream"
        };
    }

    private static string BuildEmailBody(
        CongVanDen cv,
        string tenDVXL,
        int soDinhKem,
        IReadOnlyList<string> quaLon)
    {
        var soVb = string.IsNullOrEmpty(cv.STT1) ? cv.STT.ToString() : cv.STT1;

        // Banner đính kèm
        string bannerDinhKem;
        if (soDinhKem > 0)
            bannerDinhKem = $"""
            <div style="margin-top:16px; padding:12px 14px; background:#ecfdf5; border-left:3px solid #10b981; border-radius:4px; font-size:13px;">
              <strong style="color:#065f46;">📎 Tệp đính kèm ({soDinhKem} file):</strong>
              Văn bản được đính kèm trực tiếp trong email này.
            </div>
            """;
        else
            bannerDinhKem = "";

        // Cảnh báo file quá lớn
        string bannerQuaLon = quaLon.Count > 0
            ? $"""
            <div style="margin-top:8px; padding:12px 14px; background:#fffbeb; border-left:3px solid #f59e0b; border-radius:4px; font-size:13px;">
              <strong>⚠ File sau đây quá lớn (&gt;10 MB), vui lòng tải trực tiếp từ hệ thống:</strong>
              <ul style="margin:6px 0 0; padding-left:18px;">{string.Concat(quaLon.Select(f => $"<li>{System.Net.WebUtility.HtmlEncode(f)}</li>"))}</ul>
            </div>
            """
            : "";

        return $"""
        <!DOCTYPE html>
        <html lang="vi">
        <head><meta charset="UTF-8"></head>
        <body style="font-family: Arial, sans-serif; color: #222; background:#f3f4f6; margin:0; padding:20px;">
          <div style="max-width:640px; margin:auto; background:#fff; border-radius:8px; overflow:hidden; box-shadow:0 2px 8px rgba(0,0,0,.1);">
            <div style="background:#1a56db; padding:20px 24px;">
              <p style="margin:0; color:#fff; font-size:13px; opacity:.85;">TRƯỜNG ĐẠI HỌC KIÊN GIANG - VNKGU</p>
              <h2 style="margin:6px 0 0; color:#fff; font-size:18px;">Thông báo văn bản đến mới</h2>
            </div>
            <div style="padding:24px;">
              <p style="margin:0 0 16px; color:#555; font-size:14px;">
                Phòng/Ban <strong style="color:#1a56db;">{System.Net.WebUtility.HtmlEncode(tenDVXL)}</strong>
                vừa được giao văn bản đến. Chi tiết:
              </p>
              <div style="margin-bottom:16px; padding:12px 14px; background:#fef2f2; border-left:3px solid #dc2626; border-radius:4px; font-size:13.5px; color:#7f1d1d;">
                <strong>⚠ Vui lòng đăng nhập vào phần mềm Quản lý văn bản để xử lý và ghi nhận kết quả xử lý văn bản này.</strong>
              </div>
              <table style="width:100%; border-collapse:collapse; font-size:14px;">
                <tr style="background:#f0f5ff;">
                  <td style="padding:10px 12px; width:38%; font-weight:600; color:#374151; border-bottom:1px solid #e5e7eb;">Trích yếu</td>
                  <td style="padding:10px 12px; border-bottom:1px solid #e5e7eb;">{System.Net.WebUtility.HtmlEncode(cv.TrichYeu)}</td>
                </tr>
                <tr>
                  <td style="padding:10px 12px; font-weight:600; color:#374151; border-bottom:1px solid #e5e7eb;">Số ký hiệu</td>
                  <td style="padding:10px 12px; border-bottom:1px solid #e5e7eb;">{System.Net.WebUtility.HtmlEncode(cv.SoCV)}</td>
                </tr>
                <tr style="background:#f0f5ff;">
                  <td style="padding:10px 12px; font-weight:600; color:#374151; border-bottom:1px solid #e5e7eb;">Loại văn bản</td>
                  <td style="padding:10px 12px; border-bottom:1px solid #e5e7eb;">{System.Net.WebUtility.HtmlEncode(cv.TenLVB ?? "—")}</td>
                </tr>
                <tr>
                  <td style="padding:10px 12px; font-weight:600; color:#374151; border-bottom:1px solid #e5e7eb;">Cơ quan ban hành</td>
                  <td style="padding:10px 12px; border-bottom:1px solid #e5e7eb;">{System.Net.WebUtility.HtmlEncode(cv.TenCQ ?? "—")}</td>
                </tr>
                <tr style="background:#f0f5ff;">
                  <td style="padding:10px 12px; font-weight:600; color:#374151; border-bottom:1px solid #e5e7eb;">Ngày ban hành</td>
                  <td style="padding:10px 12px; border-bottom:1px solid #e5e7eb;">{cv.NgayBanHanh:dd/MM/yyyy}</td>
                </tr>
                <tr>
                  <td style="padding:10px 12px; font-weight:600; color:#374151; border-bottom:1px solid #e5e7eb;">Ngày đến</td>
                  <td style="padding:10px 12px; border-bottom:1px solid #e5e7eb;">{cv.NgayDen:dd/MM/yyyy}</td>
                </tr>
                <tr style="background:#f0f5ff;">
                  <td style="padding:10px 12px; font-weight:600; color:#374151; border-bottom:1px solid #e5e7eb;">Người ký</td>
                  <td style="padding:10px 12px; border-bottom:1px solid #e5e7eb;">{System.Net.WebUtility.HtmlEncode(cv.NguoiKy ?? "—")}</td>
                </tr>
                {(cv.NgayYCHT.HasValue ? $"""
                <tr>
                  <td style="padding:10px 12px; font-weight:600; color:#dc2626; border-bottom:1px solid #e5e7eb;">Hạn xử lý</td>
                  <td style="padding:10px 12px; color:#dc2626; font-weight:600; border-bottom:1px solid #e5e7eb;">{cv.NgayYCHT:dd/MM/yyyy}</td>
                </tr>
                """ : "")}
              </table>
              {(!string.IsNullOrWhiteSpace(cv.GhiChu) ? $"""
              <div style="margin-top:16px; padding:12px 14px; background:#fffbeb; border-left:3px solid #f59e0b; border-radius:4px; font-size:13px;">
                <strong>Ghi chú:</strong> {System.Net.WebUtility.HtmlEncode(cv.GhiChu)}
              </div>
              """ : "")}
              {bannerDinhKem}
              {bannerQuaLon}
              <p style="margin-top:20px; font-size:12px; color:#9ca3af;">
                Email này được gửi từ Hệ thống Quản lý Văn bản VNKGU. Vui lòng không trả lời email này.
              </p>
            </div>
            <div style="background:#f9fafb; padding:12px 24px; text-align:center; font-size:12px; color:#9ca3af; border-top:1px solid #e5e7eb;">
              Trường Đại học Kiên Giang &mdash; vnkgu.edu.vn
            </div>
          </div>
        </body>
        </html>
        """;
    }
}
