namespace CongVan.Services;

// Lỗi upload "biết trước" (dung lượng/loại file không hợp lệ, hoặc lỗi ghi đĩa) — controller bắt
// riêng loại này để hiện thông báo rõ ràng cho người dùng thay vì để lộ lỗi 500/stack trace.
public class FileUploadException : Exception
{
    public FileUploadException(string message) : base(message) { }
    public FileUploadException(string message, Exception inner) : base(message, inner) { }
}

public class FileService
{
    private readonly string _root;
    private readonly long _maxSizeBytes;
    private readonly ILogger<FileService> _logger;

    // Chặn hẳn các đuôi file có thể thực thi được — văn thư/chuyên viên chỉ cần đính kèm văn bản/
    // ảnh/bảng tính, không có lý do gì cần tải lên file thực thi. Không chặn theo Content-Type vì
    // giá trị đó do trình duyệt tự khai, dễ giả — chỉ tin phần đuôi file để lọc thô ban đầu.
    private static readonly HashSet<string> DuoiBiCam = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".sh", ".ps1", ".psm1", ".msi", ".com", ".scr", ".vbs",
        ".js", ".jse", ".wsf", ".wsh", ".jar", ".cpl", ".msc", ".hta", ".apk", ".app"
    };

    public FileService(IConfiguration config, ILogger<FileService> logger)
    {
        _root = config["FileStorage:RootPath"]
            ?? throw new InvalidOperationException("Chưa cấu hình FileStorage:RootPath trong appsettings.json");
        // Mặc định 50MB/file — cùng ngưỡng đã dùng cho API tải văn bản nội bộ (xem
        // VanBanNoiBoApiController [RequestSizeLimit]); có thể chỉnh qua FileStorage:MaxFileSizeBytes.
        _maxSizeBytes = config.GetValue<long?>("FileStorage:MaxFileSizeBytes") ?? 50_000_000L;
        _logger = logger;
    }

    public async Task<string> SaveAsync(IFormFile file, string loai)
    {
        var ext = Path.GetExtension(file.FileName);
        if (DuoiBiCam.Contains(ext))
            throw new FileUploadException($"Không cho phép tải lên file loại \"{ext}\" (có thể thực thi được). Vui lòng chỉ đính kèm văn bản/ảnh/bảng tính.");
        if (file.Length > _maxSizeBytes)
            throw new FileUploadException($"File \"{file.FileName}\" ({file.Length / 1_000_000.0:0.0}MB) vượt quá giới hạn cho phép ({_maxSizeBytes / 1_000_000.0:0}MB).");
        if (file.Length == 0)
            throw new FileUploadException($"File \"{file.FileName}\" rỗng (0 byte) — vui lòng kiểm tra lại file gốc.");

        var now = DateTime.Now;
        var folder = Path.Combine(_root, loai, now.Year.ToString(), now.Month.ToString("D2"));

        try
        {
            Directory.CreateDirectory(folder);

            var fileName = Guid.NewGuid().ToString("N") + ext;
            var fullPath = Path.Combine(folder, fileName);

            await using var fs = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(fs);

            return $"{loai}/{now.Year}/{now.Month:D2}/{fileName}";
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // Đúng lỗi đã gặp thật 2026-09-06 (thư mục tháng mới bị lệch quyền sở hữu trên host).
            // Container giờ tự chown lại /data/files mỗi lần khởi động (xem entrypoint.sh) nên
            // KHÔNG còn nên xảy ra nữa — nếu vẫn xảy ra thì log rõ đường dẫn để dễ tra ngay, và trả
            // về thông báo dễ hiểu cho người dùng thay vì để ASP.NET hiện lỗi 500 kèm stack trace.
            _logger.LogError(ex, "FileService: khong ghi duoc file vao {Folder} (loai={Loai})", folder, loai);
            throw new FileUploadException(
                "Không thể lưu file lên server (lỗi quyền truy cập thư mục lưu trữ). " +
                "Vui lòng thử lại sau ít phút hoặc báo Quản trị viên nếu vẫn còn lỗi.", ex);
        }
    }

    public string GetFullPath(string relativePath)
    {
        return Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public bool Exists(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return false;
        return File.Exists(GetFullPath(relativePath));
    }

    public void Delete(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return;
        try
        {
            var full = GetFullPath(relativePath);
            if (File.Exists(full)) File.Delete(full);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // Xóa file vật lý là dọn dẹp phụ — không nên làm hỏng cả thao tác chính (vd xóa công
            // văn) chỉ vì 1 file lẻ không xóa được trên đĩa. Log lại để dọn tay sau nếu cần.
            _logger.LogWarning(ex, "FileService: khong xoa duoc file {Path}", relativePath);
        }
    }

    public static string GetMimeType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf"  => "application/pdf",
        ".doc"  => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls"  => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".png"  => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif"  => "image/gif",
        _       => "application/octet-stream"
    };
}
