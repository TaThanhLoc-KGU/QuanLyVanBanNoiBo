namespace CongVan.Models;

public class EmailConfig
{
    public int Id { get; set; }
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = "";
    public string SmtpPassword { get; set; } = "";
    public string SenderName { get; set; } = "Hệ thống Quản lý Văn bản VNKGU";
    public string SenderEmail { get; set; } = "";
    public bool UseSSL { get; set; } = true;
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}
