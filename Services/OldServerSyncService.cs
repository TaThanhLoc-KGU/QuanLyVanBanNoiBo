using Dapper;
using Microsoft.Data.SqlClient;

namespace CongVan.Services;

/// <summary>
/// Đồng bộ tạm thời (một chiều, chỉ đọc từ server cũ) trong giai đoạn văn thư còn dùng song song
/// server cũ + server mới để đánh giá. Bỏ hẳn service này sau khi chính thức chuyển hẳn qua server mới.
/// Chỉ chạy khi có cấu hình OldServerSync:ConnectionString (env OldServerSync__ConnectionString) —
/// không cấu hình thì tự đứng yên, không ảnh hưởng gì đến các môi trường khác (dev, v.v.).
/// </summary>
public class OldServerSyncService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly ILogger<OldServerSyncService> _logger;

    public OldServerSyncService(IConfiguration config, ILogger<OldServerSyncService> logger)
    {
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var oldConnStr = _config["OldServerSync:ConnectionString"];
        if (string.IsNullOrWhiteSpace(oldConnStr))
        {
            _logger.LogInformation("OldServerSync: chua cau hinh OldServerSync:ConnectionString -> khong chay.");
            return;
        }
        var newConnStr = _config.GetConnectionString("CongVanConnection")!;
        var intervalMinutes = _config.GetValue<int?>("OldServerSync:IntervalMinutes") ?? 5;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncOnceAsync(oldConnStr, newConnStr, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OldServerSync: loi khi dong bo mot chu ky.");
            }

            try { await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken); }
            catch (TaskCanceledException) { }
        }
    }

    private async Task SyncOnceAsync(string oldConnStr, string newConnStr, CancellationToken ct)
    {
        using var old = new SqlConnection(oldConnStr);
        using var dst = new SqlConnection(newConnStr);
        await old.OpenAsync(ct);
        await dst.OpenAsync(ct);

        int nNv = 0, nDen = 0, nDi = 0, nDv = 0, nXl = 0, nFile = 0, nQtxl = 0, nDaXem = 0, nDiOut = 0, nDenOut = 0, nCat = 0;
        // Danh mục tra cứu (cơ quan ban hành, nhóm công văn) PHẢI đồng bộ TRƯỚC CongVanDen/CongVanDi —
        // nếu server mới thiếu cơ quan ban hành thì dòng công văn tham chiếu tới nó bị lỗi FK và bị bỏ qua.
        try { nCat = await SyncDanhMucAsync(old, dst); } catch (Exception ex) { _logger.LogError(ex, "OldServerSync: loi dong bo danh muc"); }
        try { nNv = await SyncNhanVienAsync(old, dst); } catch (Exception ex) { _logger.LogError(ex, "OldServerSync: loi bang NhanVien"); }
        try { nDen = await SyncCongVanDenAsync(old, dst); } catch (Exception ex) { _logger.LogError(ex, "OldServerSync: loi bang CongVanDen"); }
        try { nDi = await SyncCongVanDiAsync(old, dst); } catch (Exception ex) { _logger.LogError(ex, "OldServerSync: loi bang CongVanDi"); }
        // Đẩy NGƯỢC văn bản đi nhập trên server mới sang server cũ (CHỈ INSERT bản chưa có — không sửa,
        // không xóa dòng cũ). Tắt bằng OldServerSync:WriteBackCongVanDi=false nếu cần.
        if (_config.GetValue<bool?>("OldServerSync:WriteBackCongVanDi") ?? true)
        {
            try { nDiOut = await SyncCongVanDiToOldAsync(old, dst); }
            catch (Exception ex) { _logger.LogError(ex, "OldServerSync: loi ghi nguoc CongVanDi -> server cu"); }
        }
        // Ghi ngược VĂN BẢN ĐẾN nhập trên server mới sang server cũ. Tắt: OldServerSync:WriteBackCongVanDen=false.
        if (_config.GetValue<bool?>("OldServerSync:WriteBackCongVanDen") ?? true)
        {
            try { nDenOut = await SyncCongVanDenToOldAsync(old, dst); }
            catch (Exception ex) { _logger.LogError(ex, "OldServerSync: loi ghi nguoc CongVanDen -> server cu"); }
        }
        try { nDv = await SyncCvDenDvAsync(old, dst); } catch (Exception ex) { _logger.LogError(ex, "OldServerSync: loi bang CVDen_DV"); }
        try { nXl = await SyncCongVanDenXuLyDVAsync(old, dst); } catch (Exception ex) { _logger.LogError(ex, "OldServerSync: loi bang CongVanDenXuLyDV"); }
        try { nFile = await SyncCvDenFileAsync(old, dst); } catch (Exception ex) { _logger.LogError(ex, "OldServerSync: loi bang CVDenFile"); }
        try { nQtxl = await SyncCvDenQtxlAsync(old, dst); } catch (Exception ex) { _logger.LogError(ex, "OldServerSync: loi bang CVDen_QTXL"); }
        try { nDaXem = await SyncCvDenDaXemAsync(old, dst); } catch (Exception ex) { _logger.LogError(ex, "OldServerSync: loi bang CVDen_DaXem"); }

        if (nCat + nNv + nDen + nDi + nDv + nXl + nFile + nQtxl + nDaXem + nDiOut + nDenOut > 0)
            _logger.LogInformation(
                "OldServerSync: DanhMuc={Cat} NhanVien={NV} CongVanDen={Den} CongVanDi={Di} ->cu(Den={DenOut},Di={DiOut}) CVDen_DV={Dv} CongVanDenXuLyDV={Xl} CVDenFile={File} CVDen_QTXL={Qtxl} CVDen_DaXem={DaXem}",
                nCat, nNv, nDen, nDi, nDenOut, nDiOut, nDv, nXl, nFile, nQtxl, nDaXem);
    }

    // Đồng bộ danh mục tra cứu OLD -> NEW: CHỈ THÊM bản ghi mới theo khóa chính (không sửa, không xóa).
    // MaCQ / MaNCV không phải cột IDENTITY nên chèn thẳng khóa gốc, giữ đúng mã để CongVanDen khớp FK.
    private async Task<int> SyncDanhMucAsync(SqlConnection old, SqlConnection dst)
    {
        int n = 0;

        // CoQuan (cơ quan ban hành) — nguồn hay bị lệch nhất vì văn thư thêm cơ quan mới liên tục.
        var coQuan = await old.QueryAsync("SELECT MaCQ, TenCQ, HienThi FROM CoQuan");
        foreach (var r in coQuan)
        {
            try
            {
                var affected = await dst.ExecuteAsync(
                    "IF NOT EXISTS(SELECT 1 FROM CoQuan WHERE MaCQ=@MaCQ) " +
                    "INSERT INTO CoQuan (MaCQ,TenCQ,HienThi) VALUES (@MaCQ,@TenCQ,@HienThi)",
                    new { r.MaCQ, r.TenCQ, r.HienThi });
                if (affected > 0) n++;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "OldServerSync: bo qua CoQuan MaCQ={MaCQ}", (object)r.MaCQ); }
        }

        // NhomCV (nhóm công văn) — CongVanDen.MaNCV tham chiếu tới.
        var nhomCv = await old.QueryAsync("SELECT MaNCV, TenNCV FROM NhomCV");
        foreach (var r in nhomCv)
        {
            try
            {
                var affected = await dst.ExecuteAsync(
                    "IF NOT EXISTS(SELECT 1 FROM NhomCV WHERE MaNCV=@MaNCV) " +
                    "INSERT INTO NhomCV (MaNCV,TenNCV) VALUES (@MaNCV,@TenNCV)",
                    new { r.MaNCV, r.TenNCV });
                if (affected > 0) n++;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "OldServerSync: bo qua NhomCV MaNCV={MaNCV}", (object)r.MaNCV); }
        }

        return n;
    }

    // Chi them nhan vien MOI (khong ghi de nhan vien da co) — tranh de mat khau/thong tin da doi
    // rieng ben server moi bi mat khau cu tren server cu ghi de lai moi 5 phut.
    private async Task<int> SyncNhanVienAsync(SqlConnection old, SqlConnection dst)
    {
        var rows = await old.QueryAsync(
            "SELECT MaNV,HoNV,TenNV,Email,MatKhau,AnhNV,MaDV,username FROM NhanVien");
        int n = 0;
        foreach (var r in rows)
        {
            try
            {
                var affected = await dst.ExecuteAsync(
                    "IF NOT EXISTS(SELECT 1 FROM NhanVien WHERE MaNV=@MaNV) " +
                    "INSERT INTO NhanVien (MaNV,HoNV,TenNV,Email,MatKhau,AnhNV,MaDV,username) " +
                    "VALUES (@MaNV,@HoNV,@TenNV,@Email,@MatKhau,@AnhNV,@MaDV,@username)",
                    new { r.MaNV, r.HoNV, r.TenNV, r.Email, r.MatKhau, r.AnhNV, r.MaDV, r.username });
                if (affected > 0) n++;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "OldServerSync: bo qua NhanVien MaNV={MaNV}", (object)r.MaNV); }
        }
        return n;
    }

    private async Task<int> SyncCongVanDenAsync(SqlConnection old, SqlConnection dst)
    {
        var rows = await old.QueryAsync(
            "SELECT MSCV,SoCV,NgayDen,STT,STT1,MaCQ,NgayBanHanh,TrichYeu,NguoiKy,FileDinhKem,GhiChu,MaLVB," +
            "MaSCV,MaNCV,MaLDXem,MaDVXL,BoPhanPhoiHop,NgayGiao,NgayYCHT,NgayHT,MaVT,NgayNhap,MaNVXNHTCV," +
            "NgayXNHTCV,KQXN,MSCVDi FROM CongVanDen");
        int n = 0;
        // CHỈ INSERT dòng chưa có (không UPDATE) — server mới là nguồn chính, không để dữ liệu cũ đè
        // lên bản đã sửa trên server mới; đồng thời tránh vòng lặp đè nhau với chiều ghi ngược.
        const string sql =
            "MERGE CongVanDen AS tgt USING (SELECT CAST(@MSCV AS nchar(10)) AS MSCV) AS src ON tgt.MSCV=src.MSCV " +
            "WHEN NOT MATCHED THEN INSERT (MSCV,SoCV,NgayDen,STT,STT1,MaCQ,NgayBanHanh,TrichYeu,NguoiKy," +
            "FileDinhKem,GhiChu,MaLVB,MaSCV,MaNCV,MaLDXem,MaDVXL,BoPhanPhoiHop,NgayGiao,NgayYCHT,NgayHT,MaVT," +
            "NgayNhap,MaNVXNHTCV,NgayXNHTCV,KQXN,MSCVDi) VALUES (@MSCV,@SoCV,@NgayDen,@STT,@STT1,@MaCQ," +
            "@NgayBanHanh,@TrichYeu,@NguoiKy,@FileDinhKem,@GhiChu,@MaLVB,@MaSCV,@MaNCV,@MaLDXem,@MaDVXL," +
            "@BoPhanPhoiHop,@NgayGiao,@NgayYCHT,@NgayHT,@MaVT,@NgayNhap,@MaNVXNHTCV,@NgayXNHTCV,@KQXN,@MSCVDi);";
        foreach (var r in rows)
        {
            try
            {
                int stt = (int)(double)r.STT;
                var affected = await dst.ExecuteAsync(sql, new
                {
                    MSCV = (string)r.MSCV, r.SoCV, r.NgayDen, STT = stt, r.STT1, r.MaCQ, r.NgayBanHanh,
                    r.TrichYeu, r.NguoiKy, FileDinhKem = ChuanHoaDuongDan((string?)r.FileDinhKem), r.GhiChu, r.MaLVB, r.MaSCV, r.MaNCV, r.MaLDXem,
                    r.MaDVXL, r.BoPhanPhoiHop, r.NgayGiao, r.NgayYCHT, r.NgayHT, r.MaVT, r.NgayNhap,
                    r.MaNVXNHTCV, r.NgayXNHTCV, r.KQXN, r.MSCVDi
                });
                if (affected > 0) n++;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "OldServerSync: bo qua CongVanDen MSCV={MSCV}", (object)r.MSCV); }
        }
        return n;
    }

    private async Task<int> SyncCongVanDiAsync(SqlConnection old, SqlConnection dst)
    {
        var rows = await old.QueryAsync(
            "SELECT MSCV,STT,STT1,SoCVCT,NgayCongVan,NgayBanHanh,MaSCV,MaNCV,MaLVB,TrichYeu,MaLDKy,NoiNhanCV," +
            "MaVT,SoLuong,FileDinhKem,GhiChu,MSCVDen,NgayNhap FROM CongVanDi");
        int n = 0;
        // CHỈ INSERT dòng chưa có — server mới là nguồn chính (xem ghi chú ở SyncCongVanDenAsync).
        const string sql =
            "MERGE CongVanDi AS tgt USING (SELECT CAST(@MSCV AS nchar(10)) AS MSCV) AS src ON tgt.MSCV=src.MSCV " +
            "WHEN NOT MATCHED THEN INSERT (MSCV,STT,STT1,SoCVCT,NgayCongVan,NgayBanHanh,MaSCV,MaNCV,MaLVB," +
            "TrichYeu,MaLDKy,NoiNhanCV,MaVT,SoLuong,FileDinhKem,GhiChu,MSCVDen,NgayNhap) VALUES (@MSCV,@STT," +
            "@STT1,@SoCVCT,@NgayCongVan,@NgayBanHanh,@MaSCV,@MaNCV,@MaLVB,@TrichYeu,@MaLDKy,@NoiNhanCV,@MaVT," +
            "@SoLuong,@FileDinhKem,@GhiChu,@MSCVDen,@NgayNhap);";
        foreach (var r in rows)
        {
            try
            {
                int stt = (int)(double)r.STT;
                var affected = await dst.ExecuteAsync(sql, new
                {
                    MSCV = (string)r.MSCV, STT = stt, r.STT1, r.SoCVCT, r.NgayCongVan, r.NgayBanHanh, r.MaSCV,
                    r.MaNCV, r.MaLVB, r.TrichYeu, r.MaLDKy, r.NoiNhanCV, r.MaVT, r.SoLuong,
                    FileDinhKem = ChuanHoaDuongDan((string?)r.FileDinhKem),
                    r.GhiChu, r.MSCVDen, r.NgayNhap
                });
                if (affected > 0) n++;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "OldServerSync: bo qua CongVanDi MSCV={MSCV}", (object)r.MSCV); }
        }
        return n;
    }

    // ── Ghi NGƯỢC: văn bản đi nhập trên server MỚI -> server CŨ ────────────────────────────────
    // CHỈ INSERT bản MSCV chưa tồn tại bên cũ (IF NOT EXISTS) — không đụng 19 dòng cũ sẵn có, không
    // xóa. Bảng CongVanDi bên cũ có 5 khóa ngoại BẮT BUỘC (SoCV, LoaiVBCVDi chỉ 2 loại, NhomCV,
    // NhanVien x2) nên phải ánh xạ giá trị từ mới sang mã bên cũ THEO TÊN, không map được thì dùng
    // giá trị mặc định an toàn (loại "Công văn", sổ đầu tiên, người ký/văn thư -> văn thư -> MaNV nhỏ nhất).
    private async Task<int> SyncCongVanDiToOldAsync(SqlConnection old, SqlConnection dst)
    {
        var oldNvList = (await old.QueryAsync(
            "SELECT MaNV, LTRIM(RTRIM(ISNULL(HoNV,N'') + N' ' + ISNULL(TenNV,N''))) AS Ten FROM NhanVien")).ToList();
        var oldNvIds = oldNvList.Select(x => (int)x.MaNV).ToHashSet();
        var oldNvByName = new Dictionary<string, int>();
        foreach (var x in oldNvList)
        {
            var t = ((string?)x.Ten ?? "").ToLowerInvariant();
            if (t.Length > 0 && !oldNvByName.ContainsKey(t)) oldNvByName[t] = (int)x.MaNV;
        }
        int defaultNv = oldNvIds.Count > 0 ? oldNvIds.Min() : 1;

        var oldScv = new Dictionary<string, byte>();
        foreach (var x in await old.QueryAsync("SELECT MaSCV, LTRIM(RTRIM(TenSCV)) AS Ten FROM SoCV"))
            oldScv[((string?)x.Ten ?? "").ToLowerInvariant()] = (byte)x.MaSCV;
        byte defaultScv = oldScv.Values.DefaultIfEmpty((byte)1).First();

        var oldLvb = new Dictionary<string, short>();
        foreach (var x in await old.QueryAsync("SELECT MaLVB, LTRIM(RTRIM(TenLVB)) AS Ten FROM LoaiVBCVDi"))
            oldLvb[((string?)x.Ten ?? "").ToLowerInvariant()] = (short)x.MaLVB;
        short defaultLvb = oldLvb.TryGetValue("công văn", out var lvbCv) ? lvbCv : oldLvb.Values.DefaultIfEmpty((short)1).First();

        short oldNcv = (await old.QueryFirstOrDefaultAsync<short?>("SELECT TOP 1 MaNCV FROM NhomCV ORDER BY MaNCV")) ?? 1;

        int MapNv(int? maNV, string? ten)
        {
            if (maNV is > 0 && oldNvIds.Contains(maNV.Value)) return maNV.Value;
            if (!string.IsNullOrWhiteSpace(ten) && oldNvByName.TryGetValue(ten.Trim().ToLowerInvariant(), out var m)) return m;
            return defaultNv;
        }

        var rows = await dst.QueryAsync(@"
            SELECT cv.MSCV, cv.STT, cv.STT1, cv.SoCVCT, cv.NgayCongVan, cv.NgayBanHanh, cv.TrichYeu,
                   cv.NoiNhanCV, cv.SoLuong, cv.FileDinhKem, cv.GhiChu, cv.MSCVDen, cv.NgayNhap,
                   cv.MaLDKy, cv.MaVT,
                   sc.TenSCV, lv.TenLVB,
                   LTRIM(RTRIM(ISNULL(ld.HoNV,'') + ' ' + ISNULL(ld.TenNV,''))) AS TenLDKy,
                   LTRIM(RTRIM(ISNULL(vt.HoNV,'') + ' ' + ISNULL(vt.TenNV,''))) AS TenVT
            FROM CongVanDi cv
            LEFT JOIN SoCV sc     ON cv.MaSCV = sc.MaSCV
            LEFT JOIN LoaiVB lv   ON cv.MaLVB = lv.MaLVB
            LEFT JOIN NhanVien ld ON cv.MaLDKy = ld.MaNV
            LEFT JOIN NhanVien vt ON cv.MaVT = vt.MaNV");

        const string sql =
            "IF NOT EXISTS (SELECT 1 FROM CongVanDi WHERE MSCV = @MSCV) " +
            "INSERT INTO CongVanDi (MSCV,STT,STT1,SoCVCT,NgayCongVan,NgayBanHanh,MaSCV,MaNCV,MaLVB,TrichYeu," +
            "MaLDKy,NoiNhanCV,MaVT,SoLuong,FileDinhKem,GhiChu,MSCVDen,NgayNhap) " +
            "VALUES (@MSCV,@STT,@STT1,@SoCVCT,@NgayCongVan,@NgayBanHanh,@MaSCV,@MaNCV,@MaLVB,@TrichYeu," +
            "@MaLDKy,@NoiNhanCV,@MaVT,@SoLuong,@FileDinhKem,@GhiChu,@MSCVDen,@NgayNhap)";

        int n = 0;
        foreach (var r in rows)
        {
            try
            {
                var tenSCV = ((string?)r.TenSCV ?? "").Trim().ToLowerInvariant();
                var tenLVB = ((string?)r.TenLVB ?? "").Trim().ToLowerInvariant();
                int maVT = MapNv((int?)r.MaVT, (string?)r.TenVT);
                int? maLDKyRaw = r.MaLDKy is null ? null : (int?)Convert.ToInt32(r.MaLDKy);
                int maLDKy = maLDKyRaw is > 0 ? MapNv(maLDKyRaw, (string?)r.TenLDKy) : maVT;

                var affected = await old.ExecuteAsync(sql, new
                {
                    MSCV = ((string)r.MSCV).Trim(),
                    STT = Convert.ToDouble(r.STT),
                    r.STT1, r.SoCVCT, r.NgayCongVan, r.NgayBanHanh,
                    MaSCV = oldScv.TryGetValue(tenSCV, out var s) ? s : defaultScv,
                    MaNCV = oldNcv,
                    MaLVB = oldLvb.TryGetValue(tenLVB, out var l) ? l : defaultLvb,
                    r.TrichYeu,
                    MaLDKy = maLDKy,
                    r.NoiNhanCV,
                    MaVT = maVT,
                    SoLuong = Convert.ToInt16(r.SoLuong),
                    FileDinhKem = ThemTienToTaiNguyen((string?)r.FileDinhKem),
                    r.GhiChu, r.MSCVDen, r.NgayNhap
                });
                if (affected > 0) n++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OldServerSync: bo qua ghi nguoc CongVanDi MSCV={MSCV}", (object)r.MSCV);
            }
        }
        return n;
    }

    // ── Ghi NGƯỢC: VĂN BẢN ĐẾN nhập trên server MỚI -> server CŨ ─────────────────────────────
    // CHỈ INSERT bản MSCV chưa có bên cũ. Không đụng dòng cũ. Bỏ qua bản "mirror" từ văn bản đi
    // (MSCVDi khác rỗng — đã lo ở SyncCongVanDiToOldAsync). Kèm luôn đơn vị xử lý để bên cũ thấy
    // trong hàng chờ của đơn vị. CongVanDen bên cũ có 8 khóa ngoại — map theo TÊN, không map được
    // thì để NULL (nếu cột cho phép) hoặc dùng mặc định an toàn.
    private async Task<int> SyncCongVanDenToOldAsync(SqlConnection old, SqlConnection dst)
    {
        var oldNvList = (await old.QueryAsync(
            "SELECT MaNV, LTRIM(RTRIM(ISNULL(HoNV,N'') + N' ' + ISNULL(TenNV,N''))) AS Ten FROM NhanVien")).ToList();
        var oldNvIds = oldNvList.Select(x => (int)x.MaNV).ToHashSet();
        var oldNvByName = new Dictionary<string, int>();
        foreach (var x in oldNvList)
        {
            var t = ((string?)x.Ten ?? "").ToLowerInvariant();
            if (t.Length > 0 && !oldNvByName.ContainsKey(t)) oldNvByName[t] = (int)x.MaNV;
        }
        int defaultNv = oldNvIds.Count > 0 ? oldNvIds.Min() : 1;

        var oldScv = new Dictionary<string, byte>();
        foreach (var x in await old.QueryAsync("SELECT MaSCV, LTRIM(RTRIM(TenSCV)) AS Ten FROM SoCV"))
            oldScv[((string?)x.Ten ?? "").ToLowerInvariant()] = (byte)x.MaSCV;
        byte defaultScv = oldScv.Values.DefaultIfEmpty((byte)1).First();

        var oldLvb = new Dictionary<string, short>();
        foreach (var x in await old.QueryAsync("SELECT MaLVB, LTRIM(RTRIM(TenLVB)) AS Ten FROM LoaiVB"))
            oldLvb[((string?)x.Ten ?? "").ToLowerInvariant()] = (short)x.MaLVB;

        var oldCq = new Dictionary<string, short>();
        foreach (var x in await old.QueryAsync("SELECT MaCQ, LTRIM(RTRIM(TenCQ)) AS Ten FROM CoQuan"))
        {
            var t = ((string?)x.Ten ?? "").ToLowerInvariant();
            if (t.Length > 0 && !oldCq.ContainsKey(t)) oldCq[t] = (short)x.MaCQ;
        }
        var oldDvIds = (await old.QueryAsync<int>("SELECT MaDV FROM DonVi")).Select(x => (byte)x).ToHashSet();

        int MapNv(int? maNV, string? ten)
        {
            if (maNV is > 0 && oldNvIds.Contains(maNV.Value)) return maNV.Value;
            if (!string.IsNullOrWhiteSpace(ten) && oldNvByName.TryGetValue(ten.Trim().ToLowerInvariant(), out var m)) return m;
            return defaultNv;
        }
        int? MapNvNull(int? maNV, string? ten)
        {
            if (maNV is > 0 && oldNvIds.Contains(maNV.Value)) return maNV.Value;
            if (!string.IsNullOrWhiteSpace(ten) && oldNvByName.TryGetValue(ten.Trim().ToLowerInvariant(), out var m)) return m;
            return null;
        }

        var rows = (await dst.QueryAsync(@"
            SELECT cv.MSCV, cv.SoCV, cv.NgayDen, cv.STT, cv.STT1, cv.NgayBanHanh, cv.TrichYeu, cv.NguoiKy,
                   cv.FileDinhKem, cv.GhiChu, cv.MaDVXL, cv.BoPhanPhoiHop, cv.NgayGiao, cv.NgayYCHT, cv.NgayHT,
                   cv.MaVT, cv.NgayNhap, cv.MaLDXem, cv.MaNVXNHTCV, cv.NgayXNHTCV, cv.KQXN,
                   sc.TenSCV, lv.TenLVB, cq.TenCQ,
                   LTRIM(RTRIM(ISNULL(vt.HoNV,'') + ' ' + ISNULL(vt.TenNV,''))) AS TenVT,
                   LTRIM(RTRIM(ISNULL(ld.HoNV,'') + ' ' + ISNULL(ld.TenNV,''))) AS TenLDXem,
                   LTRIM(RTRIM(ISNULL(xn.HoNV,'') + ' ' + ISNULL(xn.TenNV,''))) AS TenXN
            FROM CongVanDen cv
            LEFT JOIN SoCV sc     ON cv.MaSCV = sc.MaSCV
            LEFT JOIN LoaiVB lv   ON cv.MaLVB = lv.MaLVB
            LEFT JOIN CoQuan cq   ON cv.MaCQ  = cq.MaCQ
            LEFT JOIN NhanVien vt ON cv.MaVT  = vt.MaNV
            LEFT JOIN NhanVien ld ON cv.MaLDXem = ld.MaNV
            LEFT JOIN NhanVien xn ON cv.MaNVXNHTCV = xn.MaNV
            WHERE cv.MSCVDi IS NULL OR LTRIM(RTRIM(cv.MSCVDi)) = ''")).ToList();

        const string sqlDen =
            "IF NOT EXISTS (SELECT 1 FROM CongVanDen WHERE MSCV = @MSCV) " +
            "INSERT INTO CongVanDen (MSCV,SoCV,NgayDen,STT,STT1,MaCQ,NgayBanHanh,TrichYeu,NguoiKy,FileDinhKem," +
            "GhiChu,MaLVB,MaSCV,MaLDXem,MaDVXL,BoPhanPhoiHop,NgayGiao,NgayYCHT,NgayHT,MaVT,NgayNhap," +
            "MaNVXNHTCV,NgayXNHTCV,KQXN) " +
            "VALUES (@MSCV,@SoCV,@NgayDen,@STT,@STT1,@MaCQ,@NgayBanHanh,@TrichYeu,@NguoiKy,@FileDinhKem," +
            "@GhiChu,@MaLVB,@MaSCV,@MaLDXem,@MaDVXL,@BoPhanPhoiHop,@NgayGiao,@NgayYCHT,@NgayHT,@MaVT,@NgayNhap," +
            "@MaNVXNHTCV,@NgayXNHTCV,@KQXN)";

        int n = 0;
        foreach (var r in rows)
        {
            try
            {
                var tenSCV = ((string?)r.TenSCV ?? "").Trim().ToLowerInvariant();
                var tenLVB = ((string?)r.TenLVB ?? "").Trim().ToLowerInvariant();
                var tenCQ  = ((string?)r.TenCQ  ?? "").Trim().ToLowerInvariant();
                byte? maDVXL = null;
                if (r.MaDVXL != null && oldDvIds.Contains((byte)Convert.ToByte(r.MaDVXL))) maDVXL = (byte)Convert.ToByte(r.MaDVXL);

                var mscv = ((string)r.MSCV).Trim();
                var affected = await old.ExecuteAsync(sqlDen, new
                {
                    MSCV = mscv,
                    SoCV = string.IsNullOrWhiteSpace((string?)r.SoCV) ? "-" : (string)r.SoCV,
                    r.NgayDen,
                    STT = Convert.ToDouble(r.STT),
                    r.STT1,
                    MaCQ = oldCq.TryGetValue(tenCQ, out var cq) ? (short?)cq : null,
                    r.NgayBanHanh,
                    r.TrichYeu,
                    r.NguoiKy,
                    FileDinhKem = ThemTienToTaiNguyen((string?)r.FileDinhKem),
                    r.GhiChu,
                    MaLVB = oldLvb.TryGetValue(tenLVB, out var lv) ? (short?)lv : null,
                    MaSCV = oldScv.TryGetValue(tenSCV, out var s) ? s : defaultScv,
                    MaLDXem = MapNvNull((int?)r.MaLDXem, (string?)r.TenLDXem),
                    MaDVXL = maDVXL,
                    r.BoPhanPhoiHop, r.NgayGiao, r.NgayYCHT, r.NgayHT,
                    MaVT = MapNv((int?)r.MaVT, (string?)r.TenVT),
                    r.NgayNhap,
                    MaNVXNHTCV = MapNvNull((int?)r.MaNVXNHTCV, (string?)r.TenXN),
                    r.NgayXNHTCV, r.KQXN
                });
                if (affected == 0) continue; // đã có bên cũ, không đụng
                n++;

                // Kèm dòng theo dõi xử lý theo đơn vị (để đơn vị thấy trong hàng chờ bên cũ).
                var xuly = await dst.QueryAsync(
                    "SELECT MaDV,LoaiDV,TrangThai,NgayXem,NgayTiepNhan,NgayHoanThanh,NgayCapNhat,GhiChu,MaNVCapNhat " +
                    "FROM CongVanDenXuLyDV WHERE MSCV=@mscv", new { mscv });
                foreach (var x in xuly)
                {
                    if (!oldDvIds.Contains((byte)Convert.ToByte(x.MaDV))) continue;
                    try
                    {
                        await old.ExecuteAsync(
                            "IF NOT EXISTS(SELECT 1 FROM CongVanDenXuLyDV WHERE MSCV=@mscv AND MaDV=@MaDV) " +
                            "  INSERT INTO CongVanDenXuLyDV(MSCV,MaDV,LoaiDV,TrangThai,NgayXem,NgayTiepNhan,NgayHoanThanh,NgayCapNhat,GhiChu,MaNVCapNhat) " +
                            "  VALUES(@mscv,@MaDV,@LoaiDV,@TrangThai,@NgayXem,@NgayTiepNhan,@NgayHoanThanh,@NgayCapNhat,@GhiChu,@MaNVCapNhat)",
                            new
                            {
                                mscv, x.MaDV, x.LoaiDV,
                                TrangThai = (byte)Convert.ToByte(x.TrangThai) > 3 ? (byte)0 : (byte)Convert.ToByte(x.TrangThai),
                                x.NgayXem, x.NgayTiepNhan, x.NgayHoanThanh, x.NgayCapNhat, x.GhiChu,
                                MaNVCapNhat = MapNvNull((int?)x.MaNVCapNhat, null)
                            });
                    }
                    catch (Exception ex2) { _logger.LogWarning(ex2, "OldServerSync: bo qua XuLyDV nguoc MSCV={MSCV} MaDV={MaDV}", (object)mscv, (object)x.MaDV); }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OldServerSync: bo qua ghi nguoc CongVanDen MSCV={MSCV}", (object)r.MSCV);
            }
        }
        return n;
    }

    // Đảo ngược ChuanHoaDuongDan — server cũ mong đường dẫn có tiền tố "~/tainguyen/".
    private static string? ThemTienToTaiNguyen(string? duongDan)
    {
        if (string.IsNullOrEmpty(duongDan)) return duongDan;
        const string tienTo = "~/tainguyen/";
        if (duongDan.StartsWith(tienTo, StringComparison.OrdinalIgnoreCase)) return duongDan;
        if (duongDan.StartsWith("http", StringComparison.OrdinalIgnoreCase) || duongDan.StartsWith("/")) return duongDan;
        return tienTo + duongDan.TrimStart('~', '/');
    }

    // Server cu luu FileDinhKem kem tien to "~/tainguyen/" (quy uoc ASP.NET cu) trong khi server
    // moi chi luu duong dan tuong doi thuan (vd "congvandi/2025/08/xxx.pdf"). Neu chep nguyen van
    // se ra duong dan sai, file khong tim thay du file that van con dung cho — phai bo tien to nay.
    private static string? ChuanHoaDuongDan(string? duongDan)
    {
        if (string.IsNullOrEmpty(duongDan)) return duongDan;
        const string tienTo = "~/tainguyen/";
        return duongDan.StartsWith(tienTo, StringComparison.OrdinalIgnoreCase)
            ? duongDan.Substring(tienTo.Length)
            : duongDan;
    }

    // Phai chay SAU CongVanDen (FK_CVD_DV_PhoiHop_CongVanDen).
    private async Task<int> SyncCvDenDvAsync(SqlConnection old, SqlConnection dst)
    {
        var rows = await old.QueryAsync(
            "SELECT MSCV,MaDV,MaNV,NgayGiao,NgayXem,NgayYCHT,NgayHT,NgayXNHT,KQXN,FileDinhKem FROM CVDen_DV");
        int n = 0;
        const string sql =
            "MERGE CVDen_DV AS tgt USING (SELECT CAST(@MSCV AS nchar(10)) AS MSCV, CAST(@MaDV AS tinyint) AS MaDV) AS src " +
            "ON tgt.MSCV=src.MSCV AND tgt.MaDV=src.MaDV " +
            "WHEN MATCHED THEN UPDATE SET MaNV=@MaNV,NgayGiao=@NgayGiao,NgayXem=@NgayXem,NgayYCHT=@NgayYCHT," +
            "NgayHT=@NgayHT,NgayXNHT=@NgayXNHT,KQXN=@KQXN,FileDinhKem=@FileDinhKem " +
            "WHEN NOT MATCHED THEN INSERT (MSCV,MaDV,MaNV,NgayGiao,NgayXem,NgayYCHT,NgayHT,NgayXNHT,KQXN,FileDinhKem) " +
            "VALUES (@MSCV,@MaDV,@MaNV,@NgayGiao,@NgayXem,@NgayYCHT,@NgayHT,@NgayXNHT,@KQXN,@FileDinhKem);";
        foreach (var r in rows)
        {
            try
            {
                var affected = await dst.ExecuteAsync(sql, new
                {
                    MSCV = (string)r.MSCV, r.MaDV, r.MaNV, r.NgayGiao, r.NgayXem, r.NgayYCHT, r.NgayHT,
                    r.NgayXNHT, r.KQXN, FileDinhKem = ChuanHoaDuongDan((string?)r.FileDinhKem)
                });
                if (affected > 0) n++;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "OldServerSync: bo qua CVDen_DV MSCV={MSCV} MaDV={MaDV}", (object)r.MSCV, (object)r.MaDV); }
        }
        return n;
    }

    private async Task<int> SyncCongVanDenXuLyDVAsync(SqlConnection old, SqlConnection dst)
    {
        var rows = await old.QueryAsync(
            "SELECT ID,MSCV,MaDV,LoaiDV,TrangThai,NgayXem,NgayTiepNhan,NgayHoanThanh,NgayCapNhat,GhiChu," +
            "FilePath,TenFile,MaNVCapNhat FROM CongVanDenXuLyDV");
        int n = 0;
        const string sql =
            "SET IDENTITY_INSERT CongVanDenXuLyDV ON; " +
            "MERGE CongVanDenXuLyDV AS tgt USING (SELECT CAST(@ID AS int) AS ID) AS src ON tgt.ID=src.ID " +
            "WHEN MATCHED THEN UPDATE SET MSCV=@MSCV,MaDV=@MaDV,LoaiDV=@LoaiDV,TrangThai=@TrangThai," +
            "NgayXem=@NgayXem,NgayTiepNhan=@NgayTiepNhan,NgayHoanThanh=@NgayHoanThanh,NgayCapNhat=@NgayCapNhat," +
            "GhiChu=@GhiChu,FilePath=@FilePath,TenFile=@TenFile,MaNVCapNhat=@MaNVCapNhat " +
            "WHEN NOT MATCHED THEN INSERT (ID,MSCV,MaDV,LoaiDV,TrangThai,NgayXem,NgayTiepNhan,NgayHoanThanh," +
            "NgayCapNhat,GhiChu,FilePath,TenFile,MaNVCapNhat) VALUES (@ID,@MSCV,@MaDV,@LoaiDV,@TrangThai," +
            "@NgayXem,@NgayTiepNhan,@NgayHoanThanh,@NgayCapNhat,@GhiChu,@FilePath,@TenFile,@MaNVCapNhat); " +
            "SET IDENTITY_INSERT CongVanDenXuLyDV OFF;";
        foreach (var r in rows)
        {
            try
            {
                var affected = await dst.ExecuteAsync(sql, new
                {
                    r.ID, MSCV = (string)r.MSCV, r.MaDV, r.LoaiDV, r.TrangThai, r.NgayXem, r.NgayTiepNhan,
                    r.NgayHoanThanh, r.NgayCapNhat, r.GhiChu, FilePath = ChuanHoaDuongDan((string?)r.FilePath), r.TenFile, r.MaNVCapNhat
                });
                if (affected > 0) n++;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "OldServerSync: bo qua CongVanDenXuLyDV ID={ID}", (object)r.ID); }
        }
        return n;
    }

    private async Task<int> SyncCvDenFileAsync(SqlConnection old, SqlConnection dst)
    {
        var rows = await old.QueryAsync(
            "SELECT ID,MSCV,TenFile,DuongDan,NgayUpload FROM CVDenFile");
        int n = 0;
        const string sql =
            "SET IDENTITY_INSERT CVDenFile ON; " +
            "MERGE CVDenFile AS tgt USING (SELECT CAST(@ID AS int) AS ID) AS src ON tgt.ID=src.ID " +
            "WHEN MATCHED THEN UPDATE SET MSCV=@MSCV,TenFile=@TenFile,DuongDan=@DuongDan,NgayUpload=@NgayUpload " +
            "WHEN NOT MATCHED THEN INSERT (ID,MSCV,TenFile,DuongDan,NgayUpload,TrangThaiTrichXuat) " +
            "VALUES (@ID,@MSCV,@TenFile,@DuongDan,@NgayUpload,0); " +
            "SET IDENTITY_INSERT CVDenFile OFF;";
        foreach (var r in rows)
        {
            try
            {
                var affected = await dst.ExecuteAsync(sql, new
                {
                    r.ID, MSCV = (string)r.MSCV, r.TenFile, r.DuongDan, r.NgayUpload
                });
                if (affected > 0) n++;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "OldServerSync: bo qua CVDenFile ID={ID}", (object)r.ID); }
        }
        return n;
    }

    // Phai chay SAU CVDen_DV (FK_CVDen_QTXL_CVDen_DV). Bang log, khong co ID rieng -> khoa tu nhien.
    private async Task<int> SyncCvDenQtxlAsync(SqlConnection old, SqlConnection dst)
    {
        var rows = await old.QueryAsync(
            "SELECT MSCV,MaDV,MaNV,NgayXL,NoiDungXL,FileDinhKem FROM CVDen_QTXL");
        int n = 0;
        const string sql =
            "IF NOT EXISTS(SELECT 1 FROM CVDen_QTXL WHERE MSCV=@MSCV AND MaDV=@MaDV AND NgayXL=@NgayXL) " +
            "INSERT INTO CVDen_QTXL (MSCV,MaDV,MaNV,NgayXL,NoiDungXL,FileDinhKem) " +
            "VALUES (@MSCV,@MaDV,@MaNV,@NgayXL,@NoiDungXL,@FileDinhKem)";
        foreach (var r in rows)
        {
            try
            {
                var affected = await dst.ExecuteAsync(sql, new
                {
                    MSCV = (string)r.MSCV, r.MaDV, r.MaNV, r.NgayXL, r.NoiDungXL, FileDinhKem = ChuanHoaDuongDan((string?)r.FileDinhKem)
                });
                if (affected > 0) n++;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "OldServerSync: bo qua CVDen_QTXL MSCV={MSCV}", (object)r.MSCV); }
        }
        return n;
    }

    private async Task<int> SyncCvDenDaXemAsync(SqlConnection old, SqlConnection dst)
    {
        var rows = await old.QueryAsync("SELECT MSCV,MaNV,NgayXem FROM CVDen_DaXem");
        int n = 0;
        const string sql =
            "IF NOT EXISTS(SELECT 1 FROM CVDen_DaXem WHERE MSCV=@MSCV AND MaNV=@MaNV) " +
            "INSERT INTO CVDen_DaXem (MSCV,MaNV,NgayXem) VALUES (@MSCV,@MaNV,@NgayXem)";
        foreach (var r in rows)
        {
            try
            {
                var affected = await dst.ExecuteAsync(sql, new { MSCV = (string)r.MSCV, r.MaNV, r.NgayXem });
                if (affected > 0) n++;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "OldServerSync: bo qua CVDen_DaXem MSCV={MSCV}", (object)r.MSCV); }
        }
        return n;
    }
}
