using Dapper;
using CongVan.Models;

namespace CongVan.Services;

// Hộp thư cá nhân — xử lý văn bản theo TỪNG NGƯỜI (xem migrations/037_hop_thu_ca_nhan.sql).
public partial class DbService
{
    private const string SelectXuLy = @"
        SELECT x.*, (nn.HoNV+' '+nn.TenNV) AS TenNVNhan, (ng.HoNV+' '+ng.TenNV) AS TenNVGui,
               COALESCE(cvd.TrichYeu, dt.TrichYeu) AS TrichYeu, COALESCE(cvd.STT1, N'Dự thảo #' + CAST(dt.ID AS nvarchar(20))) AS SoKyHieu,
               lv.TenLVB, cq.TenCQ, COALESCE(cvd.NgayBanHanh, dt.NgayTao) AS NgayVB
        FROM VanBan_XuLy x
        JOIN NhanVien nn ON x.MaNVNhan = nn.MaNV
        LEFT JOIN NhanVien ng ON x.MaNVGui = ng.MaNV
        LEFT JOIN CongVanDen cvd ON x.LoaiVB = 1 AND cvd.MSCV = x.MSCV
        LEFT JOIN DuThaoVanBanDi dt ON x.LoaiVB = 2 AND CAST(dt.ID AS nvarchar(20)) = x.MSCV
        LEFT JOIN LoaiVB lv ON lv.MaLVB = COALESCE(cvd.MaLVB, dt.MaLVB)
        LEFT JOIN CoQuan cq ON cvd.MaCQ = cq.MaCQ";

    private const string DieuKienDangCho =
        "((x.VaiTro IN (1,2) AND x.TrangThai IN (0,1)) OR (x.VaiTro=3 AND x.TrangThai=0))";

    private static string TenVaiTroXuLy(byte vaiTro) => vaiTro switch { 1 => "Xử lý chính", 2 => "Đồng xử lý", _ => "Xem để biết" };

