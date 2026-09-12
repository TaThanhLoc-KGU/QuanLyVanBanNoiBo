using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using CongVan.Services;

namespace CongVan.Controllers.Api;

// API CÔNG KHAI — không cần đăng nhập, không cần API Key. Chỉ đọc (GET), chỉ trả các văn bản đi
// đã được văn thư gắn "dấu công khai" (CongVanDi.CongKhai = 1). Dùng cho các trang web công khai
// khác của trường lấy về hiển thị. Tài liệu: API_VANBAN_CONGKHAI.md
//
// Không lộ: nơi nhận nội bộ, ghi chú, đường dẫn file, thông tin người nhập — chỉ các trường của
// một "sổ văn bản đi công khai" đúng nghĩa.
[ApiController]
[Route("api/v1/vanban-cong-khai")]
[EnableCors("ApiCors")]
public class VanBanCongKhaiApiController : ControllerBase
{
    private readonly DbService _db;
    private readonly FileService _fileSvc;
    public VanBanCongKhaiApiController(DbService db, FileService fileSvc) { _db = db; _fileSvc = fileSvc; }

    public record VanBanCongKhaiDto(
        string Id, string SoVanBan, DateTime NgayVanBan, DateTime NgayBanHanh,
        string TrichYeu, string? LoaiVanBan, string? SoVanBanThuoc, string? NguoiKy,
        string? DonViSoanThao, bool DaKySo, bool CoFile, DateTime? NgayCongKhai);

    public record TrangDto(int Total, int Page, int PageSize, IEnumerable<VanBanCongKhaiDto> Items);

    private static VanBanCongKhaiDto ToDto(Models.CongVanDi cv) => new(
        cv.MSCV?.Trim() ?? "",
        string.IsNullOrWhiteSpace(cv.STT1) ? cv.STT.ToString("0") : cv.STT1!.Trim(),
        cv.NgayCongVan, cv.NgayBanHanh, cv.TrichYeu,
        cv.TenLVB, cv.TenSCV, string.IsNullOrWhiteSpace(cv.TenLDKy) ? null : cv.TenLDKy!.Trim(),
        string.IsNullOrWhiteSpace(cv.DonViSoanThao) ? null : cv.DonViSoanThao,
        cv.DaKySo, !string.IsNullOrEmpty(cv.FileDinhKem), cv.NgayCongKhai);

    /// <summary>Danh sách văn bản đi công khai (phân trang). Lọc: nam, maSCV, maLVB, q (từ khóa trích yếu/số).</summary>
    [HttpGet]
    [ResponseCache(Duration = 120, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> DanhSach(
        [FromQuery] int? nam, [FromQuery] byte? maSCV, [FromQuery] short? maLVB,
        [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _db.GetCongVanDiCongKhaiAsync(nam, maSCV, maLVB, q, page, pageSize);
        return Ok(new TrangDto(total, page, pageSize, items.Select(ToDto)));
    }

    /// <summary>Chi tiết 1 văn bản đi công khai theo mã số (id = MSCV). 404 nếu không tồn tại hoặc chưa công khai.</summary>
    [HttpGet("{id}")]
    [ResponseCache(Duration = 120, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> ChiTiet(string id)
    {
        var cv = await _db.GetCongVanDiCongKhaiByIdAsync(id.Trim());
        if (cv == null) return NotFound(new { error = "Không tìm thấy văn bản công khai." });
        return Ok(new
        {
            id = cv.MSCV?.Trim(),
            soVanBan = string.IsNullOrWhiteSpace(cv.STT1) ? cv.STT.ToString("0") : cv.STT1!.Trim(),
            ngayVanBan = cv.NgayCongVan,
            ngayBanHanh = cv.NgayBanHanh,
            trichYeu = cv.TrichYeu,
            loaiVanBan = cv.TenLVB,
            soVanBanThuoc = cv.TenSCV,
            nguoiKy = string.IsNullOrWhiteSpace(cv.TenLDKy) ? null : cv.TenLDKy!.Trim(),
            donViSoanThao = cv.DonViSoanThao,
            soLuong = cv.SoLuong,
            daKySo = cv.DaKySo,
            loaiChungThu = cv.LoaiChungThu,
            coFile = !string.IsNullOrEmpty(cv.FileDinhKem),
            urlTaiFile = string.IsNullOrEmpty(cv.FileDinhKem) ? null : $"/api/v1/vanban-cong-khai/{cv.MSCV?.Trim()}/tai-file",
            ngayCongKhai = cv.NgayCongKhai
        });
    }

    /// <summary>Tải file đính kèm của 1 văn bản đi công khai.</summary>
    [HttpGet("{id}/tai-file")]
    public async Task<IActionResult> TaiFile(string id)
    {
        var cv = await _db.GetCongVanDiCongKhaiByIdAsync(id.Trim());
        if (cv == null || string.IsNullOrEmpty(cv.FileDinhKem))
            return NotFound(new { error = "Không có file công khai cho văn bản này." });
        if (!_fileSvc.Exists(cv.FileDinhKem))
            return NotFound(new { error = "File không còn tồn tại trên server." });

        var tenFile = System.IO.Path.GetFileName(cv.FileDinhKem);
        var bytes = await System.IO.File.ReadAllBytesAsync(_fileSvc.GetFullPath(cv.FileDinhKem));
        return File(bytes, FileService.GetMimeType(tenFile), tenFile);
    }

    /// <summary>Danh mục Sổ văn bản + Loại văn bản (để trang công khai dựng bộ lọc).</summary>
    [HttpGet("danh-muc")]
    [ResponseCache(Duration = 600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> DanhMuc()
    {
        var so = await _db.GetSoCVAsync();
        var loai = await _db.GetLoaiVBAsync(chiHienThi: true);
        return Ok(new
        {
            soVanBan = so.Select(s => new { ma = s.MaSCV, ten = s.TenSCV }),
            loaiVanBan = loai.Select(l => new { ma = l.MaLVB, ten = l.TenLVB })
        });
    }
}
