using Dapper;
using CongVan.Models;

namespace CongVan.Services;

// Dự thảo văn bản đi (migration 038). Chuỗi trình/duyệt dùng VanBan_XuLy (LoaiVB=2) — xem DbService.HopThu.cs.
public partial class DbService
{
    private const string SelectDuThao = @"
        SELECT d.*, lv.TenLVB, nc.TenNCV, dv.TenDV AS TenDVSoan,
               (ns.HoNV+' '+ns.TenNV) AS TenNVSoan, (nk.HoNV+' '+nk.TenNV) AS TenNVKy, cvd.TrichYeu AS TrichYeuDen
        FROM DuThaoVanBanDi d
        LEFT JOIN LoaiVB lv ON d.MaLVB = lv.MaLVB
        LEFT JOIN NhomCV nc ON d.MaNCV = nc.MaNCV
        LEFT JOIN DonVi dv ON d.MaDVSoan = dv.MaDV
        LEFT JOIN NhanVien ns ON d.MaNVSoan = ns.MaNV
        LEFT JOIN NhanVien nk ON d.MaNVKy = nk.MaNV
        LEFT JOIN CongVanDen cvd ON d.MSCVDen = cvd.MSCV";

    public async Task<DuThao?> GetDuThaoAsync(int id)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<DuThao>(SelectDuThao + " WHERE d.ID=@id", new { id });
    }

    public async Task<List<DuThao>> GetDuThaoCuaToiAsync(short maNV, string? tuKhoa)
    {
        using var db = Open();
        var where = "WHERE d.MaNVSoan=@maNV";
        var p = new DynamicParameters(); p.Add("maNV", maNV);
        if (!string.IsNullOrWhiteSpace(tuKhoa)) { where += " AND d.TrichYeu LIKE @tk"; p.Add("tk", $"%{tuKhoa.Trim()}%"); }
        return (await db.QueryAsync<DuThao>($"{SelectDuThao} {where} ORDER BY d.NgayCapNhat DESC", p)).ToList();
    }

    public async Task<int> LuuDuThaoAsync(DuThao d)
    {
        using var db = Open();
        if (d.ID == 0)
            return await db.ExecuteScalarAsync<int>(
                @"INSERT INTO DuThaoVanBanDi (TrichYeu, MaLVB, DoKhan, MaNCV, MaDVSoan, MaNVSoan, HinhThuc, HanXuLy, MaNVKy, NoiDungXuLy, DonViNhan, NoiNhanKhac, MSCVDen, PhamVi, ChuoiDuyet)
                  OUTPUT INSERTED.ID
                  VALUES (@TrichYeu, @MaLVB, @DoKhan, @MaNCV, @MaDVSoan, @MaNVSoan, @HinhThuc, @HanXuLy, @MaNVKy, @NoiDungXuLy, @DonViNhan, @NoiNhanKhac, @MSCVDen, @PhamVi, @ChuoiDuyet)", d);
        await db.ExecuteAsync(
            @"UPDATE DuThaoVanBanDi SET TrichYeu=@TrichYeu, MaLVB=@MaLVB, DoKhan=@DoKhan, MaNCV=@MaNCV, HinhThuc=@HinhThuc,
                HanXuLy=@HanXuLy, MaNVKy=@MaNVKy, NoiDungXuLy=@NoiDungXuLy, DonViNhan=@DonViNhan, NoiNhanKhac=@NoiNhanKhac,
                MSCVDen=@MSCVDen, PhamVi=@PhamVi, ChuoiDuyet=@ChuoiDuyet, NgayCapNhat=GETDATE()
              WHERE ID=@ID", d);
        return d.ID;
    }

    public async Task XoaDuThaoAsync(int id)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM VanBan_XuLy WHERE LoaiVB=2 AND MSCV=@m; DELETE FROM VanBan_NhatKy WHERE LoaiVB=2 AND MSCV=@m; DELETE FROM DuThaoVanBanDi WHERE ID=@id",
            new { id, m = id.ToString() });
    }

    public async Task DatTrangThaiDuThaoAsync(int id, byte trangThai)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE DuThaoVanBanDi SET TrangThai=@trangThai, NgayCapNhat=GETDATE() WHERE ID=@id", new { id, trangThai });
    }

    public async Task<List<DuThaoFile>> GetFileDuThaoAsync(int maDT)
    {
        using var db = Open();
        return (await db.QueryAsync<DuThaoFile>("SELECT * FROM DuThao_File WHERE MaDT=@maDT ORDER BY ID", new { maDT })).ToList();
    }

    public async Task<int> ThemFileDuThaoAsync(int maDT, string tenFile, string duongDan)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<int>(
            "INSERT INTO DuThao_File (MaDT, TenFile, DuongDan) OUTPUT INSERTED.ID VALUES (@maDT, @tenFile, @duongDan)", new { maDT, tenFile, duongDan });
    }

    public async Task<DuThaoFile?> GetFileDuThaoByIdAsync(int id)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<DuThaoFile>("SELECT * FROM DuThao_File WHERE ID=@id", new { id });
    }

    public async Task XoaFileDuThaoAsync(int id)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM DuThao_File WHERE ID=@id", new { id });
    }

    // Lãnh đạo phụ trách đơn vị (DonVi.MaNV_TDV) — người duyệt đầu tiên của dự thảo.
    public async Task<NhanVien?> GetLanhDaoDonViAsync(byte maDV)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<NhanVien>(
            @"SELECT nv.*, dv.TenDV FROM DonVi dv JOIN NhanVien nv ON dv.MaNV_TDV = nv.MaNV
              WHERE dv.MaDV=@maDV AND nv.NgayNghiViec IS NULL", new { maDV });
    }

    // Đã ban hành: gắn số văn bản đi sinh ra, đóng mọi dòng xử lý còn treo.
    public async Task DanhDauDuThaoDaBanHanhAsync(int id, string mscvDi, short maNV)
    {
        using var db = Open();
        await db.ExecuteAsync(
            @"UPDATE DuThaoVanBanDi SET TrangThai=3, MSCVDi=@mscvDi, NgayBanHanh=GETDATE(), MaNVBanHanh=@maNV, NgayCapNhat=GETDATE() WHERE ID=@id;
              UPDATE VanBan_XuLy SET TrangThai=3, NgayXuLy=GETDATE(), NgayXem=ISNULL(NgayXem,GETDATE())
                WHERE LoaiVB=2 AND MSCV=@m AND TrangThai IN (0,1);
              INSERT INTO VanBan_NhatKy (LoaiVB, MSCV, MaNV, HanhDong, NoiDung) VALUES (2, @m, @maNV, N'KetThuc', N'Đã ban hành — số ' + @mscvDi);",
            new { id, mscvDi, maNV, m = id.ToString() });
    }

    // Người soạn giữ 1 dòng xử lý của chính mình (vai trò chính) để chuỗi trình/trả lại hoạt động:
    // khi lãnh đạo trả lại, dòng này được mở lại ở hộp thư của người soạn.
    public async Task<int> LayHoacTaoDongNguoiSoanAsync(int maDT, short maNV)
    {
        using var db = Open();
        var m = maDT.ToString();
        var id = await db.QueryFirstOrDefaultAsync<int?>(
            "SELECT TOP 1 ID FROM VanBan_XuLy WHERE LoaiVB=2 AND MSCV=@m AND MaNVNhan=@maNV AND VaiTro=1 AND TrangThai IN (0,1) ORDER BY ID DESC",
            new { m, maNV });
        if (id.HasValue) return id.Value;
        return await db.ExecuteScalarAsync<int>(
            @"INSERT INTO VanBan_XuLy (LoaiVB, MSCV, MaNVNhan, MaNVGui, VaiTro, TrangThai, NoiDungGui, NgayXem)
              OUTPUT INSERTED.ID VALUES (2, @m, @maNV, NULL, 1, 1, N'Soạn thảo', GETDATE())", new { m, maNV });
    }

    // Văn bản đi này sinh ra từ dự thảo do chính người này soạn / xử lý → được xem dù chưa công khai.
    public async Task<bool> DuThaoCuaNguoiNayAsync(string mscvDi, short maNV)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<int>(
            @"SELECT COUNT(1) FROM DuThaoVanBanDi d WHERE d.MSCVDi=@mscvDi AND (d.MaNVSoan=@maNV
                OR EXISTS(SELECT 1 FROM VanBan_XuLy x WHERE x.LoaiVB=2 AND x.MSCV=CAST(d.ID AS nvarchar(20)) AND x.MaNVNhan=@maNV))",
            new { mscvDi, maNV }) > 0;
    }

    public async Task DatChuoiDuyetAsync(int id, string? csv)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE DuThaoVanBanDi SET ChuoiDuyet=@csv, NgayCapNhat=GETDATE() WHERE ID=@id", new { id, csv });
    }

    // Đã ban hành NỘI BỘ đơn vị: gắn văn bản nội bộ sinh ra, đóng mọi dòng xử lý còn treo.
    public async Task DanhDauDuThaoDaBanHanhNoiBoAsync(int id, int maVBNoiBo, short maNV)
    {
        using var db = Open();
        await db.ExecuteAsync(
            @"UPDATE DuThaoVanBanDi SET TrangThai=3, MaVBNoiBo=@maVBNoiBo, NgayBanHanh=GETDATE(), MaNVBanHanh=@maNV, NgayCapNhat=GETDATE() WHERE ID=@id;
              UPDATE VanBan_XuLy SET TrangThai=3, NgayXuLy=GETDATE(), NgayXem=ISNULL(NgayXem,GETDATE())
                WHERE LoaiVB=2 AND MSCV=@m AND TrangThai IN (0,1);
              INSERT INTO VanBan_NhatKy (LoaiVB, MSCV, MaNV, HanhDong, NoiDung) VALUES (2, @m, @maNV, N'KetThuc', N'Đã ban hành nội bộ đơn vị');",
            new { id, maVBNoiBo, maNV, m = id.ToString() });
    }
}
