using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using CongVan.Models;
using CongVan.Services;

namespace CongVan.Controllers.Api;

// API công khai cho web riêng của TỪNG ĐƠN VỊ đọc/ghi sổ văn bản nội bộ của chính đơn vị đó.
// Xác thực bằng API Key (header X-Api-Key, xem ApiKeyAuthFilter) — KHÔNG dùng session đăng nhập nên
// KHÔNG kế thừa BaseController. Mọi hành động đều tự khoanh vùng theo MaDV lấy từ key đã xác thực,
// không bao giờ tin MaDV do client gửi lên trong body/query — xem tài liệu đầy đủ ở API_VANBANNOIBO.md.
[ApiController]
[Route("api/v1/vanbannoibo")]
[EnableCors("ApiCors")]
[TypeFilter(typeof(ApiKeyAuthFilter))]
public class VanBanNoiBoApiController : ControllerBase
{
    private readonly DbService _db;
    private readonly FileService _fileSvc;
    public VanBanNoiBoApiController(DbService db, FileService fileSvc) { _db = db; _fileSvc = fileSvc; }

    private byte MaDV => ApiKeyAuthFilter.ApiMaDV(HttpContext);

    public record VanBanDto(int Id, int Stt, string? SoHieu, string TieuDe, string? NoiDung,
        DateTime NgayBanHanh, string? NguoiKy, byte TrangThai, string TrangThaiText, DateTime NgayTao, int SoFile);

    public record VanBanFileDto(int Id, string TenFile, DateTime NgayUpload);

    public record VanBanChiTietDto(int Id, int Stt, string? SoHieu, string TieuDe, string? NoiDung,
        DateTime NgayBanHanh, string? NguoiKy, byte TrangThai, string TrangThaiText, DateTime NgayTao, List<VanBanFileDto> Files);

    public record VanBanCreateRequest(string? SoHieu, string TieuDe, string? NoiDung, DateTime NgayBanHanh, string? NguoiKy, byte TrangThai = 1);
    public record VanBanUpdateRequest(string? SoHieu, string TieuDe, string? NoiDung, DateTime NgayBanHanh, string? NguoiKy, byte TrangThai);

    private static VanBanDto ToDto(VanBanNoiBo v) =>
        new(v.ID, v.STT, v.SoHieu, v.TieuDe, v.NoiDung, v.NgayBanHanh, v.NguoiKy, v.TrangThai, v.TenTrangThai, v.NgayTao, v.SoFile);

    // Kiểm tra nhanh key còn dùng được không, và đang đại diện cho đơn vị nào — không đụng dữ liệu thật.
    [HttpGet("whoami")]
    public async Task<IActionResult> WhoAmI()
    {
        var dv = await _db.GetDonViByIdAsync(MaDV);
        return Ok(new { maDV = MaDV, tenDV = dv?.TenDV });
    }

