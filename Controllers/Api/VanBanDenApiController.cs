using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using CongVan.Models;
using CongVan.Services;

namespace CongVan.Controllers.Api;

// API cho web/hệ thống riêng của 1 ĐƠN VỊ đọc danh sách CÔNG VĂN ĐẾN mà đơn vị đó được giao xử lý
// (chủ trì hoặc phối hợp). Chỉ đọc (GET). Xác thực bằng API Key giống api/v1/vanbannoibo — mỗi key
// gắn với đúng 1 đơn vị, chỉ thấy công văn đến của đơn vị đó. Tài liệu: API_VANBAN_DEN.md
[ApiController]
[Route("api/v1/vanban-den")]
[EnableCors("ApiCors")]
[TypeFilter(typeof(ApiKeyAuthFilter))]
public class VanBanDenApiController : ControllerBase
{
    private readonly DbService _db;
    private readonly FileService _fileSvc;
    public VanBanDenApiController(DbService db, FileService fileSvc) { _db = db; _fileSvc = fileSvc; }

    private byte MaDV => ApiKeyAuthFilter.ApiMaDV(HttpContext);

    public record VanBanDenDto(
        string Id, string SoVanBan, string? SoDen, DateTime NgayDen, DateTime NgayBanHanh,
        string TrichYeu, string? CoQuanBanHanh, string? LoaiVanBan, string? NguoiKy,
        string? DonViXuLyChinh, DateTime? HanXuLy, DateTime? NgayHoanThanh,
        string TrangThai, string TrangThaiText, bool CoFile);

    public record TrangDto(int Total, int Page, int PageSize, IEnumerable<VanBanDenDto> Items);

    private static VanBanDenDto ToDto(CongVanDen cv) => new(
        cv.MSCV?.Trim() ?? "",
        string.IsNullOrWhiteSpace(cv.STT1) ? cv.STT.ToString("0") : cv.STT1!.Trim(),
        string.IsNullOrWhiteSpace(cv.SoCV) ? null : cv.SoCV.Trim(),
        cv.NgayDen, cv.NgayBanHanh, cv.TrichYeu,
        cv.TenCQ, cv.TenLVB, string.IsNullOrWhiteSpace(cv.NguoiKy) ? null : cv.NguoiKy,
        cv.TenDVXL, cv.NgayYCHT, cv.NgayHT ?? cv.NgayXNHTCV,
        cv.TrangThaiCode, cv.TrangThaiLabel, !string.IsNullOrEmpty(cv.FileDinhKem));

    private bool DonViDuocGiao(CongVanDen cv) =>
        cv.MaDVXL == MaDV ||
        (cv.BoPhanPhoiHop ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Any(x => byte.TryParse(x.Trim(), out var m) && m == MaDV);

    /// <summary>Kiểm tra key + đơn vị của key (không đụng dữ liệu).</summary>
    [HttpGet("whoami")]
    public async Task<IActionResult> WhoAmI()
    {
        var dv = await _db.GetDonViByIdAsync(MaDV);
        return Ok(new { maDV = MaDV, tenDV = dv?.TenDV });
    }

    /// <summary>Danh sách công văn đến giao cho đơn vị của key. Lọc: nam, q (trích yếu/số/cơ quan); phân trang.</summary>
    [HttpGet]
    public async Task<IActionResult> DanhSach(
        [FromQuery] int? nam, [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _db.GetCongVanDenPagedAsync(
            nam, null, q, filterMaDV: MaDV, maNVHienTai: null,
            maDVXL: null, nguoiKy: null, tuNgay: null, denNgay: null, page: page, pageSize: pageSize);
        return Ok(new TrangDto(total, page, pageSize, items.Select(ToDto)));
    }

    /// <summary>Chi tiết 1 công văn đến (id = mã số văn bản). 404 nếu không thuộc đơn vị của key.</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> ChiTiet(string id)
    {
        var cv = await _db.GetCongVanDenByIdAsync(id.Trim());
        if (cv == null || !DonViDuocGiao(cv))
            return NotFound(new { error = "Không tìm thấy công văn đến (hoặc không thuộc đơn vị của key)." });

        var xuLy = (await _db.GetXuLyDVAsync(cv.MSCV!.Trim())).FirstOrDefault(x => x.MaDV == MaDV);
        var files = await _db.GetFileDinhKemAsync(cv.MSCV!.Trim());

        return Ok(new
        {
            id = cv.MSCV?.Trim(),
            soVanBan = string.IsNullOrWhiteSpace(cv.STT1) ? cv.STT.ToString("0") : cv.STT1!.Trim(),
            soDen = string.IsNullOrWhiteSpace(cv.SoCV) ? null : cv.SoCV.Trim(),
            ngayDen = cv.NgayDen,
            ngayBanHanh = cv.NgayBanHanh,
            trichYeu = cv.TrichYeu,
            coQuanBanHanh = cv.TenCQ,
            loaiVanBan = cv.TenLVB,
            nguoiKy = string.IsNullOrWhiteSpace(cv.NguoiKy) ? null : cv.NguoiKy,
            ghiChu = cv.GhiChu,
            donViXuLyChinh = cv.TenDVXL,
            hanXuLy = cv.NgayYCHT,
            ngayHoanThanh = cv.NgayHT ?? cv.NgayXNHTCV,
            trangThai = cv.TrangThaiCode,
            trangThaiText = cv.TrangThaiLabel,
            xuLyCuaDonVi = xuLy == null ? null : new
            {
                vaiTro = xuLy.TenLoaiDV,
                trangThai = xuLy.TrangThai,
                trangThaiText = xuLy.TenTrangThai,
                ngayTiepNhan = xuLy.NgayTiepNhan,
                ngayHoanThanh = xuLy.NgayHoanThanh,
                ghiChu = xuLy.GhiChu
            },
            files = files.Select(f => new
            {
                id = f.ID,
                tenFile = f.TenFile,
                ngayUpload = f.NgayUpload,
                urlTaiFile = $"/api/v1/vanban-den/{cv.MSCV?.Trim()}/files/{f.ID}"
            })
        });
    }

    /// <summary>Tải 1 file đính kèm của công văn đến (chỉ khi công văn đó thuộc đơn vị của key).</summary>
    [HttpGet("{id}/files/{fileId:int}")]
    public async Task<IActionResult> TaiFile(string id, int fileId)
    {
        var cv = await _db.GetCongVanDenByIdAsync(id.Trim());
        if (cv == null || !DonViDuocGiao(cv))
            return NotFound(new { error = "Không tìm thấy công văn đến." });

        var f = (await _db.GetFileDinhKemAsync(cv.MSCV!.Trim())).FirstOrDefault(x => x.ID == fileId);
        if (f == null) return NotFound(new { error = "Không tìm thấy file." });
        if (!_fileSvc.Exists(f.DuongDan)) return NotFound(new { error = "File không còn tồn tại trên server." });

        var bytes = await System.IO.File.ReadAllBytesAsync(_fileSvc.GetFullPath(f.DuongDan));
        return File(bytes, FileService.GetMimeType(f.TenFile), f.TenFile);
    }
}