    // tab: "cho" = chờ xử lý (chính/đồng xử lý), "da" = đã xử lý, "xembiet" = đồng gửi xem để biết.
    public async Task<(List<VanBanXuLy> Items, int Total)> GetHopThuAsync(short maNV, string tab, string? tuKhoa, int page, int pageSize, byte? loaiVB = null)
    {
        using var db = Open();
        var where = tab switch
        {
            "da" => "x.VaiTro IN (1,2) AND x.TrangThai IN (2,3,4)",
            "xembiet" => "x.VaiTro = 3 AND x.TrangThai <> 5",
            _ => "x.VaiTro IN (1,2) AND x.TrangThai IN (0,1)"
        };
        var p = new DynamicParameters();
        p.Add("maNV", maNV);
        if (loaiVB.HasValue) { where += " AND x.LoaiVB=@loaiVB"; p.Add("loaiVB", loaiVB); }
        var kw = "";
        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            kw = " AND (COALESCE(cvd.TrichYeu, dt.TrichYeu) LIKE @tk OR cvd.STT1 LIKE @tk OR cq.TenCQ LIKE @tk)";
            p.Add("tk", $"%{tuKhoa.Trim()}%");
        }
        var from = $"{SelectXuLy} WHERE x.MaNVNhan=@maNV AND {where}{kw}";
        var total = await db.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM ({from}) t", p);
        p.Add("skip", (Math.Max(page, 1) - 1) * pageSize);
        p.Add("size", pageSize);
        var items = (await db.QueryAsync<VanBanXuLy>(
            from + " ORDER BY x.NgayGui DESC OFFSET @skip ROWS FETCH NEXT @size ROWS ONLY", p)).ToList();
        return (items, total);
    }

    public async Task<int> DemChoTheoLoaiAsync(short maNV, byte loaiVB)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM VanBan_XuLy WHERE MaNVNhan=@maNV AND LoaiVB=@loaiVB AND VaiTro IN (1,2) AND TrangThai IN (0,1)", new { maNV, loaiVB });
    }

    public async Task<(int Cho, int XemBiet)> DemHopThuAsync(short maNV)
    {
        using var db = Open();
        var cho = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM VanBan_XuLy WHERE MaNVNhan=@maNV AND VaiTro IN (1,2) AND TrangThai IN (0,1)", new { maNV });
        var xb = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM VanBan_XuLy WHERE MaNVNhan=@maNV AND VaiTro=3 AND TrangThai=0", new { maNV });
        return (cho, xb);
    }

    public async Task<List<VanBanXuLy>> GetXuLyCuaVanBanAsync(byte loaiVB, string mscv)
    {
        using var db = Open();
        return (await db.QueryAsync<VanBanXuLy>(
            SelectXuLy + " WHERE x.LoaiVB=@loaiVB AND x.MSCV=@mscv ORDER BY x.NgayGui, x.ID", new { loaiVB, mscv })).ToList();
    }

    public async Task<List<VanBanNhatKy>> GetNhatKyVanBanAsync(byte loaiVB, string mscv)
    {
        using var db = Open();
        return (await db.QueryAsync<VanBanNhatKy>(
            @"SELECT k.*, (a.HoNV+' '+a.TenNV) AS TenNV, (b.HoNV+' '+b.TenNV) AS TenNVLienQuan
              FROM VanBan_NhatKy k
              JOIN NhanVien a ON k.MaNV=a.MaNV
              LEFT JOIN NhanVien b ON k.MaNVLienQuan=b.MaNV
              WHERE k.LoaiVB=@loaiVB AND k.MSCV=@mscv ORDER BY k.Ngay, k.ID", new { loaiVB, mscv })).ToList();
    }

    // Dòng đang chờ của người này với văn bản này (null nếu không có).
    public async Task<VanBanXuLy?> GetDongDangChoAsync(byte loaiVB, string mscv, short maNV)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<VanBanXuLy>(
            SelectXuLy + $" WHERE x.LoaiVB=@loaiVB AND x.MSCV=@mscv AND x.MaNVNhan=@maNV AND {DieuKienDangCho} ORDER BY x.ID DESC",
            new { loaiVB, mscv, maNV });
    }

    // Người này có dính líu gì tới văn bản (đã/đang được chuyển) không — dùng cho quyền xem.
    public async Task<bool> CoLienQuanVanBanAsync(byte loaiVB, string mscv, short maNV)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM VanBan_XuLy WHERE LoaiVB=@loaiVB AND MSCV=@mscv AND MaNVNhan=@maNV AND TrangThai<>5",
            new { loaiVB, mscv, maNV }) > 0;
    }

    private static async Task GhiNhatKyAsync(Microsoft.Data.SqlClient.SqlConnection db, Microsoft.Data.SqlClient.SqlTransaction? tx,
        byte loaiVB, string mscv, short maNV, string hanhDong, string? noiDung, short? maNVLienQuan = null)
    {
        await db.ExecuteAsync(
            @"INSERT INTO VanBan_NhatKy (LoaiVB, MSCV, MaNV, HanhDong, NoiDung, MaNVLienQuan)
              VALUES (@loaiVB, @mscv, @maNV, @hanhDong, @noiDung, @maNVLienQuan)",
            new { loaiVB, mscv, maNV, hanhDong, noiDung, maNVLienQuan }, tx);
    }

    // Chuyển văn bản cho nhiều người (xử lý chính / đồng xử lý / xem để biết).
    // idDongCuaToi != null: đây là CHUYỂN TIẾP từ 1 dòng đang giữ — dòng đó chuyển sang "Đã chuyển tiếp".
    // Trả về số người đã nhận (bỏ qua người đang giữ sẵn văn bản này).
    public async Task<int> ChuyenXuLyAsync(byte loaiVB, string mscv, short maNVGui, List<NguoiNhanXuLy> nguoiNhan,
        string? noiDung, DateTime? hanXuLy, int? idDongCuaToi)
    {
        using var db = Open();
        await db.OpenAsync();
        using var tx = db.BeginTransaction();
        int soNguoi = 0;
        foreach (var n in nguoiNhan.GroupBy(x => x.MaNV).Select(g => g.OrderBy(x => x.VaiTro).First()))
        {
            if (n.MaNV == maNVGui) continue;
            // Đang chỉ "xem để biết" mà nay được giao xử lý → nâng vai trò (đóng dòng xem biết cũ).
            if (n.VaiTro < VanBanXuLy.VaiTroXemBiet)
                await db.ExecuteAsync(
                    @"UPDATE VanBan_XuLy SET TrangThai=3, NgayXem=ISNULL(NgayXem,GETDATE())
                      WHERE LoaiVB=@loaiVB AND MSCV=@mscv AND MaNVNhan=@ma AND VaiTro=3 AND TrangThai=0",
                    new { loaiVB, mscv, ma = n.MaNV }, tx);
            var daCo = await db.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1) FROM VanBan_XuLy x WHERE x.LoaiVB=@loaiVB AND x.MSCV=@mscv AND x.MaNVNhan=@ma AND " + DieuKienDangCho,
                new { loaiVB, mscv, ma = n.MaNV }, tx);
            if (daCo > 0) continue;
            await db.ExecuteAsync(
                @"INSERT INTO VanBan_XuLy (LoaiVB, MSCV, MaNVNhan, MaNVGui, VaiTro, TrangThai, NoiDungGui, HanXuLy, IDCha)
                  VALUES (@loaiVB, @mscv, @ma, @maNVGui, @vaiTro, 0, @noiDung, @hanXuLy, @idDongCuaToi)",
                new { loaiVB, mscv, ma = n.MaNV, maNVGui, vaiTro = n.VaiTro, noiDung, hanXuLy, idDongCuaToi }, tx);
            await GhiNhatKyAsync(db, tx, loaiVB, mscv, maNVGui, idDongCuaToi.HasValue ? "ChuyenTiep" : "ChuyenXuLy",
                $"[{TenVaiTroXuLy(n.VaiTro)}] {noiDung}".Trim(), n.MaNV);
            soNguoi++;
        }
        if (idDongCuaToi.HasValue && soNguoi > 0)
            await db.ExecuteAsync(
                @"UPDATE VanBan_XuLy SET TrangThai=2, NgayXuLy=GETDATE(), NgayXem=ISNULL(NgayXem,GETDATE()),
                    YKien=ISNULL(NULLIF(@noiDung,''), YKien) WHERE ID=@idDongCuaToi AND MaNVNhan=@maNVGui",
                new { idDongCuaToi, maNVGui, noiDung }, tx);
        tx.Commit();
        return soNguoi;
    }

    // Mở xem: đánh dấu đã xem; dòng "xem để biết" coi là xử lý xong khi đã mở.
    public async Task MoXemXuLyAsync(int id, short maNV)
    {
        using var db = Open();
        await db.ExecuteAsync(
            @"UPDATE VanBan_XuLy SET NgayXem=ISNULL(NgayXem,GETDATE()),
                TrangThai = CASE WHEN VaiTro=3 AND TrangThai=0 THEN 3 WHEN TrangThai=0 THEN 1 ELSE TrangThai END
              WHERE ID=@id AND MaNVNhan=@maNV", new { id, maNV });
    }

    private static async Task<VanBanXuLy?> LayDongCuaAsync(Microsoft.Data.SqlClient.SqlConnection db, Microsoft.Data.SqlClient.SqlTransaction? tx, int id, short maNV)
        => await db.QueryFirstOrDefaultAsync<VanBanXuLy>(
            "SELECT * FROM VanBan_XuLy WHERE ID=@id AND MaNVNhan=@maNV AND TrangThai IN (0,1)", new { id, maNV }, tx);

    // Đồng xử lý cho ý kiến — dòng chuyển sang đã xử lý.
    public async Task<bool> ChoYKienAsync(int id, short maNV, string yKien)
    {
        using var db = Open();
        var x = await LayDongCuaAsync(db, null, id, maNV);
        if (x == null) return false;
        await db.ExecuteAsync(
            "UPDATE VanBan_XuLy SET TrangThai=2, YKien=@yKien, NgayXuLy=GETDATE(), NgayXem=ISNULL(NgayXem,GETDATE()) WHERE ID=@id",
            new { id, yKien });
        await GhiNhatKyAsync(db, null, x.LoaiVB, x.MSCV, maNV, "YKien", yKien, x.MaNVGui);
        return true;
    }

    // Trả lại người đã chuyển: dòng của mình → Đã trả lại; dòng đã chuyển tiếp của người gửi được mở lại.
    public async Task<bool> TraLaiXuLyAsync(int id, short maNV, string lyDo)
    {
        using var db = Open();
        await db.OpenAsync();
        using var tx = db.BeginTransaction();
        var x = await LayDongCuaAsync(db, tx, id, maNV);
        if (x == null) { tx.Rollback(); return false; }
        await db.ExecuteAsync(
            "UPDATE VanBan_XuLy SET TrangThai=4, YKien=@lyDo, NgayXuLy=GETDATE(), NgayXem=ISNULL(NgayXem,GETDATE()) WHERE ID=@id",
            new { id, lyDo }, tx);
        if (x.IDCha.HasValue)
            await db.ExecuteAsync(
                @"UPDATE VanBan_XuLy SET TrangThai=0, NgayXuLy=NULL, NoiDungGui = N'[Bị trả lại] ' + @lyDo
                  WHERE ID=@idCha AND TrangThai=2",
                new { idCha = x.IDCha.Value, lyDo }, tx);
        await GhiNhatKyAsync(db, tx, x.LoaiVB, x.MSCV, maNV, "TraLai", lyDo, x.MaNVGui);
        tx.Commit();
        return true;
    }

    // Người xử lý chính kết thúc xử lý, ghi kết quả.
    public async Task<bool> KetThucXuLyAsync(int id, short maNV, string ketQua)
    {
        using var db = Open();
        var x = await LayDongCuaAsync(db, null, id, maNV);
        if (x == null || x.VaiTro != VanBanXuLy.VaiTroChinh) return false;
        await db.ExecuteAsync(
            "UPDATE VanBan_XuLy SET TrangThai=3, YKien=@ketQua, NgayXuLy=GETDATE(), NgayXem=ISNULL(NgayXem,GETDATE()) WHERE ID=@id",
            new { id, ketQua });
        await GhiNhatKyAsync(db, null, x.LoaiVB, x.MSCV, maNV, "KetThuc", ketQua);
        return true;
    }

    // Người gửi thu hồi văn bản khi người nhận chưa mở xem.
    public async Task<bool> ThuHoiXuLyAsync(int id, short maNV)
    {
        using var db = Open();
        var x = await db.QueryFirstOrDefaultAsync<VanBanXuLy>(
            "SELECT * FROM VanBan_XuLy WHERE ID=@id AND MaNVGui=@maNV AND TrangThai=0 AND NgayXem IS NULL", new { id, maNV });
        if (x == null) return false;
        await db.ExecuteAsync("UPDATE VanBan_XuLy SET TrangThai=5, NgayXuLy=GETDATE() WHERE ID=@id", new { id });
        await GhiNhatKyAsync(db, null, x.LoaiVB, x.MSCV, maNV, "ThuHoi", null, x.MaNVNhan);
        return true;
    }

    // Loại văn bản đánh dấu "dùng chung" mặc định + danh sách để admin cấu hình.
    public async Task<List<LoaiVB>> GetLoaiVBDungChungAsync()
    {
        using var db = Open();
        return (await db.QueryAsync<LoaiVB>("SELECT * FROM LoaiVB WHERE HienThi=1 ORDER BY TenLVB")).ToList();
    }

    public async Task SetLoaiVBDungChungAsync(short maLVB, bool dungChung)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE LoaiVB SET DungChung=@dungChung WHERE MaLVB=@maLVB", new { maLVB, dungChung });
    }

    public async Task<bool> LoaiVBDungChungMacDinhAsync(short maLVB)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM LoaiVB WHERE MaLVB=@maLVB AND DungChung=1", new { maLVB }) > 0;
    }

    public async Task DatDungChungCongVanDenAsync(string mscv, bool dungChung)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE CongVanDen SET DungChung=@dungChung WHERE MSCV=@mscv", new { mscv, dungChung });
    }

    // Người này là chủ trì/phối hợp của Công việc sinh ra từ văn bản đến này (đường giao việc cũ).
    public async Task<bool> LaNguoiXuLyCongViecCuaVanBanAsync(string mscv, short maNV)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<int>(
            @"SELECT COUNT(1) FROM CongViec WHERE LoaiNguonGoc=1 AND MSCVGoc=@mscv
              AND (MaNVChuTri=@maNV OR (',' + ISNULL(NguoiPhoiHop,'') + ',') LIKE '%,' + CAST(@maNV AS varchar) + ',%')",
            new { mscv, maNV }) > 0;
    }
}