    [HttpGet]
    public async Task<IActionResult> DanhSach([FromQuery] int? nam, [FromQuery] string? tuKhoa)
    {
        var list = await _db.GetVanBanNoiBoAsync(MaDV, nam, tuKhoa);
        return Ok(list.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> ChiTiet(int id)
    {
        var v = await _db.GetVanBanNoiBoByIdAsync(id, MaDV);
        if (v == null) return NotFound(new { error = "Không tìm thấy văn bản." });
        var files = await _db.GetVanBanNoiBoFilesAsync(id);
        return Ok(new VanBanChiTietDto(v.ID, v.STT, v.SoHieu, v.TieuDe, v.NoiDung, v.NgayBanHanh, v.NguoiKy, v.TrangThai, v.TenTrangThai, v.NgayTao,
            files.Select(f => new VanBanFileDto(f.ID, f.TenFile, f.NgayUpload)).ToList()));
    }

    [HttpPost]
    public async Task<IActionResult> Tao([FromBody] VanBanCreateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.TieuDe)) return BadRequest(new { error = "Trường 'tieuDe' không được để trống." });

        var v = new VanBanNoiBo
        {
            MaDV = MaDV,
            STT = await _db.NextSttVanBanNoiBoAsync(MaDV, req.NgayBanHanh.Year),
            SoHieu = req.SoHieu,
            TieuDe = req.TieuDe,
            NoiDung = req.NoiDung,
            NgayBanHanh = req.NgayBanHanh,
            NguoiKy = req.NguoiKy,
            TrangThai = req.TrangThai,
            MaNVTao = 0, // không có người dùng thật đứng sau — tạo qua API
            NguonTao = 1
        };
        v.ID = await _db.ThemVanBanNoiBoAsync(v);
        var created = await _db.GetVanBanNoiBoByIdAsync(v.ID);
        return CreatedAtAction(nameof(ChiTiet), new { id = v.ID }, ToDto(created!));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Sua(int id, [FromBody] VanBanUpdateRequest req)
    {
        var v = await _db.GetVanBanNoiBoByIdAsync(id, MaDV);
        if (v == null) return NotFound(new { error = "Không tìm thấy văn bản." });
        if (string.IsNullOrWhiteSpace(req.TieuDe)) return BadRequest(new { error = "Trường 'tieuDe' không được để trống." });

        v.SoHieu = req.SoHieu;
        v.TieuDe = req.TieuDe;
        v.NoiDung = req.NoiDung;
        v.NgayBanHanh = req.NgayBanHanh;
        v.NguoiKy = req.NguoiKy;
        v.TrangThai = req.TrangThai;
        await _db.SuaVanBanNoiBoAsync(v);

        var updated = await _db.GetVanBanNoiBoByIdAsync(id);
        return Ok(ToDto(updated!));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Xoa(int id)
    {
        var v = await _db.GetVanBanNoiBoByIdAsync(id, MaDV);
        if (v == null) return NotFound(new { error = "Không tìm thấy văn bản." });

        foreach (var f in await _db.GetVanBanNoiBoFilesAsync(id))
            _fileSvc.Delete(f.DuongDan);
        await _db.XoaVanBanNoiBoAsync(id);
        return NoContent();
    }

    [HttpPost("{id:int}/files")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> TaiFileLen(int id, IFormFile file)
    {
        var v = await _db.GetVanBanNoiBoByIdAsync(id, MaDV);
        if (v == null) return NotFound(new { error = "Không tìm thấy văn bản." });
        if (file == null || file.Length == 0) return BadRequest(new { error = "Thiếu file (multipart/form-data, field 'file')." });

        var rel = await _fileSvc.SaveAsync(file, "vanbannoibo");
        var fileId = await _db.ThemVanBanNoiBoFileAsync(id, file.FileName, rel);
        return CreatedAtAction(nameof(TaiFileXuong), new { id, fileId }, new VanBanFileDto(fileId, file.FileName, DateTime.Now));
    }

    [HttpGet("{id:int}/files/{fileId:int}")]
    public async Task<IActionResult> TaiFileXuong(int id, int fileId)
    {
        var v = await _db.GetVanBanNoiBoByIdAsync(id, MaDV);
        if (v == null) return NotFound(new { error = "Không tìm thấy văn bản." });
        var f = await _db.GetVanBanNoiBoFileByIdAsync(fileId);
        if (f == null || f.VanBanID != id) return NotFound(new { error = "Không tìm thấy file." });
        if (!_fileSvc.Exists(f.DuongDan)) return NotFound(new { error = "File không còn tồn tại trên server." });

        var bytes = await System.IO.File.ReadAllBytesAsync(_fileSvc.GetFullPath(f.DuongDan));
        return File(bytes, FileService.GetMimeType(f.TenFile), f.TenFile);
    }

    [HttpDelete("{id:int}/files/{fileId:int}")]
    public async Task<IActionResult> XoaFile(int id, int fileId)
    {
        var v = await _db.GetVanBanNoiBoByIdAsync(id, MaDV);
        if (v == null) return NotFound(new { error = "Không tìm thấy văn bản." });
        var f = await _db.GetVanBanNoiBoFileByIdAsync(fileId);
        if (f == null || f.VanBanID != id) return NotFound(new { error = "Không tìm thấy file." });

        _fileSvc.Delete(f.DuongDan);
        await _db.XoaVanBanNoiBoFileAsync(fileId);
        return NoContent();
    }
}
