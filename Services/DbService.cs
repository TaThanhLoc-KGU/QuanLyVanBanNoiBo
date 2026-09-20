using Dapper;
using Microsoft.Data.SqlClient;
using CongVan.Models;

namespace CongVan.Services;

public partial class DbService
{
    private readonly string _conn;

    public DbService(IConfiguration config)
    {
        _conn = config.GetConnectionString("CongVanConnection")!;
    }

    private SqlConnection Open() => new SqlConnection(_conn);

    // ── Auth ────────────────────────────────────────────────────────────────
    public async Task<NhanVien?> LoginAsync(string login, string password)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<NhanVien>(
            "SELECT nv.MaNV, nv.HoNV, nv.TenNV, nv.Email, nv.AnhNV, nv.MaDV, nv.Username, dv.TenDV " +
            "FROM NhanVien nv LEFT JOIN DonVi dv ON nv.MaDV=dv.MaDV " +
            "WHERE (nv.Email=@login OR nv.username=@login) AND PWDCompare(@password, nv.MatKhau)=1 AND nv.NgayNghiViec IS NULL",
            new { login, password });
    }

    public async Task<bool> ChangePasswordAsync(short maNV, string matKhauCu, string matKhauMoi, string email)
    {
        using var db = Open();
        var check = await db.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(1) FROM NhanVien WHERE MaNV=@maNV AND PWDCompare(@mkcu, MatKhau)=1",
            new { maNV, mkcu = matKhauCu });
        if (check == 0) return false;
        await db.ExecuteAsync(
            "UPDATE NhanVien SET MatKhau=pwdencrypt(@mk), Email=@email WHERE MaNV=@maNV",
            new { mk = matKhauMoi, email, maNV });
        return true;
    }

    public async Task<List<PhanQuyen>> GetQuyenAsync(short maNV)
    {
        using var db = Open();
        var result = await db.QueryAsync<PhanQuyen>(
            "SELECT * FROM PhanQuyen WHERE MaNV=@maNV", new { maNV });
        return result.ToList();
    }

    // ── Lookup ──────────────────────────────────────────────────────────────
    public async Task<Dictionary<string, int>> GetDashboardStatsAsync(int nam)
    {
        using var db = Open();
        var sql = @"
            SELECT
                SUM(CASE WHEN YEAR(NgayDen)=@nam THEN 1 ELSE 0 END) AS TongNam,
                SUM(CASE WHEN MONTH(NgayDen)=@thang AND YEAR(NgayDen)=@nam THEN 1 ELSE 0 END) AS TongThang,
                SUM(CASE WHEN NgayHT IS NULL AND NgayYCHT < GETDATE() THEN 1 ELSE 0 END) AS QuaHan,
                SUM(CASE WHEN NgayHT IS NULL AND NgayYCHT IS NOT NULL THEN 1 ELSE 0 END) AS DangXuLy
            FROM CongVanDen";
        var row = await db.QueryFirstAsync(sql, new { nam, thang = DateTime.Now.Month });
        return new Dictionary<string, int>
        {
            ["TongNam"]    = (int)(row.TongNam    ?? 0),
            ["TongThang"]  = (int)(row.TongThang  ?? 0),
            ["QuaHan"]     = (int)(row.QuaHan     ?? 0),
            ["DangXuLy"]   = (int)(row.DangXuLy   ?? 0),
        };
    }

    public async Task<List<SoCV>> GetSoCVAsync()
    {
        using var db = Open();
        return (await db.QueryAsync<SoCV>("SELECT * FROM SoCV ORDER BY MaSCV")).ToList();
    }

    public async Task<byte> ThemSoCVAsync(string tenSCV, string? moTa)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<byte>(
            "INSERT INTO SoCV (MaSCV, TenSCV, MoTa, DemRiengTheoLoai) OUTPUT INSERTED.MaSCV " +
            "SELECT ISNULL(MAX(MaSCV),0)+1, @tenSCV, @moTa, 0 FROM SoCV",
            new { tenSCV, moTa });
    }

    public async Task SuaSoCVAsync(byte maSCV, string tenSCV, string? moTa)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "UPDATE SoCV SET TenSCV=@tenSCV, MoTa=@moTa WHERE MaSCV=@maSCV",
            new { maSCV, tenSCV, moTa });
    }

    public async Task SetDemRiengTheoLoaiAsync(byte maSCV, bool val)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE SoCV SET DemRiengTheoLoai=@val WHERE MaSCV=@maSCV", new { maSCV, val });
    }

    // ── Mẫu số ký hiệu (tự sinh số văn bản) ────────────────────────────────
    public async Task<List<MauSoKyHieu>> GetMauSoKyHieuAsync()
    {
        using var db = Open();
        return (await db.QueryAsync<MauSoKyHieu>(
            @"SELECT m.*, sc.TenSCV, lv.TenLVB
              FROM MauSoKyHieu m
              JOIN SoCV sc ON m.MaSCV=sc.MaSCV
              LEFT JOIN LoaiVB lv ON m.MaLVB=lv.MaLVB
              ORDER BY sc.MaSCV, lv.TenLVB")).ToList();
    }

    public async Task ThemMauSoKyHieuAsync(byte maSCV, short? maLVB, string mauChuoi)
    {
        using var db = Open();
        await db.ExecuteAsync(
            @"IF EXISTS(SELECT 1 FROM MauSoKyHieu WHERE MaSCV=@maSCV AND (MaLVB=@maLVB OR (MaLVB IS NULL AND @maLVB IS NULL)))
                UPDATE MauSoKyHieu SET MauChuoi=@mauChuoi
                WHERE MaSCV=@maSCV AND (MaLVB=@maLVB OR (MaLVB IS NULL AND @maLVB IS NULL))
              ELSE
                INSERT INTO MauSoKyHieu (MaSCV, MaLVB, MauChuoi) VALUES (@maSCV, @maLVB, @mauChuoi)",
            new { maSCV, maLVB, mauChuoi });
    }

    public async Task XoaMauSoKyHieuAsync(int id)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM MauSoKyHieu WHERE ID=@id", new { id });
    }

    // Tính STT kế tiếp + build chuỗi Số ký hiệu tự sinh cho văn bản đi, theo mẫu đã cấu hình.
    // Trả về soKyHieu=null nếu sổ này chưa cấu hình mẫu (giữ hành vi cũ: người dùng tự nhập).
    public async Task<(int Stt, string? SoKyHieu)> GetNextSoKyHieuDiAsync(byte maSCV, short? maLVB)
    {
        using var db = Open();
        var nam = DateTime.Now.Year;

        var demRiengTheoLoai = await db.QueryFirstOrDefaultAsync<bool>(
            "SELECT DemRiengTheoLoai FROM SoCV WHERE MaSCV=@maSCV", new { maSCV });

        int stt;
        if (demRiengTheoLoai && maLVB.HasValue)
        {
            stt = await db.QueryFirstOrDefaultAsync<int?>(
                "SELECT MAX(CAST(STT AS int)) FROM CongVanDi WHERE MaSCV=@maSCV AND MaLVB=@maLVB AND YEAR(NgayCongVan)=@nam",
                new { maSCV, maLVB, nam }) ?? 0;
        }
        else
        {
            stt = await db.QueryFirstOrDefaultAsync<int?>(
                "SELECT MAX(CAST(STT AS int)) FROM CongVanDi WHERE MaSCV=@maSCV AND YEAR(NgayCongVan)=@nam",
                new { maSCV, nam }) ?? 0;
        }
        stt++;

        string? mauChuoi = null;
        if (maLVB.HasValue)
            mauChuoi = await db.QueryFirstOrDefaultAsync<string>(
                "SELECT MauChuoi FROM MauSoKyHieu WHERE MaSCV=@maSCV AND MaLVB=@maLVB", new { maSCV, maLVB });
        if (mauChuoi == null)
            mauChuoi = await db.QueryFirstOrDefaultAsync<string>(
                "SELECT MauChuoi FROM MauSoKyHieu WHERE MaSCV=@maSCV AND MaLVB IS NULL", new { maSCV });

        if (mauChuoi == null) return (stt, null);

        string? kyHieu = maLVB.HasValue
            ? await db.QueryFirstOrDefaultAsync<string>("SELECT KyHieu FROM LoaiVB WHERE MaLVB=@maLVB", new { maLVB })
            : null;

        var soKyHieu = mauChuoi
            .Replace("{STT3}", stt.ToString("000"))
            .Replace("{STT2}", stt.ToString("00"))
            .Replace("{STT}", stt.ToString())
            .Replace("{NAM2}", (nam % 100).ToString("00"))
            .Replace("{NAM}", nam.ToString())
            .Replace("{KyHieu}", kyHieu ?? "");

        return (stt, soKyHieu);
    }

    public async Task<List<CoQuan>> GetCoQuanAsync(bool chiHienThi = true)
    {
        using var db = Open();
        var sql = chiHienThi
            ? "SELECT * FROM CoQuan WHERE HienThi=1 ORDER BY TenCQ"
            : "SELECT * FROM CoQuan ORDER BY TenCQ";
        return (await db.QueryAsync<CoQuan>(sql)).ToList();
    }

    public async Task<short> ThemCoQuanAsync(string tenCQ)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<short>(
            "INSERT INTO CoQuan (MaCQ, TenCQ, HienThi) OUTPUT INSERTED.MaCQ SELECT ISNULL(MAX(MaCQ),0)+1, @tenCQ, 1 FROM CoQuan",
            new { tenCQ });
    }

    public async Task SetHienThiCoQuanAsync(short maCQ, bool hienThi)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE CoQuan SET HienThi=@hienThi WHERE MaCQ=@maCQ", new { maCQ, hienThi });
    }

    // ── Phòng họp (danh mục quản lý ở Admin, dùng để đăng ký khi tạo Lịch công tác) ─────────
    public async Task<List<PhongHop>> GetPhongHopAsync(bool chiHienThi = true)
    {
        using var db = Open();
        var sql = chiHienThi
            ? "SELECT * FROM PhongHop WHERE HienThi=1 ORDER BY TenPhong"
            : "SELECT * FROM PhongHop ORDER BY TenPhong";
        return (await db.QueryAsync<PhongHop>(sql)).ToList();
    }

    public async Task<byte> ThemPhongHopAsync(string tenPhong, string? viTri, short? sucChua, string? ghiChu)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<byte>(
            @"INSERT INTO PhongHop (TenPhong, ViTri, SucChua, GhiChu, HienThi)
              OUTPUT INSERTED.MaPhong VALUES (@tenPhong, @viTri, @sucChua, @ghiChu, 1)",
            new { tenPhong, viTri, sucChua, ghiChu });
    }

    public async Task SuaPhongHopAsync(byte maPhong, string tenPhong, string? viTri, short? sucChua, string? ghiChu)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "UPDATE PhongHop SET TenPhong=@tenPhong, ViTri=@viTri, SucChua=@sucChua, GhiChu=@ghiChu WHERE MaPhong=@maPhong",
            new { maPhong, tenPhong, viTri, sucChua, ghiChu });
    }

    public async Task SetHienThiPhongHopAsync(byte maPhong, bool hienThi)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE PhongHop SET HienThi=@hienThi WHERE MaPhong=@maPhong", new { maPhong, hienThi });
    }

    // Kiểm tra trùng lịch cùng 1 phòng — 2 khoảng thời gian giao nhau khi start1<end2 && end1>start2;
    // thiếu ThoiGianKetThuc coi như chiếm 1 giờ (khớp cách hiển thị mặc định trên lịch).
    public async Task<List<LichCongTac>> GetTrungPhongAsync(byte maPhong, DateTime batDau, DateTime ketThuc, int? loaiTruMaLich = null)
    {
        using var db = Open();
        return (await db.QueryAsync<LichCongTac>(
            @"SELECT lc.*, dv.TenDV, (nv.HoNV+' '+nv.TenNV) AS TenNVTao
              FROM LichCongTac lc
              LEFT JOIN DonVi dv ON lc.MaDV=dv.MaDV
              JOIN NhanVien nv ON lc.MaNVTao=nv.MaNV
              WHERE lc.MaPhong=@maPhong AND (@loaiTruMaLich IS NULL OR lc.MaLich<>@loaiTruMaLich)
                AND lc.ThoiGianBatDau < @ketThuc
                AND ISNULL(lc.ThoiGianKetThuc, DATEADD(HOUR,1,lc.ThoiGianBatDau)) > @batDau",
            new { maPhong, batDau, ketThuc, loaiTruMaLich })).ToList();
    }

    public async Task<List<LoaiVB>> GetLoaiVBAsync(bool chiHienThi = true)
    {
        using var db = Open();
        var sql = chiHienThi
            ? "SELECT * FROM LoaiVB WHERE HienThi=1 ORDER BY TenLVB"
            : "SELECT * FROM LoaiVB ORDER BY TenLVB";
        return (await db.QueryAsync<LoaiVB>(sql)).ToList();
    }

    public async Task SetHienThiLoaiVBAsync(short maLVB, bool hienThi)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE LoaiVB SET HienThi=@hienThi WHERE MaLVB=@maLVB", new { maLVB, hienThi });
    }

    public async Task SetKyHieuLoaiVBAsync(short maLVB, string? kyHieu)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE LoaiVB SET KyHieu=@kyHieu WHERE MaLVB=@maLVB", new { maLVB, kyHieu });
    }

    public async Task<short> ThemLoaiVBAsync(string tenLVB, string? kyHieu)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<short>(
            "INSERT INTO LoaiVB (TenLVB, KyHieu, HienThi) OUTPUT INSERTED.MaLVB VALUES (@tenLVB, @kyHieu, 1)",
            new { tenLVB, kyHieu });
    }

    // ── File đính kèm nhiều file ────────────────────────────────────────────
    public async Task<List<CVDenFile>> GetFileDinhKemAsync(string mscv)
    {
        using var db = Open();
        return (await db.QueryAsync<CVDenFile>(
            "SELECT * FROM CVDenFile WHERE MSCV=@mscv ORDER BY NgayUpload", new { mscv })).ToList();
    }

    public async Task<int> ThemFileDinhKemAsync(string mscv, string tenFile, string duongDan)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<int>(
            "INSERT INTO CVDenFile (MSCV,TenFile,DuongDan) OUTPUT INSERTED.ID VALUES (@mscv,@tenFile,@duongDan)",
            new { mscv, tenFile, duongDan });
    }

    public async Task XoaFileDinhKemAsync(int id)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM CVDenFile WHERE ID=@id", new { id });
    }

    // Kết quả trích xuất chữ từ PDF (đọc trực tiếp hoặc OCR) — xem Services/PdfTextService.cs.
    public async Task CapNhatTrichXuatFileAsync(int fileId, string? noiDung, byte trangThai)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "UPDATE CVDenFile SET NoiDungTrichXuat=@noiDung, TrangThaiTrichXuat=@trangThai WHERE ID=@fileId",
            new { fileId, noiDung, trangThai });
    }

    // Các file PDF chưa trích xuất (dùng cho việc quét lại hồ sơ cũ) — giới hạn số lượng mỗi lần gọi
    // để không làm nghẽn hàng đợi nền khi có nhiều file tồn đọng.
    public async Task<List<CVDenFile>> GetFileChuaTrichXuatAsync(int limit)
    {
        using var db = Open();
        return (await db.QueryAsync<CVDenFile>(
            "SELECT TOP (@limit) * FROM CVDenFile WHERE TrangThaiTrichXuat=0 AND TenFile LIKE '%.pdf' ORDER BY NgayUpload",
            new { limit })).ToList();
    }

    public async Task<List<LoaiVBCVDi>> GetLoaiVBCVDiAsync()
    {
        using var db = Open();
        return (await db.QueryAsync<LoaiVBCVDi>("SELECT * FROM LoaiVBCVDi ORDER BY TenLVB")).ToList();
    }

    public async Task<List<NhomCV>> GetNhomCVAsync()
    {
        using var db = Open();
        return (await db.QueryAsync<NhomCV>("SELECT * FROM NhomCV ORDER BY TenNCV")).ToList();
    }

    public async Task<List<DonVi>> GetDonViAsync(bool chiLayConHoatDong = true)
    {
        using var db = Open();
        var where = chiLayConHoatDong ? "WHERE NgayGiaiThe IS NULL" : "";
        return (await db.QueryAsync<DonVi>($"SELECT * FROM DonVi {where} ORDER BY STT, TenDV")).ToList();
    }

    public async Task<DonVi?> GetDonViByIdAsync(byte maDV)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<DonVi>("SELECT * FROM DonVi WHERE MaDV=@maDV", new { maDV });
    }

    public async Task<int> GetNextSTTAsync(byte maSCV, int nam)
    {
        using var db = Open();
        var max = await db.QueryFirstOrDefaultAsync<double?>(
            "SELECT MAX(STT) FROM CongVanDen WHERE MaSCV=@maSCV AND YEAR(NgayDen)=@nam",
            new { maSCV, nam });
        return (int)(max ?? 0) + 1;
    }

    public async Task ThemDonViAsync(DonVi dv)
    {
        using var db = Open();
        var maxMa = await db.QueryFirstOrDefaultAsync<byte>("SELECT ISNULL(MAX(MaDV),0)+1 FROM DonVi");
        await db.ExecuteAsync(
            "INSERT INTO DonVi(MaDV,STT,TenDV,TenTat,LoaiDV) VALUES(@MaDV,@STT,@TenDV,@TenTat,@LoaiDV)",
            new { MaDV = maxMa, dv.STT, dv.TenDV, dv.TenTat, dv.LoaiDV });
    }

    public async Task SuaDonViAsync(DonVi dv)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "UPDATE DonVi SET STT=@STT, TenDV=@TenDV, TenTat=@TenTat, LoaiDV=@LoaiDV, MaNV_TDV=@mv, MaNV_TheoDoiCV=@mv WHERE MaDV=@MaDV",
            new { dv.STT, dv.TenDV, dv.TenTat, dv.LoaiDV, mv = (dv.MaNV_TDV == 0 ? null : dv.MaNV_TDV) ?? (dv.MaNV_TheoDoiCV == 0 ? null : dv.MaNV_TheoDoiCV), dv.MaDV });
    }

    public async Task GiaiTheDonViAsync(byte maDV)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "UPDATE DonVi SET NgayGiaiThe=GETDATE() WHERE MaDV=@maDV", new { maDV });
    }

    public async Task GopDonViAsync(byte[] maDVNguon, byte maDVDich)
    {
        using var db = Open();
        foreach (var src in maDVNguon)
        {
            // Xóa CVDen_QTXL của nguồn nếu CV đó đã có trong đơn vị đích
            await db.ExecuteAsync(
                "DELETE FROM CVDen_QTXL WHERE MaDV=@src AND MSCV IN (SELECT MSCV FROM CVDen_DV WHERE MaDV=@dst)",
                new { src, dst = maDVDich });
            // Xóa CVDen_DV nguồn trùng với đích
            await db.ExecuteAsync(
                "DELETE FROM CVDen_DV WHERE MaDV=@src AND MSCV IN (SELECT MSCV FROM CVDen_DV WHERE MaDV=@dst)",
                new { src, dst = maDVDich });
            // Chuyển CVDen_QTXL còn lại sang đích
            await db.ExecuteAsync("UPDATE CVDen_QTXL SET MaDV=@dst WHERE MaDV=@src", new { src, dst = maDVDich });
            // Chuyển CVDen_DV còn lại sang đích
            await db.ExecuteAsync("UPDATE CVDen_DV SET MaDV=@dst WHERE MaDV=@src", new { src, dst = maDVDich });
            // Chuyển CongVanDen.MaDVXL
            await db.ExecuteAsync("UPDATE CongVanDen SET MaDVXL=@dst WHERE MaDVXL=@src", new { src, dst = maDVDich });
            // Chuyển NhanVien
            await db.ExecuteAsync("UPDATE NhanVien SET MaDV=@dst WHERE MaDV=@src", new { src, dst = maDVDich });
            // Giải thể đơn vị nguồn
            await db.ExecuteAsync("UPDATE DonVi SET NgayGiaiThe=GETDATE() WHERE MaDV=@src", new { src });
        }
    }

    public async Task KhoiPhucDonViAsync(byte maDV)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "UPDATE DonVi SET NgayGiaiThe=NULL WHERE MaDV=@maDV", new { maDV });
    }

    // Tra cứu văn bản đơn vị xử lý
    public async Task<List<CongVanDen>> GetCVDenTheoDonViAsync(byte maDV, int? nam = null, string? tuKhoa = null)
    {
        using var db = Open();
        var where = "WHERE cvd.MaDVXL=@maDV";
        if (nam.HasValue) where += " AND YEAR(cvd.NgayDen)=@nam";
        if (!string.IsNullOrEmpty(tuKhoa)) where += " AND (cvd.TrichYeu LIKE @tk OR cvd.STT1 LIKE @tk)";
        return (await db.QueryAsync<CongVanDen>(
            $@"SELECT cvd.*, cq.TenCQ, lv.TenLVB, sc.TenSCV, dv.TenDV AS TenDVXL,
               ISNULL(dv.TenTat, dv.TenDV) AS TenDVTat,
               nv.HoNV+' '+nv.TenNV AS TenLDXem
               FROM CongVanDen cvd
               LEFT JOIN CoQuan cq ON cvd.MaCQ=cq.MaCQ
               LEFT JOIN LoaiVB lv ON cvd.MaLVB=lv.MaLVB
               LEFT JOIN SoCV sc ON cvd.MaSCV=sc.MaSCV
               LEFT JOIN DonVi dv ON cvd.MaDVXL=dv.MaDV
               LEFT JOIN NhanVien nv ON cvd.MaLDXem=nv.MaNV
               {where} ORDER BY cvd.NgayDen DESC",
            new { maDV, nam, tk = $"%{tuKhoa}%" })).ToList();
    }

    // Văn bản đi đã gửi cho một đơn vị nội bộ cụ thể (theo DonViNhan)
    public async Task<List<CongVanDi>> GetCVDiTheoDonViAsync(byte maDV, int? nam = null, string? tuKhoa = null)
    {
        using var db = Open();
        // Loại các văn bản đi đã có bản ghi văn bản đến tương ứng cho đúng đơn vị này (đã tạo
        // qua TaoVanBanDenTuVanBanDiAsync) — tránh hiển thị trùng, vì lúc đó nó đã nằm bên
        // "Văn bản đến" (đầy đủ tình trạng xử lý) rồi. Chỉ còn lại các bản ghi cũ trước khi có
        // cơ chế này (nhập trước khi tính năng ra đời).
        var where = @"WHERE (',' + ISNULL(cvdi.DonViNhan,'') + ',') LIKE '%,' + CAST(@maDV AS varchar) + ',%'
                      AND NOT EXISTS (SELECT 1 FROM CongVanDen cd WHERE cd.MSCVDi=cvdi.MSCV AND cd.MaDVXL=@maDV)";
        if (nam.HasValue) where += " AND YEAR(cvdi.NgayCongVan)=@nam";
        if (!string.IsNullOrEmpty(tuKhoa)) where += " AND (cvdi.TrichYeu LIKE @tk OR cvdi.STT1 LIKE @tk)";
        return (await db.QueryAsync<CongVanDi>(
            $@"SELECT cvdi.*, lv.TenLVB, sc.TenSCV, nv.HoNV+' '+nv.TenNV AS TenLDKy
               FROM CongVanDi cvdi
               LEFT JOIN LoaiVB lv ON cvdi.MaLVB=lv.MaLVB
               LEFT JOIN SoCV sc ON cvdi.MaSCV=sc.MaSCV
               LEFT JOIN NhanVien nv ON cvdi.MaLDKy=nv.MaNV
               {where} ORDER BY cvdi.NgayCongVan DESC",
            new { maDV, nam, tk = $"%{tuKhoa}%" })).ToList();
    }

    public async Task<List<NhanVien>> GetNhanVienAsync(byte? maDV = null, bool baoGomNghiViec = false)
    {
        using var db = Open();
        var where = maDV.HasValue ? "WHERE nv.MaDV=@maDV" : "WHERE 1=1";
        if (!baoGomNghiViec) where += " AND nv.NgayNghiViec IS NULL";
        var sql = $"SELECT nv.*, dv.TenDV FROM NhanVien nv LEFT JOIN DonVi dv ON nv.MaDV=dv.MaDV {where} ORDER BY nv.HoNV,nv.TenNV";
        return (await db.QueryAsync<NhanVien>(sql, new { maDV })).ToList();
    }

    public async Task<List<NhanVien>> GetLanhDaoAsync()
    {
        using var db = Open();
        return (await db.QueryAsync<NhanVien>(
            "SELECT DISTINCT nv.*, dv.TenDV FROM NhanVien nv " +
            "LEFT JOIN DonVi dv ON nv.MaDV=dv.MaDV " +
            "JOIN PhanQuyen pq ON nv.MaNV=pq.MaNV WHERE pq.MaQuyen=12 ORDER BY nv.HoNV,nv.TenNV")).ToList();
    }

    // Lãnh đạo trường (PhanQuyen.MaQuyen=12) + lãnh đạo/trưởng các đơn vị (DonVi.MaNV_TDV) —
    // dùng cho ô chọn "Lãnh đạo" ở Lịch làm việc, KHÔNG liệt kê toàn bộ viên chức.
    public async Task<List<NhanVien>> GetLanhDaoTruongVaDonViAsync()
    {
        using var db = Open();
        return (await db.QueryAsync<NhanVien>(
            "SELECT nv.*, dv.TenDV FROM NhanVien nv LEFT JOIN DonVi dv ON nv.MaDV=dv.MaDV " +
            "WHERE nv.NgayNghiViec IS NULL AND (" +
            "  nv.MaNV IN (SELECT MaNV FROM PhanQuyen WHERE MaQuyen=12)" +
            "  OR nv.MaNV IN (SELECT MaNV_TDV FROM DonVi WHERE MaNV_TDV IS NOT NULL)" +
            ") ORDER BY dv.STT, nv.HoNV, nv.TenNV")).ToList();
    }

    // ── Công văn đến ────────────────────────────────────────────────────────
    public async Task<List<CongVanDen>> GetCongVanDenAsync(int? nam = null, byte? maSCV = null, string? tuKhoa = null,
        byte? filterMaDV = null, short? maNVHienTai = null,
        byte? maDVXL = null, string? nguoiKy = null, DateTime? tuNgay = null, DateTime? denNgay = null)
    {
        using var db = Open();
        var where = "WHERE 1=1";
        if (nam.HasValue) where += " AND YEAR(cvd.NgayDen)=@nam";
        if (maSCV.HasValue) where += " AND cvd.MaSCV=@maSCV";
        if (!string.IsNullOrEmpty(tuKhoa)) where += " AND (cvd.TrichYeu LIKE @tk OR cvd.STT1 LIKE @tk OR cq.TenCQ LIKE @tk)";
        if (filterMaDV.HasValue)
            where += " AND (cvd.MaDVXL=@filterMaDV OR (',' + ISNULL(cvd.BoPhanPhoiHop,'') + ',') LIKE '%,' + CAST(@filterMaDV AS varchar) + ',%')";
        if (maDVXL.HasValue) where += " AND cvd.MaDVXL=@maDVXL";
        if (!string.IsNullOrEmpty(nguoiKy)) where += " AND cvd.NguoiKy LIKE @nguoiKyLike";
        if (tuNgay.HasValue) where += " AND cvd.NgayDen >= @tuNgay";
        if (denNgay.HasValue) where += " AND cvd.NgayDen < @denNgayExclusive";

        var sql = $@"SELECT cvd.*, cq.TenCQ, lv.TenLVB, sc.TenSCV, nc.TenNCV, dv.TenDV AS TenDVXL,
                     ISNULL(dv.TenTat, dv.TenDV) AS TenDVTat,
                     nv.HoNV+' '+nv.TenNV AS TenLDXem,
                     nvld.HoNV+' '+nvld.TenNV AS TenLDPhong,
                     nvvt.HoNV+' '+nvvt.TenNV AS TenVT,
                     nvxn.HoNV+' '+nvxn.TenNV AS TenNVXNHT,
                     CASE WHEN EXISTS(SELECT 1 FROM CVDenFile f WHERE f.MSCV=cvd.MSCV) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS CoFile,
                     CASE WHEN EXISTS(SELECT 1 FROM CVDen_DaXem dx WHERE dx.MSCV=cvd.MSCV AND dx.MaNV=@maNVHienTai) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS DaXem,
                     (SELECT MAX(x.TrangThai) FROM CongVanDenXuLyDV x WHERE x.MSCV=cvd.MSCV) AS MaxXuLyTrangThai,
                     (SELECT x.NgayXem FROM CongVanDenXuLyDV x WHERE x.MSCV=cvd.MSCV AND x.MaDV=cvd.MaDVXL AND x.LoaiDV=1) AS DonViDaXemLuc
                     FROM CongVanDen cvd
                     LEFT JOIN CoQuan cq ON cvd.MaCQ=cq.MaCQ
                     LEFT JOIN LoaiVB lv ON cvd.MaLVB=lv.MaLVB
                     LEFT JOIN SoCV sc ON cvd.MaSCV=sc.MaSCV
                     LEFT JOIN NhomCV nc ON cvd.MaNCV=nc.MaNCV
                     LEFT JOIN DonVi dv ON cvd.MaDVXL=dv.MaDV
                     LEFT JOIN NhanVien nv ON cvd.MaLDXem=nv.MaNV
                     LEFT JOIN NhanVien nvld ON dv.MaNV_TheoDoiCV=nvld.MaNV
                     LEFT JOIN NhanVien nvvt ON cvd.MaVT=nvvt.MaNV
                     LEFT JOIN NhanVien nvxn ON cvd.MaNVXNHTCV=nvxn.MaNV
                     {where} ORDER BY cvd.NgayDen DESC, cvd.STT DESC";
        return (await db.QueryAsync<CongVanDen>(sql, new {
            nam, maSCV, tk = $"%{tuKhoa}%", filterMaDV, maNVHienTai, maDVXL,
            nguoiKyLike = $"%{nguoiKy}%", tuNgay, denNgayExclusive = denNgay?.Date.AddDays(1)
        })).ToList();
    }

    // ── Phân trang + tìm nhiều thuộc tính cho các danh sách Văn bản đến ──────
    // Helper dùng chung: nhận sẵn WHERE + tham số, tự thêm SELECT/JOIN đầy đủ + OFFSET/FETCH — tránh
    // chép lại nguyên khối SELECT 15 dòng ở mỗi biến thể danh sách (Index/DangXuLy/QuaHan/HoanThanh).
    private async Task<(List<CongVanDen> Items, int Total)> QueryCongVanDenPagedAsync(
        string whereClause, string orderBy, Dapper.DynamicParameters parameters, int page, int pageSize)
    {
        using var db = Open();
        var countSql = $"SELECT COUNT(1) FROM CongVanDen cvd LEFT JOIN CoQuan cq ON cvd.MaCQ=cq.MaCQ LEFT JOIN DonVi dv ON cvd.MaDVXL=dv.MaDV {whereClause}";
        var total = await db.ExecuteScalarAsync<int>(countSql, parameters);

        parameters.Add("skip", Math.Max(0, page - 1) * pageSize);
        parameters.Add("pageSize", pageSize);
        var sql = $@"SELECT cvd.*, cq.TenCQ, lv.TenLVB, sc.TenSCV, nc.TenNCV, dv.TenDV AS TenDVXL,
                     ISNULL(dv.TenTat, dv.TenDV) AS TenDVTat,
                     nv.HoNV+' '+nv.TenNV AS TenLDXem,
                     nvld.HoNV+' '+nvld.TenNV AS TenLDPhong,
                     nvvt.HoNV+' '+nvvt.TenNV AS TenVT,
                     nvxn.HoNV+' '+nvxn.TenNV AS TenNVXNHT,
                     CASE WHEN EXISTS(SELECT 1 FROM CVDenFile f WHERE f.MSCV=cvd.MSCV) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS CoFile,
                     CASE WHEN EXISTS(SELECT 1 FROM CVDen_DaXem dx WHERE dx.MSCV=cvd.MSCV AND dx.MaNV=@maNVHienTai) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS DaXem,
                     (SELECT MAX(x.TrangThai) FROM CongVanDenXuLyDV x WHERE x.MSCV=cvd.MSCV) AS MaxXuLyTrangThai,
                     (SELECT x.NgayXem FROM CongVanDenXuLyDV x WHERE x.MSCV=cvd.MSCV AND x.MaDV=cvd.MaDVXL AND x.LoaiDV=1) AS DonViDaXemLuc
                     FROM CongVanDen cvd
                     LEFT JOIN CoQuan cq ON cvd.MaCQ=cq.MaCQ
                     LEFT JOIN LoaiVB lv ON cvd.MaLVB=lv.MaLVB
                     LEFT JOIN SoCV sc ON cvd.MaSCV=sc.MaSCV
                     LEFT JOIN NhomCV nc ON cvd.MaNCV=nc.MaNCV
                     LEFT JOIN DonVi dv ON cvd.MaDVXL=dv.MaDV
                     LEFT JOIN NhanVien nv ON cvd.MaLDXem=nv.MaNV
                     LEFT JOIN NhanVien nvld ON dv.MaNV_TheoDoiCV=nvld.MaNV
                     LEFT JOIN NhanVien nvvt ON cvd.MaVT=nvvt.MaNV
                     LEFT JOIN NhanVien nvxn ON cvd.MaNVXNHTCV=nvxn.MaNV
                     {whereClause} ORDER BY {orderBy}
                     OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY";
        var items = (await db.QueryAsync<CongVanDen>(sql, parameters)).ToList();
        return (items, total);
    }

    public async Task<(List<CongVanDen> Items, int Total)> GetCongVanDenPagedAsync(
        int? nam, byte? maSCV, string? tuKhoa, byte? filterMaDV, short? maNVHienTai,
        byte? maDVXL, string? nguoiKy, DateTime? tuNgay, DateTime? denNgay, int page, int pageSize,
        bool chiCaNhanVaDungChung = false)
    {
        var where = "WHERE 1=1";
        var p = new Dapper.DynamicParameters();
        p.Add("maNVHienTai", maNVHienTai);
        if (nam.HasValue) { where += " AND YEAR(cvd.NgayDen)=@nam"; p.Add("nam", nam); }
        if (maSCV.HasValue) { where += " AND cvd.MaSCV=@maSCV"; p.Add("maSCV", maSCV); }
        if (!string.IsNullOrEmpty(tuKhoa))
        {
            // Tìm cả trong nội dung file PDF đính kèm đã trích xuất (đọc trực tiếp hoặc OCR) — cùng 1 ô
            // tìm kiếm, người dùng không cần biết có lớp trích xuất riêng phía sau.
            where += @" AND (cvd.TrichYeu LIKE @tk OR cvd.STT1 LIKE @tk OR cq.TenCQ LIKE @tk
                        OR EXISTS(SELECT 1 FROM CVDenFile f WHERE f.MSCV=cvd.MSCV AND f.NoiDungTrichXuat LIKE @tk))";
            p.Add("tk", $"%{tuKhoa}%");
        }
        if (chiCaNhanVaDungChung)
        {
            // Viên chức thường: chỉ văn bản dùng chung, hoặc đã chuyển/giao cho chính mình.
            where += @" AND (cvd.DungChung=1
                        OR EXISTS(SELECT 1 FROM VanBan_XuLy x WHERE x.LoaiVB=1 AND x.MSCV=cvd.MSCV AND x.MaNVNhan=@maNVHienTai AND x.TrangThai<>5)
                        OR EXISTS(SELECT 1 FROM CongViec cv2 WHERE cv2.LoaiNguonGoc=1 AND cv2.MSCVGoc=cvd.MSCV
                                  AND (cv2.MaNVChuTri=@maNVHienTai OR (',' + ISNULL(cv2.NguoiPhoiHop,'') + ',') LIKE '%,' + CAST(@maNVHienTai AS varchar) + ',%')))";
        }
        else if (filterMaDV.HasValue)
        {
            where += " AND (cvd.MaDVXL=@filterMaDV OR (',' + ISNULL(cvd.BoPhanPhoiHop,'') + ',') LIKE '%,' + CAST(@filterMaDV AS varchar) + ',%' OR cvd.DungChung=1)";
            p.Add("filterMaDV", filterMaDV);
        }
        if (maDVXL.HasValue) { where += " AND cvd.MaDVXL=@maDVXL"; p.Add("maDVXL", maDVXL); }
        if (!string.IsNullOrEmpty(nguoiKy)) { where += " AND cvd.NguoiKy LIKE @nguoiKy"; p.Add("nguoiKy", $"%{nguoiKy}%"); }
        if (tuNgay.HasValue) { where += " AND cvd.NgayDen >= @tuNgay"; p.Add("tuNgay", tuNgay); }
        if (denNgay.HasValue) { where += " AND cvd.NgayDen < @denNgay"; p.Add("denNgay", denNgay.Value.Date.AddDays(1)); }

        return await QueryCongVanDenPagedAsync(where, "cvd.NgayDen DESC, cvd.STT DESC", p, page, pageSize);
    }

    public async Task<(List<CongVanDen> Items, int Total)> GetCVDenDangXuLyPagedAsync(byte? maDV, short? maNVHienTai, int page, int pageSize)
    {
        var where = "WHERE cvd.NgayHT IS NULL AND cvd.NgayYCHT IS NOT NULL";
        var p = new Dapper.DynamicParameters();
        p.Add("maNVHienTai", maNVHienTai);
        if (maDV.HasValue)
        {
            where += " AND (cvd.MaDVXL=@maDV OR (',' + ISNULL(cvd.BoPhanPhoiHop,'') + ',') LIKE '%,' + CAST(@maDV AS varchar) + ',%')";
            p.Add("maDV", maDV);
        }
        return await QueryCongVanDenPagedAsync(where, "cvd.NgayYCHT", p, page, pageSize);
    }

    public async Task<(List<CongVanDen> Items, int Total)> GetCVDenQuaHanPagedAsync(byte? maDV, short? maNVHienTai, int page, int pageSize)
    {
        var where = "WHERE cvd.NgayYCHT IS NOT NULL AND cvd.NgayHT IS NULL AND cvd.NgayYCHT<GETDATE()";
        var p = new Dapper.DynamicParameters();
        p.Add("maNVHienTai", maNVHienTai);
        if (maDV.HasValue)
        {
            where += " AND (cvd.MaDVXL=@maDV OR (',' + ISNULL(cvd.BoPhanPhoiHop,'') + ',') LIKE '%,' + CAST(@maDV AS varchar) + ',%')";
            p.Add("maDV", maDV);
        }
        return await QueryCongVanDenPagedAsync(where, "cvd.NgayYCHT", p, page, pageSize);
    }

    public async Task<(List<CongVanDen> Items, int Total)> GetCVDenHoanThanhPagedAsync(bool dungHan, byte? maDV, short? maNVHienTai, int page, int pageSize)
    {
        var op = dungHan ? "<=" : ">";
        var where = $"WHERE cvd.NgayHT IS NOT NULL AND cvd.NgayYCHT IS NOT NULL AND cvd.NgayHT{op}cvd.NgayYCHT";
        var p = new Dapper.DynamicParameters();
        p.Add("maNVHienTai", maNVHienTai);
        if (maDV.HasValue)
        {
            where += " AND (cvd.MaDVXL=@maDV OR (',' + ISNULL(cvd.BoPhanPhoiHop,'') + ',') LIKE '%,' + CAST(@maDV AS varchar) + ',%')";
            p.Add("maDV", maDV);
        }
        return await QueryCongVanDenPagedAsync(where, "cvd.NgayHT DESC", p, page, pageSize);
    }

    // Văn bản đến trong N ngày gần đây mà đơn vị xử lý chính chưa mở xem (CongVanDenXuLyDV.NgayXem
    // IS NULL) — để văn thư/lãnh đạo biết đơn vị nào chưa tiếp nhận thông tin, không chỉ dựa vào
    // trạng thái xử lý. filterMaDV=null (admin/văn thư) thấy toàn trường, có giá trị thì chỉ đơn vị đó.
    public async Task<List<CongVanDen>> GetVanBanDonViChuaXemAsync(byte? filterMaDV, int soNgay = 30)
    {
        using var db = Open();
        var where = "WHERE cvd.MaDVXL IS NOT NULL AND cvd.NgayDen >= DATEADD(DAY, -@soNgay, CAST(GETDATE() AS date)) " +
                    "AND NOT EXISTS(SELECT 1 FROM CongVanDenXuLyDV x WHERE x.MSCV=cvd.MSCV AND x.MaDV=cvd.MaDVXL AND x.LoaiDV=1 AND x.NgayXem IS NOT NULL)";
        if (filterMaDV.HasValue) where += " AND cvd.MaDVXL=@filterMaDV";

        var sql = $@"SELECT cvd.*, dv.TenDV AS TenDVXL, ISNULL(dv.TenTat, dv.TenDV) AS TenDVTat
                     FROM CongVanDen cvd
                     LEFT JOIN DonVi dv ON cvd.MaDVXL=dv.MaDV
                     {where} ORDER BY cvd.NgayDen DESC";
        return (await db.QueryAsync<CongVanDen>(sql, new { filterMaDV, soNgay })).ToList();
    }

    public async Task<int> DemCVChuaXemAsync(byte maDV, short maNV)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM CongVanDen cvd " +
            "WHERE (cvd.MaDVXL=@maDV OR (',' + ISNULL(cvd.BoPhanPhoiHop,'') + ',') LIKE '%,' + CAST(@maDV AS varchar) + ',%') " +
            "AND NOT EXISTS(SELECT 1 FROM CVDen_DaXem dx WHERE dx.MSCV=cvd.MSCV AND dx.MaNV=@maNV) " +
            "AND YEAR(cvd.NgayDen)=YEAR(GETDATE())",
            new { maDV, maNV });
    }

    public async Task<List<CongVanDen>> GetCVMoiChuaXemAsync(byte maDV, short maNV, int top = 5)
    {
        using var db = Open();
        return (await db.QueryAsync<CongVanDen>(
            $"SELECT TOP(@top) cvd.MSCV, cvd.STT1, cvd.STT, cvd.TrichYeu, cvd.NgayDen, cq.TenCQ " +
            $"FROM CongVanDen cvd LEFT JOIN CoQuan cq ON cvd.MaCQ=cq.MaCQ " +
            $"WHERE (cvd.MaDVXL=@maDV OR (',' + ISNULL(cvd.BoPhanPhoiHop,'') + ',') LIKE '%,' + CAST(@maDV AS varchar) + ',%') " +
            $"AND NOT EXISTS(SELECT 1 FROM CVDen_DaXem dx WHERE dx.MSCV=cvd.MSCV AND dx.MaNV=@maNV) " +
            $"AND YEAR(cvd.NgayDen)=YEAR(GETDATE()) " +
            $"ORDER BY cvd.NgayDen DESC, cvd.STT DESC",
            new { maDV, maNV, top })).ToList();
    }

    public async Task DanhDauDaXemAsync(string mscv, short maNV)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "IF NOT EXISTS(SELECT 1 FROM CVDen_DaXem WHERE MSCV=@mscv AND MaNV=@maNV) " +
            "INSERT INTO CVDen_DaXem(MSCV,MaNV) VALUES(@mscv,@maNV)", new { mscv, maNV });
    }

    public async Task DanhDauDaGuiEmailAsync(string mscv, short maNV)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "UPDATE CongVanDen SET NgayGuiEmail=GETDATE(), MaNVGuiEmail=@maNV WHERE MSCV=@mscv",
            new { mscv, maNV });
    }

    public async Task<CongVanDen?> GetCongVanDenByIdAsync(string mscv)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<CongVanDen>(
            @"SELECT cvd.*, cq.TenCQ, lv.TenLVB, sc.TenSCV, nc.TenNCV, dv.TenDV AS TenDVXL,
              nv.HoNV+' '+nv.TenNV AS TenLDXem,
              nvge.HoNV+' '+nvge.TenNV AS TenNVGuiEmail,
              (SELECT MAX(x.TrangThai) FROM CongVanDenXuLyDV x WHERE x.MSCV=cvd.MSCV) AS MaxXuLyTrangThai,
              (SELECT x.NgayXem FROM CongVanDenXuLyDV x WHERE x.MSCV=cvd.MSCV AND x.MaDV=cvd.MaDVXL AND x.LoaiDV=1) AS DonViDaXemLuc
              FROM CongVanDen cvd
              LEFT JOIN CoQuan cq ON cvd.MaCQ=cq.MaCQ
              LEFT JOIN LoaiVB lv ON cvd.MaLVB=lv.MaLVB
              LEFT JOIN SoCV sc ON cvd.MaSCV=sc.MaSCV
              LEFT JOIN NhomCV nc ON cvd.MaNCV=nc.MaNCV
              LEFT JOIN DonVi dv ON cvd.MaDVXL=dv.MaDV
              LEFT JOIN NhanVien nv ON cvd.MaLDXem=nv.MaNV
              LEFT JOIN NhanVien nvge ON cvd.MaNVGuiEmail=nvge.MaNV
              WHERE cvd.MSCV=@mscv", new { mscv });
    }

    public async Task<string> ThemCongVanDenAsync(CongVanDen cv, short maVT)
    {
        using var db = Open();
        // MSCV = YYYYMMseq (10 chars), seq tăng trong tháng
        const string sql = @"
            DECLARE @base nchar(6) = FORMAT(@NgayDen, 'yyyyMM');
            DECLARE @seq  int = ISNULL(
                (SELECT MAX(CAST(RIGHT(RTRIM(MSCV),4) AS int)) FROM CongVanDen WHERE LEFT(RTRIM(MSCV),6) = @base),
                0) + 1;
            DECLARE @mscv nchar(10) = @base + RIGHT('0000' + CAST(@seq AS nvarchar(4)), 4);
            INSERT INTO CongVanDen
                (MSCV, SoCV, NgayDen, STT, STT1, MaCQ, NgayBanHanh, TrichYeu, NguoiKy,
                 GhiChu, MaLVB, MaSCV, MaNCV, MaLDXem, MaDVXL, BoPhanPhoiHop,
                 NgayGiao, NgayYCHT, MaVT, NgayNhap, MSCVDi)
            VALUES
                (@mscv, @SoCV, @NgayDen, @STT, @STT1,
                 NULLIF(@MaCQ,   0), @NgayBanHanh, @TrichYeu, NULLIF(@NguoiKy, ''),
                 @GhiChu, NULLIF(@MaLVB, 0), @MaSCV, NULLIF(@MaNCV, 0),
                 NULLIF(@MaLDXem, 0), NULLIF(@MaDVXL, 0), @BoPhanPhoiHop,
                 @NgayGiao, @NgayYCHT, @MaVT, GETDATE(), NULLIF(@MSCVDi,''));
            SELECT @mscv;";
        return (await db.ExecuteScalarAsync<string>(sql, new
        {
            cv.NgayDen, cv.SoCV, cv.STT, cv.STT1,
            MaCQ = (object?)cv.MaCQ ?? DBNull.Value,
            cv.NgayBanHanh, cv.TrichYeu,
            NguoiKy = cv.NguoiKy ?? "",
            cv.GhiChu,
            MaLVB = (object?)cv.MaLVB ?? DBNull.Value,
            cv.MaSCV,
            MaNCV = (object?)cv.MaNCV ?? DBNull.Value,
            MaLDXem = (object?)cv.MaLDXem ?? DBNull.Value,
            MaDVXL = (object?)cv.MaDVXL ?? DBNull.Value,
            cv.BoPhanPhoiHop, cv.NgayGiao, cv.NgayYCHT,
            MaVT = maVT,
            MSCVDi = cv.MSCVDi ?? ""
        }))!;
    }

    public async Task SuaCongVanDenAsync(CongVanDen cv)
    {
        using var db = Open();
        const string sql = @"
            UPDATE CongVanDen SET
                SoCV=@SoCV, NgayDen=@NgayDen, STT=@STT, STT1=@STT1,
                MaCQ=NULLIF(@MaCQ,0), NgayBanHanh=@NgayBanHanh, TrichYeu=@TrichYeu,
                NguoiKy=NULLIF(@NguoiKy,''), GhiChu=@GhiChu,
                MaLVB=NULLIF(@MaLVB,0), MaSCV=@MaSCV, MaNCV=NULLIF(@MaNCV,0),
                MaLDXem=NULLIF(@MaLDXem,0), MaDVXL=NULLIF(@MaDVXL,0),
                BoPhanPhoiHop=@BoPhanPhoiHop, NgayGiao=@NgayGiao, NgayYCHT=@NgayYCHT
            WHERE MSCV=@MSCV";
        await db.ExecuteAsync(sql, new
        {
            cv.MSCV, cv.SoCV, cv.NgayDen, cv.STT, cv.STT1,
            MaCQ    = (object?)cv.MaCQ    ?? DBNull.Value,
            cv.NgayBanHanh, cv.TrichYeu,
            NguoiKy = cv.NguoiKy ?? "",
            cv.GhiChu,
            MaLVB   = (object?)cv.MaLVB   ?? DBNull.Value,
            cv.MaSCV,
            MaNCV   = (object?)cv.MaNCV   ?? DBNull.Value,
            MaLDXem = (object?)cv.MaLDXem ?? DBNull.Value,
            MaDVXL  = (object?)cv.MaDVXL  ?? DBNull.Value,
            cv.BoPhanPhoiHop, cv.NgayGiao, cv.NgayYCHT
        });
    }

    // BUG dữ liệu đã vá 2026-09-12: TRƯỚC ĐÂY chỉ xóa 5 bảng con, thiếu CVDenXuLyDV_File,
    // CVDen_DaXem, CongVanDen_ChiDao (bản ghi "Chỉ đạo & phân công") và TrinhKy/TrinhKy_Cap (nếu
    // văn bản này từng được trình ký) — các bảng đó KHÔNG có ràng buộc khóa ngoại tới CongVanDen
    // nên xóa xong CongVanDen vẫn "thành công", nhưng để lại rác mồ côi vĩnh viễn tham chiếu tới 1
    // MSCV không còn tồn tại. Cùng danh sách bảng đã dùng đúng ở XoaCongVanDiAsync (nhánh xóa "văn
    // bản đến mirror") — hợp nhất lại 1 chỗ cho khỏi lệch nhau lần nữa. Bọc transaction để không bao
    // giờ xóa dở dang (trước đây nhiều câu DELETE nối bằng ';' chạy autocommit riêng lẻ — 1 câu giữa
    // chừng lỗi thì các câu trước đã lỡ commit rồi, để lại 1 bản ghi CongVanDen "mồ côi" không ai xử
    // lý được nữa). Trả về danh sách đường dẫn file vật lý (đính kèm + file kết quả xử lý) để
    // controller xóa nốt trên đĩa SAU KHI DB đã xóa xong.
    public async Task<List<string>> XoaCongVanDenAsync(string mscv)
    {
        using var db = Open();
        db.Open();
        using var tx = db.BeginTransaction();
        try
        {
            var duongDanFile = (await db.QueryAsync<string>(
                "SELECT DuongDan FROM CVDenFile WHERE MSCV=@mscv " +
                "UNION ALL " +
                "SELECT DuongDan FROM CVDenXuLyDV_File WHERE XuLyDVID IN (SELECT ID FROM CongVanDenXuLyDV WHERE MSCV=@mscv)",
                new { mscv }, tx)).Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

            await db.ExecuteAsync(
                "DELETE FROM CVDenXuLyDV_File WHERE XuLyDVID IN (SELECT ID FROM CongVanDenXuLyDV WHERE MSCV=@mscv); " +
                "DELETE FROM CongVanDenXuLyDV WHERE MSCV=@mscv; " +
                "DELETE FROM CVDen_DaXem      WHERE MSCV=@mscv; " +
                "DELETE FROM CVDen_DV_NV      WHERE MSCV=@mscv; " +
                "DELETE FROM CVDen_QTXL       WHERE MSCV=@mscv; " +
                "DELETE FROM CVDen_DV         WHERE MSCV=@mscv; " +
                "DELETE FROM CVDenFile        WHERE MSCV=@mscv; " +
                "DELETE FROM CongVanDen_ChiDao WHERE MSCV=@mscv; " +
                "DELETE FROM TrinhKy_Cap WHERE TrinhKyID IN (SELECT ID FROM TrinhKy WHERE LoaiVanBan=1 AND MSCV=@mscv); " +
                "DELETE FROM TrinhKy     WHERE LoaiVanBan=1 AND MSCV=@mscv; " +
                "DELETE FROM CongVanDen  WHERE MSCV=@mscv;", new { mscv }, tx);

            tx.Commit();
            return duongDanFile;
        }
        catch { tx.Rollback(); throw; }
    }

    // ── Theo dõi xử lý theo từng đơn vị ────────────────────────────────────
    public async Task<List<CongVanDenXuLyDV>> GetXuLyDVAsync(string mscv)
    {
        using var db = Open();
        return (await db.QueryAsync<CongVanDenXuLyDV>(
            @"SELECT x.*, dv.TenDV, ISNULL(dv.TenTat, dv.TenDV) AS TenDVTat,
                     dvct.TenDV AS TenChuyenToi,
                     (SELECT COUNT(1) FROM CVDenXuLyDV_File f WHERE f.XuLyDVID = x.ID) AS SoFile
              FROM CongVanDenXuLyDV x
              LEFT JOIN DonVi dv ON x.MaDV = dv.MaDV
              LEFT JOIN DonVi dvct ON x.ChuyenToiMaDV = dvct.MaDV
              WHERE x.MSCV = @mscv
              ORDER BY x.LoaiDV, dv.TenDV",
            new { mscv })).ToList();
    }

    public async Task<CongVanDenXuLyDV?> GetXuLyDVByIdAsync(int id)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<CongVanDenXuLyDV>(
            "SELECT * FROM CongVanDenXuLyDV WHERE ID=@id", new { id });
    }

    public async Task TaoXuLyDVAsync(string mscv, byte? maDVXL, string? boPhanPhoiHop)
    {
        using var db = Open();
        if (maDVXL.HasValue && maDVXL > 0)
        {
            var exists = await db.QueryFirstOrDefaultAsync<int>(
                "SELECT COUNT(1) FROM CongVanDenXuLyDV WHERE MSCV=@mscv AND MaDV=@maDV",
                new { mscv, maDV = maDVXL.Value });
            if (exists == 0)
            {
                await db.ExecuteAsync(
                    "INSERT INTO CongVanDenXuLyDV (MSCV,MaDV,LoaiDV,TrangThai) VALUES(@mscv,@maDV,1,0)",
                    new { mscv, maDV = maDVXL.Value });
                await ThemThongBaoCaNhanNoiBoAsync(db, maDVXL.Value, null, 1,
                    $"Văn bản {mscv} vừa được giao cho đơn vị bạn xử lý (chủ trì)", mscv);
            }
        }
        if (!string.IsNullOrEmpty(boPhanPhoiHop))
        {
            foreach (var part in boPhanPhoiHop.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!byte.TryParse(part.Trim(), out var maDV) || maDV == (maDVXL ?? 0)) continue;
                var exists = await db.QueryFirstOrDefaultAsync<int>(
                    "SELECT COUNT(1) FROM CongVanDenXuLyDV WHERE MSCV=@mscv AND MaDV=@maDV",
                    new { mscv, maDV });
                if (exists == 0)
                {
                    await db.ExecuteAsync(
                        "INSERT INTO CongVanDenXuLyDV (MSCV,MaDV,LoaiDV,TrangThai) VALUES(@mscv,@maDV,2,0)",
                        new { mscv, maDV });
                    await ThemThongBaoCaNhanNoiBoAsync(db, maDV, null, 1,
                        $"Văn bản {mscv} vừa được giao cho đơn vị bạn phối hợp xử lý", mscv);
                }
            }
        }
    }

    public async Task DanhDauDaXemXuLyDVAsync(string mscv, byte maDV)
    {
        using var db = Open();
        await db.ExecuteAsync(
            @"UPDATE CongVanDenXuLyDV
              SET NgayXem = COALESCE(NgayXem, GETDATE())
              WHERE MSCV=@mscv AND MaDV=@maDV",
            new { mscv, maDV });
    }

    public async Task CapNhatTrangThaiXuLyDVAsync(string mscv, byte maDV, byte trangThai, short maNV, string? ghiChu)
    {
        using var db = Open();
        await db.ExecuteAsync(
            @"UPDATE CongVanDenXuLyDV SET
                TrangThai     = @trangThai,
                NgayCapNhat   = GETDATE(),
                MaNVCapNhat   = @maNV,
                GhiChu        = ISNULL(NULLIF(@ghiChu,''), GhiChu),
                NgayTiepNhan  = CASE WHEN @trangThai>=1 AND NgayTiepNhan IS NULL THEN GETDATE() ELSE NgayTiepNhan END,
                NgayHoanThanh = CASE WHEN @trangThai=3 THEN GETDATE() ELSE NgayHoanThanh END
              WHERE MSCV=@mscv AND MaDV=@maDV",
            new { mscv, maDV, trangThai, maNV, ghiChu = ghiChu ?? "" });

        // Đồng bộ với cơ chế hoàn thành cũ ở mức văn bản (CongVanDen.NgayHT): khi TẤT CẢ đơn vị
        // được giao đều đã Hoàn thành (TrangThai=3), tự động đóng văn bản luôn — tránh tình trạng
        // "Tình trạng liên thông đơn vị" báo xong hết nhưng card "Xử lý văn bản" vẫn báo chưa hoàn thành.
        if (trangThai == 3)
        {
            // TrangThai=3 (Hoàn thành) và 4 (Đã chuyển tiếp — thay bởi dòng mới) không chặn tự-đóng;
            // TrangThai=5 (Đã trả lại) VẪN chặn vì việc chưa thực sự xong, chỉ đang chờ phân công lại.
            var conLai = await db.QueryFirstOrDefaultAsync<int>(
                "SELECT COUNT(1) FROM CongVanDenXuLyDV WHERE MSCV=@mscv AND TrangThai NOT IN (3,4)", new { mscv });
            if (conLai == 0)
                await db.ExecuteAsync(
                    "UPDATE CongVanDen SET NgayHT=ISNULL(NgayHT,GETDATE()) WHERE MSCV=@mscv", new { mscv });
        }
    }

    // ── File kết quả xử lý (nhiều file/dòng, xem migrations/025_xulydv_multi_file.sql) ─────────────
    public async Task<Dictionary<int, List<CVDenXuLyDVFile>>> GetXuLyDVFilesByMscvAsync(string mscv)
    {
        using var db = Open();
        var rows = await db.QueryAsync<CVDenXuLyDVFile>(
            @"SELECT f.*, (nv.HoNV+' '+nv.TenNV) AS TenNVUpload
              FROM CVDenXuLyDV_File f
              JOIN CongVanDenXuLyDV x ON f.XuLyDVID = x.ID
              LEFT JOIN NhanVien nv ON f.MaNVUpload = nv.MaNV
              WHERE x.MSCV = @mscv ORDER BY f.NgayUpload", new { mscv });
        return rows.GroupBy(r => r.XuLyDVID).ToDictionary(g => g.Key, g => g.ToList());
    }

    public async Task<CVDenXuLyDVFile?> GetXuLyDVFileByIdAsync(int fileId)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<CVDenXuLyDVFile>(
            "SELECT * FROM CVDenXuLyDV_File WHERE ID=@fileId", new { fileId });
    }

    public async Task<int> ThemXuLyDVFileAsync(int xuLyDVId, string tenFile, string duongDan, short maNVUpload)
    {
        using var db = Open();
        var newId = await db.ExecuteScalarAsync<int>(
            @"INSERT INTO CVDenXuLyDV_File (XuLyDVID, TenFile, DuongDan, NgayUpload, MaNVUpload) OUTPUT INSERTED.ID
              VALUES (@xuLyDVId, @tenFile, @duongDan, GETDATE(), @maNVUpload);",
            new { xuLyDVId, tenFile, duongDan, maNVUpload });
        await db.ExecuteAsync("UPDATE CongVanDenXuLyDV SET NgayCapNhat=GETDATE(), MaNVCapNhat=@maNVUpload WHERE ID=@xuLyDVId",
            new { xuLyDVId, maNVUpload });
        return newId;
    }

    public async Task XoaXuLyDVFileAsync(int fileId)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM CVDenXuLyDV_File WHERE ID=@fileId", new { fileId });
    }

    public async Task<int> XacNhanHoanThanhTatCaQuaHanAsync(short maNV)
    {
        using var db = Open();
        return await db.ExecuteAsync(
            @"UPDATE CongVanDen
              SET NgayXNHTCV=GETDATE(), MaNVXNHTCV=@maNV, KQXN=1, NgayHT=ISNULL(NgayHT,GETDATE())
              WHERE NgayYCHT < GETDATE() AND NgayXNHTCV IS NULL",
            new { maNV });
    }

    public async Task<List<CongVanDen>> GetCVDenDangXuLyAsync(byte? maDV = null, short? maNV = null, short? maNVHienTai = null)
    {
        using var db = Open();
        var where = "WHERE cvd.NgayHT IS NULL AND cvd.NgayYCHT IS NOT NULL";
        if (maDV.HasValue)
            where += " AND (cvd.MaDVXL=@maDV OR (',' + ISNULL(cvd.BoPhanPhoiHop,'') + ',') LIKE '%,' + CAST(@maDV AS varchar) + ',%')";
        if (maNV.HasValue) where += " AND EXISTS(SELECT 1 FROM CVDen_DV dd WHERE dd.MSCV=cvd.MSCV AND dd.MaNV=@maNV)";
        return (await db.QueryAsync<CongVanDen>(
            $"SELECT cvd.*, dv.TenDV AS TenDVXL, ISNULL(dv.TenTat,dv.TenDV) AS TenDVTat, cq.TenCQ, " +
            $"nv.HoNV+' '+nv.TenNV AS TenLDXem, nvld.HoNV+' '+nvld.TenNV AS TenLDPhong, " +
            $"nvvt.HoNV+' '+nvvt.TenNV AS TenVT, nvxn.HoNV+' '+nvxn.TenNV AS TenNVXNHT, " +
            $"CASE WHEN EXISTS(SELECT 1 FROM CVDen_DaXem dx WHERE dx.MSCV=cvd.MSCV AND dx.MaNV=@maNVHienTai) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS DaXem, " +
            $"CASE WHEN EXISTS(SELECT 1 FROM CVDenFile f WHERE f.MSCV=cvd.MSCV) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS CoFile, " +
            $"(SELECT MAX(x.TrangThai) FROM CongVanDenXuLyDV x WHERE x.MSCV=cvd.MSCV) AS MaxXuLyTrangThai " +
            $"FROM CongVanDen cvd " +
            $"LEFT JOIN DonVi dv ON cvd.MaDVXL=dv.MaDV LEFT JOIN CoQuan cq ON cvd.MaCQ=cq.MaCQ " +
            $"LEFT JOIN NhanVien nv ON cvd.MaLDXem=nv.MaNV " +
            $"LEFT JOIN NhanVien nvld ON dv.MaNV_TheoDoiCV=nvld.MaNV " +
            $"LEFT JOIN NhanVien nvvt ON cvd.MaVT=nvvt.MaNV " +
            $"LEFT JOIN NhanVien nvxn ON cvd.MaNVXNHTCV=nvxn.MaNV " +
            $"{where} ORDER BY cvd.NgayYCHT", new { maDV, maNV, maNVHienTai })).ToList();
    }

    public async Task<List<CongVanDen>> GetCVDenHoanThanhAsync(bool dungHan, byte? maDV = null, short? maNVHienTai = null)
    {
        using var db = Open();
        var op = dungHan ? "<=" : ">";
        var dvFilter = maDV.HasValue
            ? "AND (cvd.MaDVXL=@maDV OR (',' + ISNULL(cvd.BoPhanPhoiHop,'') + ',') LIKE '%,' + CAST(@maDV AS varchar) + ',%') "
            : "";
        return (await db.QueryAsync<CongVanDen>(
            $"SELECT cvd.*, dv.TenDV AS TenDVXL, ISNULL(dv.TenTat,dv.TenDV) AS TenDVTat, cq.TenCQ, " +
            $"nv.HoNV+' '+nv.TenNV AS TenLDXem, nvld.HoNV+' '+nvld.TenNV AS TenLDPhong, " +
            $"nvvt.HoNV+' '+nvvt.TenNV AS TenVT, nvxn.HoNV+' '+nvxn.TenNV AS TenNVXNHT, " +
            $"CASE WHEN EXISTS(SELECT 1 FROM CVDen_DaXem dx WHERE dx.MSCV=cvd.MSCV AND dx.MaNV=@maNVHienTai) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS DaXem, " +
            $"CASE WHEN EXISTS(SELECT 1 FROM CVDenFile f WHERE f.MSCV=cvd.MSCV) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS CoFile, " +
            $"(SELECT MAX(x.TrangThai) FROM CongVanDenXuLyDV x WHERE x.MSCV=cvd.MSCV) AS MaxXuLyTrangThai " +
            $"FROM CongVanDen cvd " +
            $"LEFT JOIN DonVi dv ON cvd.MaDVXL=dv.MaDV LEFT JOIN CoQuan cq ON cvd.MaCQ=cq.MaCQ " +
            $"LEFT JOIN NhanVien nv ON cvd.MaLDXem=nv.MaNV " +
            $"LEFT JOIN NhanVien nvld ON dv.MaNV_TheoDoiCV=nvld.MaNV " +
            $"LEFT JOIN NhanVien nvvt ON cvd.MaVT=nvvt.MaNV " +
            $"LEFT JOIN NhanVien nvxn ON cvd.MaNVXNHTCV=nvxn.MaNV " +
            $"WHERE cvd.NgayHT IS NOT NULL AND cvd.NgayYCHT IS NOT NULL AND cvd.NgayHT{op}cvd.NgayYCHT " +
            dvFilter + "ORDER BY cvd.NgayHT DESC", new { maDV, maNVHienTai })).ToList();
    }

    public async Task<List<CongVanDen>> GetCVDenQuaHanAsync(byte? maDV = null, short? maNVHienTai = null)
    {
        using var db = Open();
        var dvFilter = maDV.HasValue
            ? "AND (cvd.MaDVXL=@maDV OR (',' + ISNULL(cvd.BoPhanPhoiHop,'') + ',') LIKE '%,' + CAST(@maDV AS varchar) + ',%') "
            : "";
        return (await db.QueryAsync<CongVanDen>(
            "SELECT cvd.*, dv.TenDV AS TenDVXL, ISNULL(dv.TenTat,dv.TenDV) AS TenDVTat, cq.TenCQ, " +
            "nv.HoNV+' '+nv.TenNV AS TenLDXem, nvld.HoNV+' '+nvld.TenNV AS TenLDPhong, " +
            "nvvt.HoNV+' '+nvvt.TenNV AS TenVT, nvxn.HoNV+' '+nvxn.TenNV AS TenNVXNHT, " +
            "CASE WHEN EXISTS(SELECT 1 FROM CVDen_DaXem dx WHERE dx.MSCV=cvd.MSCV AND dx.MaNV=@maNVHienTai) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS DaXem, " +
            "CASE WHEN EXISTS(SELECT 1 FROM CVDenFile f WHERE f.MSCV=cvd.MSCV) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS CoFile, " +
            "(SELECT MAX(x.TrangThai) FROM CongVanDenXuLyDV x WHERE x.MSCV=cvd.MSCV) AS MaxXuLyTrangThai " +
            "FROM CongVanDen cvd " +
            "LEFT JOIN DonVi dv ON cvd.MaDVXL=dv.MaDV LEFT JOIN CoQuan cq ON cvd.MaCQ=cq.MaCQ " +
            "LEFT JOIN NhanVien nv ON cvd.MaLDXem=nv.MaNV " +
            "LEFT JOIN NhanVien nvld ON dv.MaNV_TheoDoiCV=nvld.MaNV " +
            "LEFT JOIN NhanVien nvvt ON cvd.MaVT=nvvt.MaNV " +
            "LEFT JOIN NhanVien nvxn ON cvd.MaNVXNHTCV=nvxn.MaNV " +
            "WHERE cvd.NgayYCHT IS NOT NULL AND cvd.NgayHT IS NULL AND cvd.NgayYCHT<GETDATE() " +
            dvFilter + "ORDER BY cvd.NgayYCHT", new { maDV, maNVHienTai })).ToList();
    }

    public async Task XacNhanHoanThanhAsync(string mscv, short maNV)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "UPDATE CongVanDen SET NgayXNHTCV=GETDATE(), MaNVXNHTCV=@maNV, KQXN=1, NgayHT=ISNULL(NgayHT,GETDATE()) WHERE MSCV=@mscv",
            new { mscv, maNV });
    }

    public async Task HuyXacNhanHTAsync(string mscv)
    {
        using var db = Open();
        // NgayHT có thể đến từ 2 nguồn: xác nhận thủ công (đang hủy ở đây) HOẶC mọi đơn vị đã tự
        // hoàn thành xong xuôi (TrangThai=3) — chỉ xóa NgayHT nếu còn ít nhất 1 đơn vị thực sự
        // chưa xong; nếu mọi đơn vị đã xong thật thì văn bản vẫn đúng là đã hoàn thành, giữ nguyên.
        await db.ExecuteAsync(
            @"UPDATE CongVanDen SET NgayXNHTCV=NULL, MaNVXNHTCV=NULL, KQXN=NULL,
                NgayHT = CASE WHEN EXISTS(SELECT 1 FROM CongVanDenXuLyDV WHERE MSCV=@mscv AND TrangThai NOT IN (3,4))
                              THEN NULL ELSE NgayHT END
              WHERE MSCV=@mscv",
            new { mscv });
    }

    public async Task<List<CVDen_DV>> GetCVDenDVAsync(string mscv)
    {
        using var db = Open();
        return (await db.QueryAsync<CVDen_DV>(
            "SELECT cd.*, dv.TenDV, nv.HoNV+' '+nv.TenNV AS HoTenNV " +
            "FROM CVDen_DV cd LEFT JOIN DonVi dv ON cd.MaDV=dv.MaDV " +
            "LEFT JOIN NhanVien nv ON cd.MaNV=nv.MaNV WHERE cd.MSCV=@mscv",
            new { mscv })).ToList();
    }

    public async Task<List<CVDen_QTXL>> GetLichSuXuLyAsync(string mscv)
    {
        using var db = Open();
        return (await db.QueryAsync<CVDen_QTXL>(
            "SELECT q.*, nv.HoNV+' '+nv.TenNV AS HoTenNV, dv.TenDV " +
            "FROM CVDen_QTXL q LEFT JOIN NhanVien nv ON q.MaNV=nv.MaNV " +
            "LEFT JOIN DonVi dv ON q.MaDV=dv.MaDV WHERE q.MSCV=@mscv ORDER BY q.NgayXL DESC",
            new { mscv })).ToList();
    }

    // ── Công văn đi ─────────────────────────────────────────────────────────
    public async Task<List<CongVanDi>> GetCongVanDiAsync(int? nam = null, byte? maSCV = null, string? tuKhoa = null, bool chiCongKhai = false)
    {
        using var db = Open();
        var where = "WHERE 1=1";
        if (nam.HasValue) where += " AND YEAR(cvdi.NgayCongVan)=@nam";
        if (maSCV.HasValue) where += " AND cvdi.MaSCV=@maSCV";
        if (chiCongKhai) where += " AND cvdi.CongKhai=1";
        if (!string.IsNullOrEmpty(tuKhoa)) where += " AND (cvdi.TrichYeu LIKE @tk OR cvdi.STT1 LIKE @tk)";

        var sql = $@"SELECT cvdi.*, lv.TenLVB, sc.TenSCV, nc.TenNCV,
                     nv.HoNV+' '+nv.TenNV AS TenLDKy
                     FROM CongVanDi cvdi
                     LEFT JOIN LoaiVB lv ON cvdi.MaLVB=lv.MaLVB
                     LEFT JOIN SoCV sc ON cvdi.MaSCV=sc.MaSCV
                     LEFT JOIN NhomCV nc ON cvdi.MaNCV=nc.MaNCV
                     LEFT JOIN NhanVien nv ON cvdi.MaLDKy=nv.MaNV
                     {where} ORDER BY cvdi.NgayCongVan DESC, cvdi.STT DESC";
        return (await db.QueryAsync<CongVanDi>(sql, new { nam, maSCV, tk = $"%{tuKhoa}%" })).ToList();
    }

    public async Task<CongVanDi?> GetCongVanDiByIdAsync(string mscv)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<CongVanDi>(
            @"SELECT cvdi.*, lv.TenLVB, sc.TenSCV, nc.TenNCV, nv.HoNV+' '+nv.TenNV AS TenLDKy,
                     nvks.HoNV+' '+nvks.TenNV AS TenNVKySo
              FROM CongVanDi cvdi
              LEFT JOIN LoaiVB lv ON cvdi.MaLVB=lv.MaLVB
              LEFT JOIN SoCV sc ON cvdi.MaSCV=sc.MaSCV
              LEFT JOIN NhomCV nc ON cvdi.MaNCV=nc.MaNCV
              LEFT JOIN NhanVien nv ON cvdi.MaLDKy=nv.MaNV
              LEFT JOIN NhanVien nvks ON cvdi.MaNVKySo=nvks.MaNV
              WHERE cvdi.MSCV=@mscv", new { mscv });
    }

    // Ghi chú: trước đây gọi stored procedure congvandi_them/congvandi_sua nhưng các SP này
    // đã lỗi thời (thiếu @KyTu, tra cứu loại VB/nhóm VB theo TÊN vào bảng LoaiVBCVDi cũ đã bỏ dùng
    // từ khi gộp danh mục Loại văn bản) nên luôn báo lỗi 500 khi lưu. Chuyển sang SQL trực tiếp,
    // theo đúng cách CongVanDen đang làm (MSCV tự sinh theo tháng, MaLVB/MaNCV theo ID dùng chung).
    public async Task<string> ThemCongVanDiAsync(CongVanDi cv, short maVT)
    {
        using var db = Open();
        const string sql = @"
            DECLARE @base nchar(6) = FORMAT(@NgayCongVan, 'yyyyMM');
            DECLARE @seq  int = ISNULL(
                (SELECT MAX(CAST(RIGHT(RTRIM(MSCV),4) AS int)) FROM CongVanDi WHERE LEFT(RTRIM(MSCV),6) = @base),
                0) + 1;
            DECLARE @mscv nchar(10) = @base + RIGHT('0000' + CAST(@seq AS nvarchar(4)), 4);
            INSERT INTO CongVanDi
                (MSCV, STT, STT1, SoCVCT, NgayCongVan, NgayBanHanh, MaSCV, MaNCV, MaLVB,
                 TrichYeu, MaLDKy, NoiNhanCV, DonViNhan, MaVT, SoLuong, FileDinhKem, GhiChu, MSCVDen, NgayNhap, NguoiSoanThao, DonViSoanThao)
            VALUES
                (@mscv, @STT, @STT1, @SoCVCT, @NgayCongVan, @NgayBanHanh, @MaSCV, NULLIF(@MaNCV,0), @MaLVB,
                 @TrichYeu, NULLIF(@MaLDKy,0), @NoiNhanCV, @DonViNhan, @MaVT, @SoLuong, @FileDinhKem, @GhiChu, NULLIF(@MSCVDen,''), GETDATE(), @NguoiSoanThao, @DonViSoanThao);
            SELECT @mscv;";
        return (await db.ExecuteScalarAsync<string>(sql, new
        {
            cv.STT, cv.STT1, cv.SoCVCT, cv.NgayCongVan, cv.NgayBanHanh,
            cv.MaSCV, cv.MaNCV, cv.MaLVB, cv.TrichYeu, cv.MaLDKy,
            cv.NoiNhanCV, cv.DonViNhan, MaVT = maVT, cv.SoLuong, cv.FileDinhKem, cv.GhiChu,
            MSCVDen = cv.MSCVDen ?? "", cv.NguoiSoanThao, cv.DonViSoanThao
        }))!;
    }

    public async Task SuaCongVanDiAsync(CongVanDi cv)
    {
        using var db = Open();
        const string sql = @"
            UPDATE CongVanDi SET
                STT=@STT, STT1=@STT1, SoCVCT=@SoCVCT, NgayCongVan=@NgayCongVan, NgayBanHanh=@NgayBanHanh,
                MaSCV=@MaSCV, MaNCV=NULLIF(@MaNCV,0), MaLVB=@MaLVB, TrichYeu=@TrichYeu,
                MaLDKy=NULLIF(@MaLDKy,0), NoiNhanCV=@NoiNhanCV, DonViNhan=@DonViNhan, SoLuong=@SoLuong,
                FileDinhKem = CASE WHEN NULLIF(@FileDinhKem,'') IS NULL THEN FileDinhKem ELSE @FileDinhKem END,
                GhiChu=@GhiChu, MSCVDen=NULLIF(@MSCVDen,''), NguoiSoanThao=@NguoiSoanThao, DonViSoanThao=@DonViSoanThao
            WHERE MSCV=@MSCV";
        await db.ExecuteAsync(sql, new
        {
            cv.MSCV, cv.STT, cv.STT1, cv.SoCVCT, cv.NgayCongVan, cv.NgayBanHanh,
            cv.MaSCV, cv.MaNCV, cv.MaLVB, cv.TrichYeu, cv.MaLDKy,
            cv.NoiNhanCV, cv.DonViNhan, cv.SoLuong, FileDinhKem = cv.FileDinhKem ?? "", cv.GhiChu,
            MSCVDen = cv.MSCVDen ?? "", cv.NguoiSoanThao, cv.DonViSoanThao
        });
    }

    // ── "Dấu công khai" văn bản đi ────────────────────────────────────────
    public async Task DatCongKhaiCongVanDiAsync(string mscv, bool congKhai, short maNV)
    {
        using var db = Open();
        await db.ExecuteAsync(@"
            UPDATE CongVanDi SET
                CongKhai = @congKhai,
                NgayCongKhai = CASE WHEN @congKhai = 1 THEN GETDATE() ELSE NULL END,
                MaNVCongKhai = CASE WHEN @congKhai = 1 THEN @maNV ELSE NULL END
            WHERE MSCV = @mscv",
            new { mscv, congKhai, maNV });
    }

    // Danh sách văn bản đi ĐÃ CÔNG KHAI — dùng cho API công khai (không đăng nhập). Phân trang,
    // chỉ trả các trường an toàn để lộ; KHÔNG lộ nơi nhận nội bộ / ghi chú / đường dẫn file.
    public async Task<(List<CongVanDi> Items, int Total)> GetCongVanDiCongKhaiAsync(
        int? nam, byte? maSCV, short? maLVB, string? tuKhoa, int page, int pageSize)
    {
        using var db = Open();
        var where = "WHERE cvdi.CongKhai = 1";
        if (nam.HasValue)   where += " AND YEAR(cvdi.NgayCongVan) = @nam";
        if (maSCV.HasValue) where += " AND cvdi.MaSCV = @maSCV";
        if (maLVB.HasValue) where += " AND cvdi.MaLVB = @maLVB";
        if (!string.IsNullOrWhiteSpace(tuKhoa))
            where += " AND (cvdi.TrichYeu LIKE @tk OR cvdi.STT1 LIKE @tk)";

        var total = await db.ExecuteScalarAsync<int>($"SELECT COUNT(1) FROM CongVanDi cvdi {where}",
            new { nam, maSCV, maLVB, tk = $"%{tuKhoa}%" });

        var sql = $@"
            SELECT cvdi.MSCV, cvdi.STT, cvdi.STT1, cvdi.NgayCongVan, cvdi.NgayBanHanh,
                   cvdi.MaSCV, cvdi.MaLVB, cvdi.TrichYeu, cvdi.DonViSoanThao, cvdi.NgayCongKhai,
                   cvdi.FileDinhKem, cvdi.DaKySo,
                   lv.TenLVB, sc.TenSCV, nv.HoNV + ' ' + nv.TenNV AS TenLDKy
            FROM CongVanDi cvdi
            LEFT JOIN LoaiVB lv ON cvdi.MaLVB = lv.MaLVB
            LEFT JOIN SoCV sc   ON cvdi.MaSCV = sc.MaSCV
            LEFT JOIN NhanVien nv ON cvdi.MaLDKy = nv.MaNV
            {where}
            ORDER BY cvdi.NgayCongVan DESC, cvdi.STT DESC
            OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY";
        var items = (await db.QueryAsync<CongVanDi>(sql, new
        {
            nam, maSCV, maLVB, tk = $"%{tuKhoa}%",
            skip = (page - 1) * pageSize, take = pageSize
        })).ToList();
        return (items, total);
    }

    public async Task<CongVanDi?> GetCongVanDiCongKhaiByIdAsync(string mscv)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<CongVanDi>(@"
            SELECT cvdi.MSCV, cvdi.STT, cvdi.STT1, cvdi.NgayCongVan, cvdi.NgayBanHanh,
                   cvdi.MaSCV, cvdi.MaLVB, cvdi.TrichYeu, cvdi.DonViSoanThao, cvdi.SoLuong,
                   cvdi.NgayCongKhai, cvdi.FileDinhKem, cvdi.DaKySo, cvdi.LoaiChungThu,
                   lv.TenLVB, sc.TenSCV, nv.HoNV + ' ' + nv.TenNV AS TenLDKy
            FROM CongVanDi cvdi
            LEFT JOIN LoaiVB lv ON cvdi.MaLVB = lv.MaLVB
            LEFT JOIN SoCV sc   ON cvdi.MaSCV = sc.MaSCV
            LEFT JOIN NhanVien nv ON cvdi.MaLDKy = nv.MaNV
            WHERE cvdi.MSCV = @mscv AND cvdi.CongKhai = 1", new { mscv });
    }

    // Xóa văn bản đi + dọn sạch mọi bản ghi phái sinh, nếu không văn bản vẫn "sống" ở nơi khác
    // (đơn vị nhận vẫn thấy trong hộp thư đến, vẫn còn ở Sổ văn bản điều hành) — trước đây chỉ
    // DELETE mỗi bảng CongVanDi nên người dùng báo "xóa không ăn thua".
    public async Task XoaCongVanDiAsync(string mscv)
    {
        using var db = Open();

        // 1) Các văn bản đến "mirror" sinh ra từ văn bản đi này (mỗi đơn vị nội bộ 1 bản) —
        //    xóa kèm toàn bộ con của chúng theo đúng thứ tự khóa ngoại.
        var mirrors = (await db.QueryAsync<string>(
            "SELECT MSCV FROM CongVanDen WHERE MSCVDi=@mscv", new { mscv })).ToList();
        foreach (var m in mirrors)
        {
            await db.ExecuteAsync(
                "DELETE FROM CVDenXuLyDV_File WHERE XuLyDVID IN (SELECT ID FROM CongVanDenXuLyDV WHERE MSCV=@m); " +
                "DELETE FROM CongVanDenXuLyDV WHERE MSCV=@m; " +
                "DELETE FROM CVDen_DaXem   WHERE MSCV=@m; " +
                "DELETE FROM CVDen_DV_NV   WHERE MSCV=@m; " +
                "DELETE FROM CVDen_QTXL    WHERE MSCV=@m; " +
                "DELETE FROM CVDen_DV      WHERE MSCV=@m; " +
                "DELETE FROM CVDenFile     WHERE MSCV=@m; " +
                "DELETE FROM CongVanDen_ChiDao WHERE MSCV=@m; " +
                "DELETE FROM TrinhKy_Cap WHERE TrinhKyID IN (SELECT ID FROM TrinhKy WHERE LoaiVanBan=1 AND MSCV=@m); " +
                "DELETE FROM TrinhKy     WHERE LoaiVanBan=1 AND MSCV=@m; " +
                "DELETE FROM CongVanDen  WHERE MSCV=@m;", new { m });
        }

        // 2) Bản ghi Văn bản điều hành sinh từ tick "Cũng là văn bản điều hành".
        await db.ExecuteAsync(
            "DELETE FROM TrinhKy_Cap WHERE TrinhKyID IN " +
            "  (SELECT ID FROM TrinhKy WHERE LoaiVanBan=3 AND MSCV IN (SELECT MSCV FROM VanBanDieuHanh WHERE MSCVDi=@mscv)); " +
            "DELETE FROM TrinhKy WHERE LoaiVanBan=3 AND MSCV IN (SELECT MSCV FROM VanBanDieuHanh WHERE MSCVDi=@mscv); " +
            "DELETE FROM VanBanDieuHanh WHERE MSCVDi=@mscv;", new { mscv });

        // 3) Trình ký + file đính kèm của chính văn bản đi, rồi bản thân nó.
        await db.ExecuteAsync(
            "DELETE FROM TrinhKy_Cap WHERE TrinhKyID IN (SELECT ID FROM TrinhKy WHERE LoaiVanBan=2 AND MSCV=@mscv); " +
            "DELETE FROM TrinhKy WHERE LoaiVanBan=2 AND MSCV=@mscv; " +
            "DELETE FROM CongVanDi WHERE MSCV=@mscv;", new { mscv });
    }

    // Ký số: nhận file PDF đã được văn thư ký ngoài hệ thống (bằng phần mềm VGCA/VNPT-CA sẵn có
    // trên máy), thay thế file đính kèm hiện tại bằng bản đã ký, đánh dấu trạng thái.
    public async Task DanhDauDaKySoCongVanDiAsync(string mscv, string fileDaKy, string loaiChungThu, short maNV)
    {
        using var db = Open();
        await db.ExecuteAsync(
            @"UPDATE CongVanDi SET
                FileDinhKem=@fileDaKy, DaKySo=1, NgayKySo=GETDATE(), MaNVKySo=@maNV, LoaiChungThu=@loaiChungThu
              WHERE MSCV=@mscv",
            new { mscv, fileDaKy, loaiChungThu, maNV });
    }

    // ── Trình ký tuần tự nhiều cấp (dùng chung cho CongVanDi/LoaiVanBan=2 và VanBanDieuHanh=3) ──
    private async Task<TrinhKy?> LoadTrinhKyWithCapAsync(System.Data.IDbConnection db, int trinhKyId)
    {
        var tk = await db.QueryFirstOrDefaultAsync<TrinhKy>(
            @"SELECT tk.*, (nv.HoNV+' '+nv.TenNV) AS TenNVTrinh, ld.Ten AS TenLuong
              FROM TrinhKy tk
              JOIN NhanVien nv ON tk.MaNVTrinh=nv.MaNV
              LEFT JOIN LuongDuyet ld ON tk.LuongID=ld.ID
              WHERE tk.ID=@trinhKyId", new { trinhKyId });
        if (tk == null) return null;
        tk.DanhSachCap = (await db.QueryAsync<TrinhKyCap>(
            @"SELECT c.*, (nv.HoNV+' '+nv.TenNV) AS TenNVDuyet FROM TrinhKy_Cap c
              JOIN NhanVien nv ON c.MaNVDuyet=nv.MaNV WHERE c.TrinhKyID=@trinhKyId ORDER BY c.ThuTu",
            new { trinhKyId })).ToList();
        tk.NhatKy = (await db.QueryAsync<TrinhKyNhatKy>(
            @"SELECT k.*, (nv.HoNV+' '+nv.TenNV) AS TenNV FROM TrinhKy_NhatKy k
              JOIN NhanVien nv ON k.MaNV=nv.MaNV WHERE k.TrinhKyID=@trinhKyId ORDER BY k.NgayTao",
            new { trinhKyId })).ToList();
        return tk;
    }

    // ── Cấu hình luồng duyệt (mẫu trình ký) ───────────────────────────────
    public async Task<List<LuongDuyet>> GetLuongDuyetAsync(byte? loaiVanBan = null, bool chiHienThi = true)
    {
        using var db = Open();
        var where = "WHERE 1=1";
        if (loaiVanBan.HasValue) where += " AND l.LoaiVanBan=@loaiVanBan";
        if (chiHienThi) where += " AND l.HienThi=1";
        return (await db.QueryAsync<LuongDuyet>(
            $@"SELECT l.*, (SELECT COUNT(1) FROM LuongDuyet_Buoc b WHERE b.LuongID=l.ID) AS SoBuoc
               FROM LuongDuyet l {where} ORDER BY l.LoaiVanBan, l.MacDinh DESC, l.Ten",
            new { loaiVanBan })).ToList();
    }

    public async Task<LuongDuyet?> GetLuongDuyetChiTietAsync(int id)
    {
        using var db = Open();
        var l = await db.QueryFirstOrDefaultAsync<LuongDuyet>("SELECT * FROM LuongDuyet WHERE ID=@id", new { id });
        if (l == null) return null;
        l.DanhSachBuoc = (await db.QueryAsync<LuongDuyetBuoc>(
            "SELECT * FROM LuongDuyet_Buoc WHERE LuongID=@id ORDER BY ThuTu", new { id })).ToList();
        return l;
    }

    public async Task<int> LuuLuongDuyetAsync(LuongDuyet l, List<LuongDuyetBuoc> buoc)
    {
        using var db = Open();
        if (l.ID == 0)
            l.ID = await db.ExecuteScalarAsync<int>(
                @"INSERT INTO LuongDuyet (Ten, LoaiVanBan, MoTa, MacDinh, HienThi)
                  OUTPUT INSERTED.ID VALUES (@Ten,@LoaiVanBan,@MoTa,@MacDinh,@HienThi)", l);
        else
            await db.ExecuteAsync(
                "UPDATE LuongDuyet SET Ten=@Ten,LoaiVanBan=@LoaiVanBan,MoTa=@MoTa,MacDinh=@MacDinh,HienThi=@HienThi WHERE ID=@ID", l);

        if (l.MacDinh)
            await db.ExecuteAsync("UPDATE LuongDuyet SET MacDinh=0 WHERE LoaiVanBan=@LoaiVanBan AND ID<>@ID", l);

        // Thay toàn bộ danh sách bước (đơn giản, chỉ dùng lúc cấu hình — không ảnh hưởng chuỗi đang chạy)
        await db.ExecuteAsync("DELETE FROM LuongDuyet_Buoc WHERE LuongID=@id", new { id = l.ID });
        byte tt = 1;
        foreach (var b in buoc)
        {
            await db.ExecuteAsync(
                @"INSERT INTO LuongDuyet_Buoc (LuongID, ThuTu, TenBuoc, LoaiNguoiDuyet, GiaTri, ChoPhepTraLai, LaBuocKy)
                  VALUES (@LuongID,@ThuTu,@TenBuoc,@LoaiNguoiDuyet,@GiaTri,@ChoPhepTraLai,@LaBuocKy)",
                new { LuongID = l.ID, ThuTu = tt++, b.TenBuoc, b.LoaiNguoiDuyet, b.GiaTri, b.ChoPhepTraLai, b.LaBuocKy });
        }
        return l.ID;
    }

    public async Task XoaLuongDuyetAsync(int id)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM LuongDuyet_Buoc WHERE LuongID=@id; DELETE FROM LuongDuyet WHERE ID=@id;", new { id });
    }

    // Phân giải "ai duyệt" cho 1 bước, tại thời điểm khởi tạo chuỗi. Trả về MaNV đại diện (null nếu
    // không tìm được người nào). Controller vẫn cho phép BẤT KỲ người khớp điều kiện bước cùng xử lý.
    private async Task<short?> PhanGiaiNguoiDuyetAsync(System.Data.IDbConnection db, byte loai, string? giaTri, short maNVTrinh)
    {
        switch (loai)
        {
            case 1: return maNVTrinh;
            case 2:
                return await db.QueryFirstOrDefaultAsync<short?>(
                    @"SELECT dv.MaNV_TDV FROM NhanVien nv JOIN DonVi dv ON nv.MaDV=dv.MaDV
                      WHERE nv.MaNV=@maNVTrinh AND dv.MaNV_TDV IS NOT NULL", new { maNVTrinh });
            case 3:
                return await db.QueryFirstOrDefaultAsync<short?>(
                    @"SELECT TOP 1 x.MaNV FROM (
                        SELECT MaNV FROM NhanVien_ChucNang WHERE MaChucNang=@giaTri
                        UNION SELECT nv.MaNV FROM NhanVien_VaiTro nv JOIN VaiTro_Quyen vq ON nv.MaVaiTro=vq.MaVaiTro WHERE vq.MaChucNang=@giaTri
                        UNION SELECT MaNV FROM PhanQuyen WHERE MaQuyen=0
                      ) x JOIN NhanVien n ON x.MaNV=n.MaNV WHERE n.NgayNghiViec IS NULL ORDER BY x.MaNV", new { giaTri });
            case 4:
                return await db.QueryFirstOrDefaultAsync<short?>(
                    @"SELECT TOP 1 nv.MaNV FROM NhanVien_VaiTro nv JOIN NhanVien n ON nv.MaNV=n.MaNV
                      WHERE nv.MaVaiTro=@vt AND n.NgayNghiViec IS NULL ORDER BY nv.MaNV",
                    new { vt = short.TryParse(giaTri, out var v) ? v : 0 });
            case 5:
                return short.TryParse(giaTri, out var mnv) ? mnv : (short?)null;
            case 6:
                return await db.QueryFirstOrDefaultAsync<short?>(
                    @"SELECT TOP 1 p.MaNV FROM PhanQuyen p JOIN NhanVien n ON p.MaNV=n.MaNV
                      WHERE p.MaQuyen=12 AND n.NgayNghiViec IS NULL ORDER BY p.MaNV");
            default: return null;
        }
    }

    // Khởi tạo chuỗi trình ký từ 1 luồng mẫu. Trả về (id chuỗi, danh sách tên bước không phân giải được người).
    public async Task<(int TrinhKyId, List<string> BuocBoQua)> KhoiTaoTrinhKyTheoLuongAsync(
        byte loaiVanBan, string mscv, int luongID, short maNVTrinh, string? ghiChu)
    {
        using var db = Open();
        var buoc = (await db.QueryAsync<LuongDuyetBuoc>(
            "SELECT * FROM LuongDuyet_Buoc WHERE LuongID=@luongID ORDER BY ThuTu", new { luongID })).ToList();

        var trinhKyId = await db.ExecuteScalarAsync<int>(
            @"INSERT INTO TrinhKy (LoaiVanBan, MSCV, TrangThai, MaNVTrinh, NgayTrinh, GhiChu, LuongID)
              OUTPUT INSERTED.ID VALUES (@loaiVanBan, @mscv, 0, @maNVTrinh, GETDATE(), @ghiChu, @luongID)",
            new { loaiVanBan, mscv, maNVTrinh, ghiChu, luongID });

        var boQua = new List<string>();
        byte thuTu = 1;
        foreach (var b in buoc)
        {
            var maNV = await PhanGiaiNguoiDuyetAsync(db, b.LoaiNguoiDuyet, b.GiaTri, maNVTrinh);
            if (maNV is null or 0) { boQua.Add(b.TenBuoc); continue; }
            await db.ExecuteAsync(
                @"INSERT INTO TrinhKy_Cap
                    (TrinhKyID, ThuTu, MaNVDuyet, TrangThai, TenBuoc, ChoPhepTraLai, LaBuocKy, LoaiNguoiDuyet, GiaTriNguoiDuyet)
                  VALUES (@trinhKyId, @thuTu, @maNV, 0, @TenBuoc, @ChoPhepTraLai, @LaBuocKy, @LoaiNguoiDuyet, @GiaTri)",
                new { trinhKyId, thuTu, maNV, b.TenBuoc, b.ChoPhepTraLai, b.LaBuocKy, b.LoaiNguoiDuyet, b.GiaTri });
            thuTu++;
        }

        await GhiNhatKyTrinhKyAsync(db, trinhKyId, null, "trinh", maNVTrinh, ghiChu);
        return (trinhKyId, boQua);
    }

    private async Task GhiNhatKyTrinhKyAsync(System.Data.IDbConnection db, int trinhKyId, int? capId, string hanhDong, short maNV, string? noiDung, System.Data.IDbTransaction? tx = null)
        => await db.ExecuteAsync(
            "INSERT INTO TrinhKy_NhatKy (TrinhKyID, CapID, HanhDong, MaNV, NoiDung) VALUES (@trinhKyId,@capId,@hanhDong,@maNV,@noiDung)",
            new { trinhKyId, capId, hanhDong, maNV, noiDung }, tx);

    // Duyệt/Trả lại/Từ chối MỘT bước trình ký (dưới đây + HoanTatTrinhKyAsync) đều bọc transaction +
    // ĐIỀU KIỆN "AND TrangThai=0" ngay trên câu UPDATE, kiểm RowsAffected trước khi ghi nhật ký —
    // vá race condition 2026-09-11: trước đây đọc trạng thái ở tầng C# rồi mới UPDATE (không khóa),
    // nên 2 request cùng lúc (vd 1 người bấm "Duyệt" đúng lúc người khác trong cùng nhóm bấm "Từ
    // chối"/"Trả lại" trên CÙNG capId) có thể CẢ HAI đều pass điều kiện rồi cùng ghi đè — để lại 2
    // dòng nhật ký mâu thuẫn (vừa duyệt vừa từ chối) và trạng thái cuối cùng tùy ai ghi sau. Giờ chỉ
    // request nào "thắng" được UPDATE có điều kiện (SQL Server tự khóa dòng cho request đến sau) mới
    // được ghi nhật ký; request thua trả về false, controller báo "bước đã được xử lý, tải lại trang".
    public async Task<bool> DuyetCapTrinhKyAsync(int capId, short maNV, string? ghiChu)
    {
        using var db = Open();
        db.Open();
        using var tx = db.BeginTransaction();
        try
        {
            var cap = await db.QueryFirstOrDefaultAsync<TrinhKyCap>("SELECT * FROM TrinhKy_Cap WHERE ID=@capId", new { capId }, tx);
            if (cap == null) { tx.Rollback(); return false; }
            var affected = await db.ExecuteAsync(
                "UPDATE TrinhKy_Cap SET TrangThai=1, NgayXL=GETDATE(), GhiChu=@ghiChu WHERE ID=@capId AND TrangThai=0",
                new { capId, ghiChu }, tx);
            if (affected == 0) { tx.Rollback(); return false; }
            await GhiNhatKyTrinhKyAsync(db, cap.TrinhKyID, capId, "duyet", maNV, ghiChu, tx);
            tx.Commit();
            return true;
        }
        catch { tx.Rollback(); throw; }
    }

    // Trả lại: bước hiện tại (capId, đang chờ) được trả về bước LIỀN TRƯỚC để làm lại. Bước trước
    // (đang ở trạng thái đã duyệt) quay lại 0=chờ; các bước đã duyệt SAU bước trước cũng quay lại 0.
    public async Task<bool> TraLaiCapTrinhKyAsync(int capId, short maNV, string lyDo)
    {
        using var db = Open();
        db.Open();
        using var tx = db.BeginTransaction();
        try
        {
            var cap = await db.QueryFirstOrDefaultAsync<TrinhKyCap>("SELECT * FROM TrinhKy_Cap WHERE ID=@capId", new { capId }, tx);
            if (cap == null || cap.TrangThai != 0) { tx.Rollback(); return false; }
            var truoc = await db.QueryFirstOrDefaultAsync<TrinhKyCap>(
                "SELECT TOP 1 * FROM TrinhKy_Cap WHERE TrinhKyID=@t AND ThuTu<@tt ORDER BY ThuTu DESC",
                new { t = cap.TrinhKyID, tt = cap.ThuTu }, tx);
            if (truoc == null) { tx.Rollback(); return false; } // bước đầu tiên: không có "bước trước" để trả về

            // "Khóa" bước hiện tại bằng đúng 1 UPDATE có điều kiện TrangThai=0 trước khi động vào
            // các bước khác — nếu 1 request khác đã duyệt/từ chối capId này ngay trước đó thì
            // affected=0, hủy toàn bộ thao tác thay vì ghi đè lên quyết định đã có.
            var claimed = await db.ExecuteAsync(
                "UPDATE TrinhKy_Cap SET GhiChu=@lyDo WHERE ID=@capId AND TrangThai=0", new { capId, lyDo }, tx);
            if (claimed == 0) { tx.Rollback(); return false; }

            await db.ExecuteAsync(
                "UPDATE TrinhKy_Cap SET TrangThai=0, NgayXL=NULL WHERE TrinhKyID=@t AND ThuTu>=@ttTruoc AND ThuTu<@ttHienTai",
                new { t = cap.TrinhKyID, ttTruoc = truoc.ThuTu, ttHienTai = cap.ThuTu }, tx);
            await GhiNhatKyTrinhKyAsync(db, cap.TrinhKyID, capId, "tra_lai", maNV, lyDo, tx);
            tx.Commit();
            return true;
        }
        catch { tx.Rollback(); throw; }
    }

    public async Task<TrinhKy?> GetTrinhKyDangHoatDongAsync(byte loaiVanBan, string mscv)
    {
        using var db = Open();
        var id = await db.QueryFirstOrDefaultAsync<int?>(
            "SELECT TOP 1 ID FROM TrinhKy WHERE LoaiVanBan=@loaiVanBan AND MSCV=@mscv AND TrangThai=0 ORDER BY NgayTrinh DESC",
            new { loaiVanBan, mscv });
        return id.HasValue ? await LoadTrinhKyWithCapAsync(db, id.Value) : null;
    }

    public async Task<List<TrinhKy>> GetLichSuTrinhKyAsync(byte loaiVanBan, string mscv)
    {
        using var db = Open();
        var ids = (await db.QueryAsync<int>(
            "SELECT ID FROM TrinhKy WHERE LoaiVanBan=@loaiVanBan AND MSCV=@mscv ORDER BY NgayTrinh DESC",
            new { loaiVanBan, mscv })).ToList();
        var ds = new List<TrinhKy>();
        foreach (var id in ids)
        {
            var tk = await LoadTrinhKyWithCapAsync(db, id);
            if (tk != null) ds.Add(tk);
        }
        return ds;
    }

    public async Task<int> TaoTrinhKyAsync(byte loaiVanBan, string mscv, short maNVTrinh, List<short> nguoiDuyet, string? ghiChu)
    {
        using var db = Open();
        var trinhKyId = await db.ExecuteScalarAsync<int>(
            @"INSERT INTO TrinhKy (LoaiVanBan, MSCV, TrangThai, MaNVTrinh, NgayTrinh, GhiChu)
              OUTPUT INSERTED.ID VALUES (@loaiVanBan, @mscv, 0, @maNVTrinh, GETDATE(), @ghiChu)",
            new { loaiVanBan, mscv, maNVTrinh, ghiChu });
        byte thuTu = 1;
        foreach (var maNVDuyet in nguoiDuyet)
        {
            await db.ExecuteAsync(
                @"INSERT INTO TrinhKy_Cap (TrinhKyID, ThuTu, MaNVDuyet, TrangThai, TenBuoc, ChoPhepTraLai, LoaiNguoiDuyet, GiaTriNguoiDuyet)
                  VALUES (@trinhKyId, @thuTu, @maNVDuyet, 0, N'Cấp duyệt ' + CAST(@thuTu AS nvarchar(3)), 1, 5, CAST(@maNVDuyet AS nvarchar(10)))",
                new { trinhKyId, thuTu, maNVDuyet });
            thuTu++;
        }
        await GhiNhatKyTrinhKyAsync(db, trinhKyId, null, "trinh", maNVTrinh, ghiChu);
        return trinhKyId;
    }

    public async Task<TrinhKyCap?> GetCapTrinhKyByIdAsync(int capId)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<TrinhKyCap>("SELECT * FROM TrinhKy_Cap WHERE ID=@capId", new { capId });
    }

    public async Task<bool> TuChoiCapTrinhKyAsync(int capId, int trinhKyId, short maNV, string lyDo)
    {
        using var db = Open();
        db.Open();
        using var tx = db.BeginTransaction();
        try
        {
            // Guard TrangThai=0 giống DuyetCapTrinhKyAsync — trước đây UPDATE thẳng theo capId,
            // không kiểm trạng thái hiện tại, nên có thể "từ chối" đè lên 1 bước ĐÃ được duyệt/trả
            // lại bởi request khác chạy song song ngay trước đó.
            var affected = await db.ExecuteAsync(
                "UPDATE TrinhKy_Cap SET TrangThai=2, NgayXL=GETDATE(), GhiChu=@lyDo WHERE ID=@capId AND TrangThai=0",
                new { capId, lyDo }, tx);
            if (affected == 0) { tx.Rollback(); return false; }
            await db.ExecuteAsync("UPDATE TrinhKy SET TrangThai=2 WHERE ID=@trinhKyId", new { trinhKyId }, tx);
            await GhiNhatKyTrinhKyAsync(db, trinhKyId, capId, "tu_choi", maNV, lyDo, tx);
            tx.Commit();
            return true;
        }
        catch { tx.Rollback(); throw; }
    }

    // Gọi ngay sau khi cấp cuối tải file đã ký lên thành công — đóng cấp cuối + toàn bộ chuỗi.
    public async Task<bool> HoanTatTrinhKyAsync(int trinhKyId, int capIdCuoi, short maNV)
    {
        using var db = Open();
        db.Open();
        using var tx = db.BeginTransaction();
        try
        {
            // Guard TrangThai=0: nếu bước ký vừa bị trả lại/từ chối bởi 1 request khác đúng lúc file
            // đã ký đang được tải lên (2 tab, hoặc double-submit) thì KHÔNG được đóng chuỗi nữa.
            var affected = await db.ExecuteAsync(
                "UPDATE TrinhKy_Cap SET TrangThai=3, NgayXL=GETDATE() WHERE ID=@capIdCuoi AND TrangThai=0",
                new { capIdCuoi }, tx);
            if (affected == 0) { tx.Rollback(); return false; }
            await db.ExecuteAsync("UPDATE TrinhKy SET TrangThai=1 WHERE ID=@trinhKyId", new { trinhKyId }, tx);
            await GhiNhatKyTrinhKyAsync(db, trinhKyId, capIdCuoi, "hoan_tat", maNV, null, tx);
            tx.Commit();
            return true;
        }
        catch { tx.Rollback(); throw; }
    }

    // ── Nhập văn bản đi hàng loạt từ Excel (hoàn thiện hồ sơ cũ) ────────────
    public async Task<byte> FindOrCreateSoCVAsync(string? ten)
    {
        using var db = Open();
        if (!string.IsNullOrWhiteSpace(ten))
        {
            var found = await db.QueryFirstOrDefaultAsync<byte?>(
                "SELECT TOP 1 MaSCV FROM SoCV WHERE LTRIM(RTRIM(TenSCV))=LTRIM(RTRIM(@ten))", new { ten });
            if (found.HasValue) return found.Value;
        }
        var mac = await db.QueryFirstOrDefaultAsync<byte?>("SELECT TOP 1 MaSCV FROM SoCV ORDER BY MaSCV");
        if (mac.HasValue && string.IsNullOrWhiteSpace(ten)) return mac.Value;
        return await ThemSoCVAsync(!string.IsNullOrWhiteSpace(ten) ? ten.Trim() : "Sổ công văn", null);
    }

    public async Task<short> FindOrCreateLoaiVBAsync(string? ten)
    {
        using var db = Open();
        if (!string.IsNullOrWhiteSpace(ten))
        {
            var found = await db.QueryFirstOrDefaultAsync<short?>(
                "SELECT TOP 1 MaLVB FROM LoaiVB WHERE LTRIM(RTRIM(TenLVB))=LTRIM(RTRIM(@ten))", new { ten });
            if (found.HasValue) return found.Value;
            return await ThemLoaiVBAsync(ten.Trim(), null);
        }
        var mac = await db.QueryFirstOrDefaultAsync<short?>("SELECT TOP 1 MaLVB FROM LoaiVB WHERE HienThi=1 ORDER BY MaLVB");
        return mac ?? await ThemLoaiVBAsync("Công văn", null);
    }

    public async Task<short?> FindNhanVienTheoTenAsync(string? hoTen)
    {
        if (string.IsNullOrWhiteSpace(hoTen)) return null;
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<short?>(
            "SELECT TOP 1 MaNV FROM NhanVien WHERE LTRIM(RTRIM(HoNV+' '+TenNV))=LTRIM(RTRIM(@ten))", new { ten = hoTen });
    }

    // Văn bản đi gửi cho đơn vị nội bộ = văn bản đến của đơn vị đó (trừ văn bản đến thật sự
    // từ cơ quan ngoài trường, do văn thư nhập riêng). Tự tạo 1 bản ghi CongVanDen cho mỗi
    // đơn vị được chọn nhận, liên kết ngược qua MSCVDi, dùng chung toàn bộ hạ tầng công văn
    // đến sẵn có (XuLyDV, đã xem, trạng thái, email).
    public async Task<short> GetOrCreateVanThuCoQuanAsync()
    {
        using var db = Open();
        var maCQ = await db.QueryFirstOrDefaultAsync<short?>(
            "SELECT TOP 1 MaCQ FROM CoQuan WHERE TenCQ=N'Trường Đại học Kiên Giang'");
        if (maCQ.HasValue) return maCQ.Value;
        return await db.ExecuteScalarAsync<short>(
            "INSERT INTO CoQuan (TenCQ, HienThi) OUTPUT INSERTED.MaCQ SELECT N'Trường Đại học Kiên Giang', 1");
    }

    public async Task<string> TaoVanBanDenTuVanBanDiAsync(CongVanDi cv, byte maDVNhan, short maVT)
    {
        var maCQ = await GetOrCreateVanThuCoQuanAsync();
        var stt = await GetNextSTTAsync(cv.MaSCV, DateTime.Now.Year);

        var cvDen = new CongVanDen
        {
            NgayDen = cv.NgayCongVan,
            STT = stt,
            STT1 = cv.STT1,
            MaCQ = maCQ,
            NgayBanHanh = cv.NgayBanHanh,
            TrichYeu = cv.TrichYeu,
            NguoiKy = cv.TenLDKy,
            GhiChu = cv.GhiChu,
            MaLVB = cv.MaLVB,
            MaSCV = cv.MaSCV,
            MaDVXL = maDVNhan,
            NgayGiao = DateTime.Today,
            MSCVDi = cv.MSCV
        };
        var mscv = await ThemCongVanDenAsync(cvDen, maVT);
        await TaoXuLyDVAsync(mscv, maDVNhan, null);

        if (!string.IsNullOrEmpty(cv.FileDinhKem))
            await ThemFileDinhKemAsync(mscv, System.IO.Path.GetFileName(cv.FileDinhKem), cv.FileDinhKem);

        return mscv;
    }

    // Danh sách các bản ghi Văn bản đến đã được tự tạo (mirror) từ 1 văn bản đi, dùng khi sửa
    // văn bản đi để biết đơn vị nào đã có mirror rồi (cập nhật nội dung) và đơn vị nào mới thêm
    // (tạo mirror mới), không suy luận lại từ đầu để tránh phá lịch sử xử lý đã có.
    public async Task<List<(string MSCV, byte MaDVXL)>> GetVanBanDenMirrorsAsync(string mscvDi)
    {
        using var db = Open();
        var rows = await db.QueryAsync<(string MSCV, byte MaDVXL)>(
            "SELECT MSCV, MaDVXL FROM CongVanDen WHERE MSCVDi=@mscvDi AND MaDVXL IS NOT NULL", new { mscvDi });
        return rows.ToList();
    }

    // Đồng bộ nội dung khi văn bản đi gốc được sửa sang bản ghi Văn bản đến mirror đã tạo trước
    // đó — chỉ cập nhật các trường nội dung, KHÔNG đụng tới STT (số riêng của đơn vị nhận),
    // MaDVXL, trạng thái xử lý (XuLyDV) hay NgayHT, để không làm mất tiến độ xử lý đã ghi nhận.
    public async Task CapNhatVanBanDenTuVanBanDiAsync(string mscvDen, CongVanDi cv, bool coFileMoi)
    {
        using var db = Open();
        const string sql = @"
            UPDATE CongVanDen SET
                NgayDen=@NgayDen, STT1=@STT1, NgayBanHanh=@NgayBanHanh,
                TrichYeu=@TrichYeu, NguoiKy=NULLIF(@NguoiKy,''), GhiChu=@GhiChu,
                MaLVB=NULLIF(@MaLVB,0), MaSCV=@MaSCV
            WHERE MSCV=@mscvDen";
        await db.ExecuteAsync(sql, new
        {
            mscvDen, NgayDen = cv.NgayCongVan, cv.STT1, cv.NgayBanHanh, cv.TrichYeu,
            NguoiKy = cv.TenLDKy ?? "", cv.GhiChu, cv.MaLVB, cv.MaSCV
        });

        if (coFileMoi && !string.IsNullOrEmpty(cv.FileDinhKem))
            await ThemFileDinhKemAsync(mscvDen, System.IO.Path.GetFileName(cv.FileDinhKem), cv.FileDinhKem);
    }

    // ── Văn bản điều hành ───────────────────────────────────────────────────
    public async Task<List<VanBanDieuHanh>> GetVanBanDieuHanhAsync(int? nam = null, byte? maSCV = null, string? tuKhoa = null)
    {
        using var db = Open();
        var where = "WHERE 1=1";
        if (nam.HasValue) where += " AND YEAR(vbdh.NgayBanHanh)=@nam";
        if (maSCV.HasValue) where += " AND vbdh.MaSCV=@maSCV";
        if (!string.IsNullOrEmpty(tuKhoa)) where += " AND (vbdh.TrichYeu LIKE @tk OR vbdh.STT1 LIKE @tk)";

        var sql = $@"SELECT vbdh.*, lv.TenLVB, sc.TenSCV, nv.HoNV+' '+nv.TenNV AS TenLDKy
                     FROM VanBanDieuHanh vbdh
                     LEFT JOIN LoaiVB lv ON vbdh.MaLVB=lv.MaLVB
                     LEFT JOIN SoCV sc ON vbdh.MaSCV=sc.MaSCV
                     LEFT JOIN NhanVien nv ON vbdh.MaLDKy=nv.MaNV
                     {where} ORDER BY vbdh.NgayBanHanh DESC, vbdh.STT DESC";
        return (await db.QueryAsync<VanBanDieuHanh>(sql, new { nam, maSCV, tk = $"%{tuKhoa}%" })).ToList();
    }

    public async Task<VanBanDieuHanh?> GetVanBanDieuHanhByIdAsync(string mscv)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<VanBanDieuHanh>(
            @"SELECT vbdh.*, lv.TenLVB, sc.TenSCV, nv.HoNV+' '+nv.TenNV AS TenLDKy,
                     nvks.HoNV+' '+nvks.TenNV AS TenNVKySo
              FROM VanBanDieuHanh vbdh
              LEFT JOIN LoaiVB lv ON vbdh.MaLVB=lv.MaLVB
              LEFT JOIN SoCV sc ON vbdh.MaSCV=sc.MaSCV
              LEFT JOIN NhanVien nv ON vbdh.MaLDKy=nv.MaNV
              LEFT JOIN NhanVien nvks ON vbdh.MaNVKySo=nvks.MaNV
              WHERE vbdh.MSCV=@mscv", new { mscv });
    }

    public async Task<string> ThemVanBanDieuHanhAsync(VanBanDieuHanh vb, short maVT)
    {
        using var db = Open();
        const string sql = @"
            DECLARE @base nchar(6) = FORMAT(@NgayBanHanh, 'yyyyMM');
            DECLARE @seq  int = ISNULL(
                (SELECT MAX(CAST(RIGHT(RTRIM(MSCV),4) AS int)) FROM VanBanDieuHanh WHERE LEFT(RTRIM(MSCV),6) = @base),
                0) + 1;
            DECLARE @mscv nchar(10) = @base + RIGHT('0000' + CAST(@seq AS nvarchar(4)), 4);
            INSERT INTO VanBanDieuHanh
                (MSCV, STT, STT1, NgayBanHanh, MaSCV, MaLVB, TrichYeu, MaLDKy, PhamViApDung, GhiChu, FileDinhKem, MaVT, NgayNhap, MSCVDi)
            VALUES
                (@mscv, @STT, @STT1, @NgayBanHanh, @MaSCV, @MaLVB, @TrichYeu, NULLIF(@MaLDKy,0), @PhamViApDung, @GhiChu, @FileDinhKem, @MaVT, GETDATE(), NULLIF(@MSCVDi,''));
            SELECT @mscv;";
        return (await db.ExecuteScalarAsync<string>(sql, new
        {
            vb.STT, vb.STT1, vb.NgayBanHanh, vb.MaSCV, vb.MaLVB, vb.TrichYeu,
            MaLDKy = vb.MaLDKy ?? 0, vb.PhamViApDung, vb.GhiChu, vb.FileDinhKem, MaVT = maVT,
            MSCVDi = vb.MSCVDi ?? ""
        }))!;
    }

    // Tick "Cũng là văn bản điều hành" khi nhập văn bản đi -> tạo thêm 1 bản ghi bên Văn bản
    // điều hành, dùng CHUNG số ký hiệu (không tự sinh số mới) vì là cùng một văn bản.
    public async Task<string> ThemVanBanDieuHanhTuCongVanDiAsync(CongVanDi cv, short maVT)
    {
        var vb = new VanBanDieuHanh
        {
            STT = (int)cv.STT,
            STT1 = cv.STT1,
            NgayBanHanh = cv.NgayBanHanh,
            MaSCV = cv.MaSCV,
            MaLVB = cv.MaLVB,
            TrichYeu = cv.TrichYeu,
            MaLDKy = cv.MaLDKy,
            PhamViApDung = cv.NoiNhanCV,
            GhiChu = cv.GhiChu,
            FileDinhKem = cv.FileDinhKem,
            MSCVDi = cv.MSCV
        };
        return await ThemVanBanDieuHanhAsync(vb, maVT);
    }

    public async Task<VanBanDieuHanh?> GetVanBanDieuHanhByMscvDiAsync(string mscvDi)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<VanBanDieuHanh>(
            "SELECT * FROM VanBanDieuHanh WHERE MSCVDi=@mscvDi", new { mscvDi });
    }

    // Đồng bộ nội dung khi văn bản đi gốc được sửa sang bản ghi Văn bản điều hành đã tạo trước
    // đó qua tick "Cũng là văn bản điều hành" — dùng chung số ký hiệu (STT/STT1) nên không sinh
    // lại số mới ở đây.
    public async Task CapNhatVanBanDieuHanhTuCongVanDiAsync(string mscv, CongVanDi cv)
    {
        using var db = Open();
        const string sql = @"
            UPDATE VanBanDieuHanh SET
                STT1=@STT1, NgayBanHanh=@NgayBanHanh, MaSCV=@MaSCV, MaLVB=@MaLVB,
                TrichYeu=@TrichYeu, MaLDKy=NULLIF(@MaLDKy,0), PhamViApDung=@PhamViApDung, GhiChu=@GhiChu,
                FileDinhKem = CASE WHEN NULLIF(@FileDinhKem,'') IS NULL THEN FileDinhKem ELSE @FileDinhKem END
            WHERE MSCV=@mscv";
        await db.ExecuteAsync(sql, new
        {
            mscv, cv.STT1, cv.NgayBanHanh, cv.MaSCV, cv.MaLVB, cv.TrichYeu,
            MaLDKy = cv.MaLDKy, PhamViApDung = cv.NoiNhanCV, cv.GhiChu,
            FileDinhKem = cv.FileDinhKem ?? ""
        });
    }

    public async Task SuaVanBanDieuHanhAsync(VanBanDieuHanh vb)
    {
        using var db = Open();
        const string sql = @"
            UPDATE VanBanDieuHanh SET
                STT=@STT, STT1=@STT1, NgayBanHanh=@NgayBanHanh, MaSCV=@MaSCV, MaLVB=@MaLVB,
                TrichYeu=@TrichYeu, MaLDKy=NULLIF(@MaLDKy,0), PhamViApDung=@PhamViApDung, GhiChu=@GhiChu,
                FileDinhKem = CASE WHEN NULLIF(@FileDinhKem,'') IS NULL THEN FileDinhKem ELSE @FileDinhKem END
            WHERE MSCV=@MSCV";
        await db.ExecuteAsync(sql, new
        {
            vb.MSCV, vb.STT, vb.STT1, vb.NgayBanHanh, vb.MaSCV, vb.MaLVB, vb.TrichYeu,
            MaLDKy = vb.MaLDKy ?? 0, vb.PhamViApDung, vb.GhiChu, FileDinhKem = vb.FileDinhKem ?? ""
        });
    }

    public async Task XoaVanBanDieuHanhAsync(string mscv)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM VanBanDieuHanh WHERE MSCV=@mscv", new { mscv });
    }

    public async Task DanhDauDaKySoVanBanDieuHanhAsync(string mscv, string fileDaKy, string loaiChungThu, short maNV)
    {
        using var db = Open();
        await db.ExecuteAsync(
            @"UPDATE VanBanDieuHanh SET
                FileDinhKem=@fileDaKy, DaKySo=1, NgayKySo=GETDATE(), MaNVKySo=@maNV, LoaiChungThu=@loaiChungThu
              WHERE MSCV=@mscv",
            new { mscv, fileDaKy, loaiChungThu, maNV });
    }

    // Tính STT kế tiếp + build Số ký hiệu tự sinh cho văn bản điều hành — cùng cơ chế mẫu số
    // với văn bản đi (GetNextSoKyHieuDiAsync) nhưng đếm theo bảng VanBanDieuHanh riêng.
    public async Task<(int Stt, string? SoKyHieu)> GetNextSoKyHieuDieuHanhAsync(byte maSCV, short? maLVB)
    {
        using var db = Open();
        var nam = DateTime.Now.Year;

        var demRiengTheoLoai = await db.QueryFirstOrDefaultAsync<bool>(
            "SELECT DemRiengTheoLoai FROM SoCV WHERE MaSCV=@maSCV", new { maSCV });

        int stt;
        if (demRiengTheoLoai && maLVB.HasValue)
        {
            stt = await db.QueryFirstOrDefaultAsync<int?>(
                "SELECT MAX(STT) FROM VanBanDieuHanh WHERE MaSCV=@maSCV AND MaLVB=@maLVB AND YEAR(NgayBanHanh)=@nam",
                new { maSCV, maLVB, nam }) ?? 0;
        }
        else
        {
            stt = await db.QueryFirstOrDefaultAsync<int?>(
                "SELECT MAX(STT) FROM VanBanDieuHanh WHERE MaSCV=@maSCV AND YEAR(NgayBanHanh)=@nam",
                new { maSCV, nam }) ?? 0;
        }
        stt++;

        string? mauChuoi = null;
        if (maLVB.HasValue)
            mauChuoi = await db.QueryFirstOrDefaultAsync<string>(
                "SELECT MauChuoi FROM MauSoKyHieu WHERE MaSCV=@maSCV AND MaLVB=@maLVB", new { maSCV, maLVB });
        if (mauChuoi == null)
            mauChuoi = await db.QueryFirstOrDefaultAsync<string>(
                "SELECT MauChuoi FROM MauSoKyHieu WHERE MaSCV=@maSCV AND MaLVB IS NULL", new { maSCV });

        if (mauChuoi == null) return (stt, null);

        string? kyHieu = maLVB.HasValue
            ? await db.QueryFirstOrDefaultAsync<string>("SELECT KyHieu FROM LoaiVB WHERE MaLVB=@maLVB", new { maLVB })
            : null;

        var soKyHieu = mauChuoi
            .Replace("{STT3}", stt.ToString("000"))
            .Replace("{STT2}", stt.ToString("00"))
            .Replace("{STT}", stt.ToString())
            .Replace("{NAM2}", (nam % 100).ToString("00"))
            .Replace("{NAM}", nam.ToString())
            .Replace("{KyHieu}", kyHieu ?? "");

        return (stt, soKyHieu);
    }

    // ── Admin ───────────────────────────────────────────────────────────────
    public async Task<List<NhanVien>> GetAllNhanVienAsync(bool baoGomNghiViec = false)
    {
        using var db = Open();
        var where = baoGomNghiViec ? "" : "WHERE nv.NgayNghiViec IS NULL";
        return (await db.QueryAsync<NhanVien>(
            $"SELECT nv.*, dv.TenDV FROM NhanVien nv LEFT JOIN DonVi dv ON nv.MaDV=dv.MaDV {where} ORDER BY dv.STT,nv.HoNV,nv.TenNV")).ToList();
    }

    // Danh sách người có thể chọn làm "người duyệt" khi trình ký: lãnh đạo đơn vị của người trình
    // (DonVi.MaNV_TDV) + toàn bộ lãnh đạo trường (PhanQuyen.MaQuyen=12) — KHÔNG liệt kê toàn bộ
    // nhân viên. Bản thân người trình cũng được thêm vào để có thể tự ký nháy trước khi trình tiếp.
    public async Task<List<NhanVien>> GetNguoiDuyetTrinhKyAsync(byte maDV, short maNVTrinh)
    {
        using var db = Open();
        return (await db.QueryAsync<NhanVien>(
            "SELECT nv.*, dv.TenDV FROM NhanVien nv LEFT JOIN DonVi dv ON nv.MaDV=dv.MaDV " +
            "WHERE nv.NgayNghiViec IS NULL AND (" +
            "  nv.MaNV = @maNVTrinh" +
            "  OR nv.MaNV IN (SELECT MaNV_TDV FROM DonVi WHERE MaDV=@maDV AND MaNV_TDV IS NOT NULL)" +
            "  OR nv.MaNV IN (SELECT MaNV FROM PhanQuyen WHERE MaQuyen=12)" +
            ") ORDER BY dv.STT,nv.HoNV,nv.TenNV",
            new { maDV, maNVTrinh })).ToList();
    }

    public async Task NghiViecNhanVienAsync(short maNV, DateTime ngayNghiViec, string? lyDo)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "UPDATE NhanVien SET NgayNghiViec=@ngayNghiViec, LyDoNghiViec=@lyDo WHERE MaNV=@maNV",
            new { maNV, ngayNghiViec, lyDo });
    }

    public async Task KhoiPhucNhanVienAsync(short maNV)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "UPDATE NhanVien SET NgayNghiViec=NULL, LyDoNghiViec=NULL WHERE MaNV=@maNV",
            new { maNV });
    }

    public async Task<List<PhanQuyen>> GetPhanQuyenAsync(short? maNV = null)
    {
        using var db = Open();
        return (await db.QueryAsync<PhanQuyen>(
            "SELECT * FROM PhanQuyen" + (maNV.HasValue ? " WHERE MaNV=@maNV" : ""),
            new { maNV })).ToList();
    }

    public async Task<List<QuyenXL>> GetQuyenXLAsync()
    {
        using var db = Open();
        return (await db.QueryAsync<QuyenXL>("SELECT * FROM QuyenXL ORDER BY MaQuyen")).ToList();
    }

    public async Task SetPhanQuyenAsync(short maNV, byte maDV, byte maQuyen, bool add)
    {
        using var db = Open();
        if (add)
            await db.ExecuteAsync(
                "IF NOT EXISTS(SELECT 1 FROM PhanQuyen WHERE MaNV=@maNV AND MaDV=@maDV AND MaQuyen=@maQuyen) " +
                "INSERT INTO PhanQuyen VALUES(@maNV,@maDV,@maQuyen)", new { maNV, maDV, maQuyen });
        else
            await db.ExecuteAsync(
                "DELETE FROM PhanQuyen WHERE MaNV=@maNV AND MaDV=@maDV AND MaQuyen=@maQuyen", new { maNV, maDV, maQuyen });
    }

    public async Task<short> ThemNhanVienAsync(NhanVien nv, string matKhauRaw)
    {
        using var db = Open();
        // MaNV không phải IDENTITY nên tự tính
        return await db.ExecuteScalarAsync<short>(
            "DECLARE @id smallint = (SELECT ISNULL(MAX(MaNV),0)+1 FROM NhanVien); " +
            "INSERT INTO NhanVien (MaNV,HoNV,TenNV,Email,MatKhau,MaDV,Username) " +
            "VALUES (@id,@HoNV,@TenNV,@Email,PWDENCRYPT(@MatKhau),@MaDV,@Username); " +
            "SELECT @id;",
            new { nv.HoNV, nv.TenNV, nv.Email, MatKhau = matKhauRaw, nv.MaDV, nv.Username });
    }

    public async Task SuaNhanVienAsync(NhanVien nv)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "UPDATE NhanVien SET HoNV=@HoNV, TenNV=@TenNV, Email=@Email, MaDV=@MaDV, Username=@Username " +
            "WHERE MaNV=@MaNV",
            new { nv.MaNV, nv.HoNV, nv.TenNV, nv.Email, nv.MaDV, nv.Username });
    }

    public async Task DoiMatKhauAdminAsync(short maNV, string matKhauMoi)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "UPDATE NhanVien SET MatKhau=PWDENCRYPT(@mk) WHERE MaNV=@maNV",
            new { maNV, mk = matKhauMoi });
    }

    public async Task XoaNhanVienAsync(short maNV)
    {
        using var db = Open();
        // Xóa phan quyen trước để tránh FK
        await db.ExecuteAsync(
            "DELETE FROM PhanQuyen WHERE MaNV=@maNV; DELETE FROM NhanVien WHERE MaNV=@maNV;",
            new { maNV });
    }

    public async Task SetLanhDaoPhoangAsync(byte maDV, short? maNV_TDV)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "UPDATE DonVi SET MaNV_TDV=@maNV_TDV, MaNV_TheoDoiCV=@maNV_TDV WHERE MaDV=@maDV",
            new { maDV, maNV_TDV });
    }

    // ── Văn thư đơn vị (RBAC v3) — nhiều người/1 đơn vị, khác với Lãnh đạo phòng (1 người/đơn vị) ─
    public async Task<Dictionary<byte, List<short>>> GetVanThuDonViMapAsync()
    {
        using var db = Open();
        var rows = await db.QueryAsync<(byte MaDV, short MaNV)>("SELECT MaDV, MaNV FROM DonVi_VanThu");
        return rows.GroupBy(r => r.MaDV).ToDictionary(g => g.Key, g => g.Select(r => r.MaNV).ToList());
    }

    public async Task<List<short>> GetVanThuDonViAsync(byte maDV)
    {
        using var db = Open();
        return (await db.QueryAsync<short>("SELECT MaNV FROM DonVi_VanThu WHERE MaDV=@maDV", new { maDV })).ToList();
    }

    public async Task<bool> LaVanThuDonViAsync(short maNV, byte maDV)
    {
        using var db = Open();
        var c = await db.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(1) FROM DonVi_VanThu WHERE MaDV=@maDV AND MaNV=@maNV", new { maDV, maNV });
        return c > 0;
    }

    public async Task<bool> LaLanhDaoDonViAsync(short maNV, byte maDV)
    {
        using var db = Open();
        var c = await db.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(1) FROM DonVi WHERE MaDV=@maDV AND MaNV_TDV=@maNV", new { maDV, maNV });
        return c > 0;
    }

    // Cong tac chung: cac tinh nang moi (ngoai cong van den/di) da cong khai cho tat ca nguoi
    // dung hay chua — chi Admin (quyen 0) thay truoc de test khi con dang tat. Cache o session
    // luc dang nhap (xem AccountController.Login), khong doc DB moi request.
    public async Task<bool> GetTinhNangMoiCongKhaiAsync()
    {
        using var db = Open();
        var giaTri = await db.QueryFirstOrDefaultAsync<string>(
            "SELECT GiaTri FROM CauHinhHeThong WHERE Ten='TinhNangMoiCongKhai'");
        return giaTri == "1";
    }

    public async Task SetTinhNangMoiCongKhaiAsync(bool bat)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "UPDATE CauHinhHeThong SET GiaTri=@gt WHERE Ten='TinhNangMoiCongKhai'",
            new { gt = bat ? "1" : "0" });
    }

    public async Task SetVanThuDonViAsync(byte maDV, short maNV, bool add)
    {
        using var db = Open();
        if (add)
            await db.ExecuteAsync(
                "IF NOT EXISTS(SELECT 1 FROM DonVi_VanThu WHERE MaDV=@maDV AND MaNV=@maNV) " +
                "INSERT INTO DonVi_VanThu (MaDV, MaNV) VALUES (@maDV, @maNV)", new { maDV, maNV });
        else
            await db.ExecuteAsync("DELETE FROM DonVi_VanThu WHERE MaDV=@maDV AND MaNV=@maNV", new { maDV, maNV });
    }

    // Tìm nhanh văn bản gốc để gắn vào công việc — gõ số văn bản hoặc 1 phần trích yếu.
    // loai: 1=Văn bản đến, 2=Văn bản đi, 3=Văn bản điều hành.
    public async Task<List<(string MSCV, string SoVanBan, string TrichYeu, DateTime Ngay)>> TimVanBanGocAsync(byte loai, string q, int top = 20)
    {
        using var db = Open();
        var tk = $"%{q}%";
        string sql = loai switch
        {
            1 => @"SELECT TOP (@top) RTRIM(MSCV) AS MSCV, ISNULL(NULLIF(RTRIM(SoCV),''), CAST(STT AS varchar(10))) AS SoVanBan,
                          TrichYeu, NgayDen AS Ngay
                   FROM CongVanDen WHERE TrichYeu LIKE @tk OR STT1 LIKE @tk OR SoCV LIKE @tk ORDER BY NgayDen DESC",
            2 => @"SELECT TOP (@top) RTRIM(MSCV) AS MSCV, ISNULL(NULLIF(RTRIM(STT1),''), CAST(STT AS varchar(10))) AS SoVanBan,
                          TrichYeu, NgayCongVan AS Ngay
                   FROM CongVanDi WHERE TrichYeu LIKE @tk OR STT1 LIKE @tk ORDER BY NgayCongVan DESC",
            3 => @"SELECT TOP (@top) RTRIM(MSCV) AS MSCV, ISNULL(NULLIF(RTRIM(STT1),''), CAST(STT AS varchar(10))) AS SoVanBan,
                          TrichYeu, NgayBanHanh AS Ngay
                   FROM VanBanDieuHanh WHERE TrichYeu LIKE @tk OR STT1 LIKE @tk ORDER BY NgayBanHanh DESC",
            _ => ""
        };
        if (sql == "") return new();
        return (await db.QueryAsync<(string, string, string, DateTime)>(sql, new { tk, top })).ToList();
    }

    public async Task<NhanVien?> GetNhanVienByIdAsync(short maNV)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<NhanVien>(
            "SELECT nv.*, dv.TenDV FROM NhanVien nv LEFT JOIN DonVi dv ON nv.MaDV=dv.MaDV WHERE nv.MaNV=@maNV", new { maNV });
    }

    // ── Email phòng ban ─────────────────────────────────────────────────────
    public async Task SetEmailDonViAsync(byte maDV, string? email)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE DonVi SET Email=@email WHERE MaDV=@maDV", new { maDV, email });
    }

    // ── Email Config ────────────────────────────────────────────────────────
    public async Task<EmailConfig?> GetEmailConfigAsync()
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<EmailConfig>("SELECT TOP 1 * FROM EmailConfig ORDER BY Id");
    }

    public async Task SaveEmailConfigAsync(EmailConfig cfg)
    {
        cfg.SmtpHost     ??= "";
        cfg.SmtpUser     ??= "";
        cfg.SmtpPassword ??= "";
        cfg.SenderName   ??= "";
        cfg.SenderEmail  ??= "";
        using var db = Open();
        await db.ExecuteAsync(
            @"IF EXISTS(SELECT 1 FROM EmailConfig WHERE Id=@Id)
                UPDATE EmailConfig SET SmtpHost=@SmtpHost, SmtpPort=@SmtpPort, SmtpUser=@SmtpUser,
                    SmtpPassword=CASE WHEN NULLIF(@SmtpPassword,'') IS NULL THEN SmtpPassword ELSE @SmtpPassword END,
                    SenderName=@SenderName, SenderEmail=@SenderEmail, UseSSL=@UseSSL, IsActive=@IsActive,
                    UpdatedAt=GETDATE() WHERE Id=@Id
              ELSE
                INSERT INTO EmailConfig (SmtpHost,SmtpPort,SmtpUser,SmtpPassword,SenderName,SenderEmail,UseSSL,IsActive)
                VALUES (@SmtpHost,@SmtpPort,@SmtpUser,ISNULL(NULLIF(@SmtpPassword,''),''),@SenderName,@SenderEmail,@UseSSL,@IsActive)",
            cfg);
    }

    public async Task<(string? TenDV, string? Email)> GetDonViEmailAsync(byte maDV)
    {
        using var db = Open();
        var row = await db.QueryFirstOrDefaultAsync(
            "SELECT TenDV, Email FROM DonVi WHERE MaDV=@maDV", new { maDV });
        return (row?.TenDV, row?.Email);
    }

    // ── Văn phòng điện tử: Quản lý công việc ────────────────────────────────
    public async Task<List<CongViec>> GetCongViecAsync(byte? maDV, byte? trangThai, string? tuKhoa, short? chiCuaMaNV)
    {
        using var db = Open();
        var where = "WHERE 1=1";
        if (maDV.HasValue) where += " AND cv.MaDV=@maDV";
        if (trangThai.HasValue) where += " AND cv.TrangThai=@trangThai";
        if (!string.IsNullOrEmpty(tuKhoa)) where += " AND cv.TieuDe LIKE @tk";
        if (chiCuaMaNV.HasValue)
            where += " AND (cv.MaNVChuTri=@chiCuaMaNV OR cv.MaNVGiao=@chiCuaMaNV OR cv.MaNVTao=@chiCuaMaNV " +
                     "OR ',' + ISNULL(cv.NguoiPhoiHop,'') + ',' LIKE '%,' + CAST(@chiCuaMaNV AS nvarchar) + ',%')";

        var sql = $@"SELECT cv.*, (g.HoNV+' '+g.TenNV) AS TenNVGiao, (c.HoNV+' '+c.TenNV) AS TenNVChuTri, dv.TenDV
                     FROM CongViec cv
                     JOIN NhanVien g ON cv.MaNVGiao=g.MaNV
                     JOIN NhanVien c ON cv.MaNVChuTri=c.MaNV
                     JOIN DonVi dv ON cv.MaDV=dv.MaDV
                     {where} ORDER BY cv.NgayGiao DESC, cv.MaCV DESC";
        return (await db.QueryAsync<CongViec>(sql, new { maDV, trangThai, tuKhoa = $"%{tuKhoa}%", chiCuaMaNV })).ToList();
    }

    public async Task<CongViec?> GetCongViecByIdAsync(int maCV)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<CongViec>(
            @"SELECT cv.*, (g.HoNV+' '+g.TenNV) AS TenNVGiao, (c.HoNV+' '+c.TenNV) AS TenNVChuTri, dv.TenDV
              FROM CongViec cv
              JOIN NhanVien g ON cv.MaNVGiao=g.MaNV
              JOIN NhanVien c ON cv.MaNVChuTri=c.MaNV
              JOIN DonVi dv ON cv.MaDV=dv.MaDV
              WHERE cv.MaCV=@maCV", new { maCV });
    }

    public async Task<int> ThemCongViecAsync(CongViec cv, short maNVTao)
    {
        using var db = Open();
        const string sql = @"
            INSERT INTO CongViec
                (TieuDe, MoTa, MaNVGiao, MaNVChuTri, MaDV, NguoiPhoiHop, NgayGiao, HanXuLy,
                 MucDoUuTien, TrangThai, LoaiNguonGoc, MSCVGoc, GhiChu, MaNVTao, NgayTao)
            OUTPUT INSERTED.MaCV
            VALUES
                (@TieuDe, @MoTa, @MaNVGiao, @MaNVChuTri, @MaDV, @NguoiPhoiHop, @NgayGiao, @HanXuLy,
                 @MucDoUuTien, 0, @LoaiNguonGoc, NULLIF(@MSCVGoc,''), @GhiChu, @MaNVTao, GETDATE());";
        return await db.ExecuteScalarAsync<int>(sql, new
        {
            cv.TieuDe, cv.MoTa, cv.MaNVGiao, cv.MaNVChuTri, cv.MaDV, cv.NguoiPhoiHop, cv.NgayGiao, cv.HanXuLy,
            cv.MucDoUuTien, cv.LoaiNguonGoc, MSCVGoc = cv.MSCVGoc ?? "", cv.GhiChu, MaNVTao = maNVTao
        });
    }

    public async Task SuaCongViecAsync(CongViec cv)
    {
        using var db = Open();
        const string sql = @"
            UPDATE CongViec SET
                TieuDe=@TieuDe, MoTa=@MoTa, MaNVChuTri=@MaNVChuTri, MaDV=@MaDV, NguoiPhoiHop=@NguoiPhoiHop,
                HanXuLy=@HanXuLy, MucDoUuTien=@MucDoUuTien, LoaiNguonGoc=@LoaiNguonGoc, MSCVGoc=NULLIF(@MSCVGoc,''), GhiChu=@GhiChu
            WHERE MaCV=@MaCV";
        await db.ExecuteAsync(sql, new
        {
            cv.MaCV, cv.TieuDe, cv.MoTa, cv.MaNVChuTri, cv.MaDV, cv.NguoiPhoiHop,
            cv.HanXuLy, cv.MucDoUuTien, cv.LoaiNguonGoc, MSCVGoc = cv.MSCVGoc ?? "", cv.GhiChu
        });
    }

    public async Task XoaCongViecAsync(int maCV)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM CongViec_NhatKy WHERE MaCV=@maCV", new { maCV });
        await db.ExecuteAsync("DELETE FROM CongViec_File WHERE MaCV=@maCV", new { maCV });
        await db.ExecuteAsync("DELETE FROM CongViec WHERE MaCV=@maCV", new { maCV });
    }

    public async Task CapNhatTrangThaiCongViecAsync(int maCV, byte trangThai)
    {
        using var db = Open();
        var sql = trangThai == 2
            ? "UPDATE CongViec SET TrangThai=@trangThai, NgayHoanThanh=ISNULL(NgayHoanThanh,GETDATE()) WHERE MaCV=@maCV"
            : "UPDATE CongViec SET TrangThai=@trangThai WHERE MaCV=@maCV";
        await db.ExecuteAsync(sql, new { maCV, trangThai });
    }

    public async Task ThemNhatKyCongViecAsync(int maCV, short maNV, string noiDung, byte? trangThaiMoi, string? fileDinhKem)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "INSERT INTO CongViec_NhatKy (MaCV, MaNV, NgayGhi, NoiDung, TrangThaiMoi, FileDinhKem) VALUES (@maCV, @maNV, GETDATE(), @noiDung, @trangThaiMoi, @fileDinhKem)",
            new { maCV, maNV, noiDung, trangThaiMoi, fileDinhKem });
    }

    public async Task<List<CongViecNhatKy>> GetNhatKyCongViecAsync(int maCV)
    {
        using var db = Open();
        var sql = @"SELECT nk.*, (nv.HoNV+' '+nv.TenNV) AS TenNV
                     FROM CongViec_NhatKy nk JOIN NhanVien nv ON nk.MaNV=nv.MaNV
                     WHERE nk.MaCV=@maCV ORDER BY nk.NgayGhi DESC";
        return (await db.QueryAsync<CongViecNhatKy>(sql, new { maCV })).ToList();
    }

    // Trao đổi/bình luận theo công việc — thảo luận tự do, khác Nhật ký xử lý (không gắn trạng
    // thái, luôn mở kể cả khi công việc đã hoàn thành/huỷ).
    public async Task<List<CongViecBinhLuan>> GetBinhLuanCongViecAsync(int maCV)
    {
        using var db = Open();
        var sql = @"SELECT bl.*, (nv.HoNV+' '+nv.TenNV) AS TenNV
                     FROM CongViec_BinhLuan bl JOIN NhanVien nv ON bl.MaNV=nv.MaNV
                     WHERE bl.MaCV=@maCV ORDER BY bl.NgayBinhLuan ASC";
        return (await db.QueryAsync<CongViecBinhLuan>(sql, new { maCV })).ToList();
    }

    public async Task<int> ThemBinhLuanCongViecAsync(int maCV, short maNV, string noiDung)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<int>(
            "INSERT INTO CongViec_BinhLuan (MaCV, MaNV, NoiDung, NgayBinhLuan) OUTPUT INSERTED.ID VALUES (@maCV, @maNV, @noiDung, GETDATE())",
            new { maCV, maNV, noiDung });
    }

    public async Task<CongViecBinhLuan?> GetBinhLuanByIdAsync(int id)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<CongViecBinhLuan>("SELECT * FROM CongViec_BinhLuan WHERE ID=@id", new { id });
    }

    public async Task XoaBinhLuanCongViecAsync(int id)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM CongViec_BinhLuan WHERE ID=@id", new { id });
    }

    public async Task ThemFileCongViecAsync(int maCV, string tenFile, string duongDan, short maNVUpload)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "INSERT INTO CongViec_File (MaCV, TenFile, DuongDan, NgayUpload, MaNVUpload) VALUES (@maCV, @tenFile, @duongDan, GETDATE(), @maNVUpload)",
            new { maCV, tenFile, duongDan, maNVUpload });
    }

    public async Task<List<CongViecFile>> GetFileCongViecAsync(int maCV)
    {
        using var db = Open();
        var sql = @"SELECT f.*, (nv.HoNV+' '+nv.TenNV) AS TenNVUpload
                     FROM CongViec_File f JOIN NhanVien nv ON f.MaNVUpload=nv.MaNV
                     WHERE f.MaCV=@maCV ORDER BY f.NgayUpload DESC";
        return (await db.QueryAsync<CongViecFile>(sql, new { maCV })).ToList();
    }

    // Trả về (tiêu đề, MSCV) của văn bản gốc để hiển thị trên ChiTiet Công việc, dispatch theo LoaiNguonGoc
    public async Task<string?> GetTieuDeVanBanGocAsync(byte? loaiNguonGoc, string? mscvGoc)
    {
        if (!loaiNguonGoc.HasValue || string.IsNullOrEmpty(mscvGoc)) return null;
        using var db = Open();
        return loaiNguonGoc switch
        {
            1 => await db.QueryFirstOrDefaultAsync<string>("SELECT TrichYeu FROM CongVanDen WHERE MSCV=@mscvGoc", new { mscvGoc }),
            2 => await db.QueryFirstOrDefaultAsync<string>("SELECT TrichYeu FROM CongVanDi WHERE MSCV=@mscvGoc", new { mscvGoc }),
            3 => await db.QueryFirstOrDefaultAsync<string>("SELECT TrichYeu FROM VanBanDieuHanh WHERE MSCV=@mscvGoc", new { mscvGoc }),
            _ => null
        };
    }

    // ── Văn phòng điện tử: Quản lý hồ sơ công việc (case file) ─────────────
    public async Task<List<HoSoCongViec>> GetHoSoCongViecAsync(byte? maDV, byte? trangThai, string? tuKhoa)
    {
        using var db = Open();
        var where = "WHERE 1=1";
        if (maDV.HasValue) where += " AND hs.MaDVPhuTrach=@maDV";
        if (trangThai.HasValue) where += " AND hs.TrangThai=@trangThai";
        if (!string.IsNullOrEmpty(tuKhoa)) where += " AND hs.TieuDe LIKE @tk";

        var sql = $@"SELECT hs.*, dv.TenDV AS TenDVPhuTrach, (nv.HoNV+' '+nv.TenNV) AS TenNVPhuTrach
                     FROM HoSoCongViec hs
                     JOIN DonVi dv ON hs.MaDVPhuTrach=dv.MaDV
                     JOIN NhanVien nv ON hs.MaNVPhuTrach=nv.MaNV
                     {where} ORDER BY hs.NgayMo DESC, hs.MaHoSo DESC";
        return (await db.QueryAsync<HoSoCongViec>(sql, new { maDV, trangThai, tuKhoa = $"%{tuKhoa}%" })).ToList();
    }

    public async Task<HoSoCongViec?> GetHoSoCongViecByIdAsync(int maHoSo)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<HoSoCongViec>(
            @"SELECT hs.*, dv.TenDV AS TenDVPhuTrach, (nv.HoNV+' '+nv.TenNV) AS TenNVPhuTrach
              FROM HoSoCongViec hs
              JOIN DonVi dv ON hs.MaDVPhuTrach=dv.MaDV
              JOIN NhanVien nv ON hs.MaNVPhuTrach=nv.MaNV
              WHERE hs.MaHoSo=@maHoSo", new { maHoSo });
    }

    public async Task<int> ThemHoSoCongViecAsync(HoSoCongViec hs, short maNVTao)
    {
        using var db = Open();
        const string sql = @"
            INSERT INTO HoSoCongViec (TieuDe, MoTa, MaDVPhuTrach, MaNVPhuTrach, TrangThai, NgayMo, GhiChu, MaNVTao, NgayTao)
            OUTPUT INSERTED.MaHoSo
            VALUES (@TieuDe, @MoTa, @MaDVPhuTrach, @MaNVPhuTrach, 0, @NgayMo, @GhiChu, @MaNVTao, GETDATE());";
        return await db.ExecuteScalarAsync<int>(sql, new
        {
            hs.TieuDe, hs.MoTa, hs.MaDVPhuTrach, hs.MaNVPhuTrach, hs.NgayMo, hs.GhiChu, MaNVTao = maNVTao
        });
    }

    public async Task SuaHoSoCongViecAsync(HoSoCongViec hs)
    {
        using var db = Open();
        const string sql = @"
            UPDATE HoSoCongViec SET
                TieuDe=@TieuDe, MoTa=@MoTa, MaDVPhuTrach=@MaDVPhuTrach, MaNVPhuTrach=@MaNVPhuTrach, GhiChu=@GhiChu
            WHERE MaHoSo=@MaHoSo";
        await db.ExecuteAsync(sql, new { hs.MaHoSo, hs.TieuDe, hs.MoTa, hs.MaDVPhuTrach, hs.MaNVPhuTrach, hs.GhiChu });
    }

    public async Task CapNhatTrangThaiHoSoAsync(int maHoSo, byte trangThai)
    {
        using var db = Open();
        var sql = trangThai == 2
            ? "UPDATE HoSoCongViec SET TrangThai=@trangThai, NgayDong=ISNULL(NgayDong,GETDATE()) WHERE MaHoSo=@maHoSo"
            : "UPDATE HoSoCongViec SET TrangThai=@trangThai, NgayDong=NULL WHERE MaHoSo=@maHoSo";
        await db.ExecuteAsync(sql, new { maHoSo, trangThai });
    }

    public async Task XoaHoSoCongViecAsync(int maHoSo)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM HoSoCongViec_CongViec WHERE MaHoSo=@maHoSo", new { maHoSo });
        await db.ExecuteAsync("DELETE FROM HoSoCongViec_VanBan WHERE MaHoSo=@maHoSo", new { maHoSo });
        await db.ExecuteAsync("DELETE FROM HoSoCongViec_File WHERE MaHoSo=@maHoSo", new { maHoSo });
        await db.ExecuteAsync("DELETE FROM HoSoCongViec WHERE MaHoSo=@maHoSo", new { maHoSo });
    }

    public async Task GanCongViecVaoHoSoAsync(int maHoSo, int maCV)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "IF NOT EXISTS(SELECT 1 FROM HoSoCongViec_CongViec WHERE MaHoSo=@maHoSo AND MaCV=@maCV) " +
            "INSERT INTO HoSoCongViec_CongViec (MaHoSo, MaCV) VALUES (@maHoSo, @maCV)",
            new { maHoSo, maCV });
    }

    public async Task GoCongViecKhoiHoSoAsync(int maHoSo, int maCV)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM HoSoCongViec_CongViec WHERE MaHoSo=@maHoSo AND MaCV=@maCV", new { maHoSo, maCV });
    }

    public async Task<List<CongViec>> GetCongViecLienQuanHoSoAsync(int maHoSo)
    {
        using var db = Open();
        var sql = @"SELECT cv.*, (g.HoNV+' '+g.TenNV) AS TenNVGiao, (c.HoNV+' '+c.TenNV) AS TenNVChuTri, dv.TenDV
                     FROM HoSoCongViec_CongViec link
                     JOIN CongViec cv ON link.MaCV=cv.MaCV
                     JOIN NhanVien g ON cv.MaNVGiao=g.MaNV
                     JOIN NhanVien c ON cv.MaNVChuTri=c.MaNV
                     JOIN DonVi dv ON cv.MaDV=dv.MaDV
                     WHERE link.MaHoSo=@maHoSo ORDER BY cv.NgayGiao DESC";
        return (await db.QueryAsync<CongViec>(sql, new { maHoSo })).ToList();
    }

    public async Task GanVanBanVaoHoSoAsync(int maHoSo, byte loaiVanBan, string mscv, short maNVGan)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "IF NOT EXISTS(SELECT 1 FROM HoSoCongViec_VanBan WHERE MaHoSo=@maHoSo AND LoaiVanBan=@loaiVanBan AND MSCV=@mscv) " +
            "INSERT INTO HoSoCongViec_VanBan (MaHoSo, LoaiVanBan, MSCV, MaNVGan) VALUES (@maHoSo, @loaiVanBan, @mscv, @maNVGan)",
            new { maHoSo, loaiVanBan, mscv, maNVGan });
    }

    public async Task GoVanBanKhoiHoSoAsync(int id)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM HoSoCongViec_VanBan WHERE ID=@id", new { id });
    }

    public async Task<List<HoSoCongViecVanBan>> GetVanBanLienQuanHoSoAsync(int maHoSo)
    {
        using var db = Open();
        var rows = (await db.QueryAsync<HoSoCongViecVanBan>(
            "SELECT * FROM HoSoCongViec_VanBan WHERE MaHoSo=@maHoSo ORDER BY NgayGan DESC", new { maHoSo })).ToList();

        foreach (var r in rows)
        {
            var mscv = r.MSCV.Trim();
            (string? tieuDe, DateTime? ngay) info = r.LoaiVanBan switch
            {
                1 => await db.QueryFirstOrDefaultAsync<(string?, DateTime?)>(
                        "SELECT TrichYeu, NgayDen FROM CongVanDen WHERE MSCV=@mscv", new { mscv }),
                2 => await db.QueryFirstOrDefaultAsync<(string?, DateTime?)>(
                        "SELECT TrichYeu, NgayCongVan FROM CongVanDi WHERE MSCV=@mscv", new { mscv }),
                3 => await db.QueryFirstOrDefaultAsync<(string?, DateTime?)>(
                        "SELECT TrichYeu, NgayBanHanh FROM VanBanDieuHanh WHERE MSCV=@mscv", new { mscv }),
                _ => (null, null)
            };
            r.TieuDe = info.tieuDe;
            r.NgayVanBan = info.ngay;
        }
        return rows;
    }

    public async Task ThemFileHoSoCongViecAsync(int maHoSo, string tenFile, string duongDan, short maNVUpload)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "INSERT INTO HoSoCongViec_File (MaHoSo, TenFile, DuongDan, NgayUpload, MaNVUpload) VALUES (@maHoSo, @tenFile, @duongDan, GETDATE(), @maNVUpload)",
            new { maHoSo, tenFile, duongDan, maNVUpload });
    }

    public async Task<List<HoSoCongViecFile>> GetFileHoSoCongViecAsync(int maHoSo)
    {
        using var db = Open();
        var sql = @"SELECT f.*, (nv.HoNV+' '+nv.TenNV) AS TenNVUpload
                     FROM HoSoCongViec_File f JOIN NhanVien nv ON f.MaNVUpload=nv.MaNV
                     WHERE f.MaHoSo=@maHoSo ORDER BY f.NgayUpload DESC";
        return (await db.QueryAsync<HoSoCongViecFile>(sql, new { maHoSo })).ToList();
    }

    // ── Văn phòng điện tử: Lịch công tác ────────────────────────────────────
    public async Task<List<LichCongTac>> GetLichCongTacTheoKhoangNgayAsync(DateTime tuNgay, DateTime denNgay, byte? maDV)
    {
        using var db = Open();
        var where = "WHERE lc.ThoiGianBatDau >= @tuNgay AND lc.ThoiGianBatDau < @denNgayExclusive";
        if (maDV.HasValue) where += " AND (lc.MaDV IS NULL OR lc.MaDV=@maDV)";

        var sql = $@"SELECT lc.*, dv.TenDV, (nv.HoNV+' '+nv.TenNV) AS TenNVTao, ph.TenPhong
                     FROM LichCongTac lc
                     LEFT JOIN DonVi dv ON lc.MaDV=dv.MaDV
                     LEFT JOIN PhongHop ph ON lc.MaPhong=ph.MaPhong
                     JOIN NhanVien nv ON lc.MaNVTao=nv.MaNV
                     {where} ORDER BY lc.ThoiGianBatDau";
        var ds = (await db.QueryAsync<LichCongTac>(sql, new { tuNgay, denNgayExclusive = denNgay.Date.AddDays(1), maDV })).ToList();

        // UNION đọc-only: hạn xử lý của Công việc trong cùng khoảng ngày, không lưu trùng dữ liệu
        var whereCv = "WHERE cv.HanXuLy >= @tuNgay AND cv.HanXuLy < @denNgayExclusive AND cv.TrangThai < 2";
        if (maDV.HasValue) whereCv += " AND cv.MaDV=@maDV";
        var sqlCv = $@"SELECT cv.MaCV, cv.TieuDe, cv.HanXuLy FROM CongViec cv {whereCv} ORDER BY cv.HanXuLy";
        var congViecs = await db.QueryAsync(sqlCv, new { tuNgay, denNgayExclusive = denNgay.Date.AddDays(1), maDV });
        foreach (var cv in congViecs)
        {
            ds.Add(new LichCongTac
            {
                MaLich = 0,
                TieuDe = $"[Hạn xử lý] {cv.TieuDe}",
                ThoiGianBatDau = cv.HanXuLy,
                LoaiSuKien = 4,
                TuCongViec = true,
                MaCVLienKet = cv.MaCV
            });
        }

        return ds.OrderBy(l => l.ThoiGianBatDau).ToList();
    }

    public async Task<LichCongTac?> GetLichCongTacByIdAsync(int maLich)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<LichCongTac>(
            @"SELECT lc.*, dv.TenDV, (nv.HoNV+' '+nv.TenNV) AS TenNVTao, ph.TenPhong
              FROM LichCongTac lc
              LEFT JOIN DonVi dv ON lc.MaDV=dv.MaDV
              LEFT JOIN PhongHop ph ON lc.MaPhong=ph.MaPhong
              JOIN NhanVien nv ON lc.MaNVTao=nv.MaNV
              WHERE lc.MaLich=@maLich", new { maLich });
    }

    public async Task<int> ThemLichCongTacAsync(LichCongTac lc, short maNVTao)
    {
        using var db = Open();
        const string sql = @"
            INSERT INTO LichCongTac (TieuDe, NoiDung, ThoiGianBatDau, ThoiGianKetThuc, DiaDiem, MaDV, NguoiThamGia, LoaiSuKien, MaPhong, MaNVTao, NgayTao)
            OUTPUT INSERTED.MaLich
            VALUES (@TieuDe, @NoiDung, @ThoiGianBatDau, @ThoiGianKetThuc, @DiaDiem, @MaDV, @NguoiThamGia, @LoaiSuKien, @MaPhong, @MaNVTao, GETDATE());";
        return await db.ExecuteScalarAsync<int>(sql, new
        {
            lc.TieuDe, lc.NoiDung, lc.ThoiGianBatDau, lc.ThoiGianKetThuc, lc.DiaDiem, lc.MaDV, lc.NguoiThamGia, lc.LoaiSuKien, lc.MaPhong, MaNVTao = maNVTao
        });
    }

    public async Task SuaLichCongTacAsync(LichCongTac lc)
    {
        using var db = Open();
        const string sql = @"
            UPDATE LichCongTac SET
                TieuDe=@TieuDe, NoiDung=@NoiDung, ThoiGianBatDau=@ThoiGianBatDau, ThoiGianKetThuc=@ThoiGianKetThuc,
                DiaDiem=@DiaDiem, MaDV=@MaDV, NguoiThamGia=@NguoiThamGia, LoaiSuKien=@LoaiSuKien, MaPhong=@MaPhong
            WHERE MaLich=@MaLich";
        await db.ExecuteAsync(sql, new
        {
            lc.MaLich, lc.TieuDe, lc.NoiDung, lc.ThoiGianBatDau, lc.ThoiGianKetThuc, lc.DiaDiem, lc.MaDV, lc.NguoiThamGia, lc.LoaiSuKien, lc.MaPhong
        });
    }

    public async Task XoaLichCongTacAsync(int maLich)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM LichCongTac WHERE MaLich=@maLich", new { maLich });
    }

    // ── Văn phòng điện tử: Lịch làm việc (riêng của lãnh đạo) ────────────────
    public async Task<List<LichLamViec>> GetLichLamViecTheoKhoangNgayAsync(DateTime tuNgay, DateTime denNgay)
    {
        using var db = Open();
        var sql = @"SELECT ll.*, (nv.HoNV+' '+nv.TenNV) AS TenNVLanhDao, dv.TenDV, ph.TenPhong
                     FROM LichLamViec ll
                     JOIN NhanVien nv ON ll.MaNVLanhDao=nv.MaNV
                     LEFT JOIN DonVi dv ON ll.MaDV=dv.MaDV
                     LEFT JOIN PhongHop ph ON ll.MaPhong=ph.MaPhong
                     WHERE ll.ThoiGianBatDau >= @tuNgay AND ll.ThoiGianBatDau < @denNgayExclusive
                     ORDER BY ll.ThoiGianBatDau";
        return (await db.QueryAsync<LichLamViec>(sql, new { tuNgay, denNgayExclusive = denNgay.Date.AddDays(1) })).ToList();
    }

    // Lịch nội bộ đơn vị trong khoảng ngày — để hiển thị CHUNG trên lưới Lịch làm việc (chỉ đọc).
    public async Task<List<LichNoiBoDonVi>> GetLichNoiBoTheoKhoangNgayAsync(DateTime tuNgay, DateTime denNgay, byte? maDV)
    {
        using var db = Open();
        var where = "WHERE l.ThoiGianBatDau >= @tuNgay AND l.ThoiGianBatDau < @denNgayExclusive";
        if (maDV.HasValue) where += " AND l.MaDV=@maDV";
        return (await db.QueryAsync<LichNoiBoDonVi>(
            $@"SELECT l.*, dv.TenDV FROM LichNoiBoDonVi l LEFT JOIN DonVi dv ON l.MaDV=dv.MaDV
               {where} ORDER BY l.ThoiGianBatDau",
            new { tuNgay, denNgayExclusive = denNgay.Date.AddDays(1), maDV })).ToList();
    }

    public async Task<LichLamViec?> GetLichLamViecByIdAsync(int maLich)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<LichLamViec>(
            @"SELECT ll.*, (nv.HoNV+' '+nv.TenNV) AS TenNVLanhDao, dv.TenDV
              FROM LichLamViec ll
              JOIN NhanVien nv ON ll.MaNVLanhDao=nv.MaNV
              LEFT JOIN DonVi dv ON ll.MaDV=dv.MaDV
              WHERE ll.MaLich=@maLich", new { maLich });
    }

    public async Task<int> ThemLichLamViecAsync(LichLamViec ll, short maNVTao)
    {
        using var db = Open();
        const string sql = @"
            INSERT INTO LichLamViec (TieuDe, NoiDung, ThoiGianBatDau, ThoiGianKetThuc, DiaDiem, MaNVLanhDao, NguoiKemTheo, LoaiSuKien, MaNVTao, NgayTao, LoaiLich, MaDV, MaPhong)
            OUTPUT INSERTED.MaLich
            VALUES (@TieuDe, @NoiDung, @ThoiGianBatDau, @ThoiGianKetThuc, @DiaDiem, @MaNVLanhDao, @NguoiKemTheo, @LoaiSuKien, @MaNVTao, GETDATE(), @LoaiLich, @MaDV, @MaPhong);";
        return await db.ExecuteScalarAsync<int>(sql, new
        {
            ll.TieuDe, ll.NoiDung, ll.ThoiGianBatDau, ll.ThoiGianKetThuc, ll.DiaDiem, ll.MaNVLanhDao, ll.NguoiKemTheo, ll.LoaiSuKien,
            MaNVTao = maNVTao, ll.LoaiLich, ll.MaDV, ll.MaPhong
        });
    }

    public async Task SuaLichLamViecAsync(LichLamViec ll)
    {
        using var db = Open();
        const string sql = @"
            UPDATE LichLamViec SET
                TieuDe=@TieuDe, NoiDung=@NoiDung, ThoiGianBatDau=@ThoiGianBatDau, ThoiGianKetThuc=@ThoiGianKetThuc,
                DiaDiem=@DiaDiem, NguoiKemTheo=@NguoiKemTheo, LoaiSuKien=@LoaiSuKien,
                LoaiLich=@LoaiLich, MaDV=@MaDV, MaPhong=@MaPhong
            WHERE MaLich=@MaLich";
        await db.ExecuteAsync(sql, new
        {
            ll.MaLich, ll.TieuDe, ll.NoiDung, ll.ThoiGianBatDau, ll.ThoiGianKetThuc, ll.DiaDiem, ll.NguoiKemTheo, ll.LoaiSuKien,
            ll.LoaiLich, ll.MaDV, ll.MaPhong
        });
    }

    public async Task XoaLichLamViecAsync(int maLich)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM LichLamViec WHERE MaLich=@maLich", new { maLich });
    }

    // Lịch của RIÊNG 1 người dùng để xuất ra feed .ics (liên kết Google Calendar) — chỉ lấy đúng
    // phạm vi họ vốn xem được trên lưới Lịch làm việc: lịch công tác (LoaiLich=2, hiện cho toàn
    // trường từ trước), lịch lãnh đạo của chính họ, và lịch lãnh đạo mà họ được kèm theo. KHÔNG lấy
    // lịch lãnh đạo của người khác (đúng như lưới web chỉ hiện SỐ LƯỢNG cho người ngoài cuộc).
    public async Task<List<LichLamViec>> GetLichLamViecChoIcsAsync(short maNV, DateTime tuNgay, DateTime denNgay)
    {
        using var db = Open();
        const string sql = @"
            SELECT ll.*, (nv.HoNV+' '+nv.TenNV) AS TenNVLanhDao, dv.TenDV, ph.TenPhong
            FROM LichLamViec ll
            JOIN NhanVien nv ON ll.MaNVLanhDao=nv.MaNV
            LEFT JOIN DonVi dv ON ll.MaDV=dv.MaDV
            LEFT JOIN PhongHop ph ON ll.MaPhong=ph.MaPhong
            WHERE ll.ThoiGianBatDau >= @tuNgay AND ll.ThoiGianBatDau < @denNgayExclusive
              AND (
                    ll.LoaiLich = 2
                    OR ll.MaNVLanhDao = @maNV
                    OR (',' + ISNULL(ll.NguoiKemTheo,'') + ',') LIKE '%,' + CAST(@maNV AS varchar) + ',%'
              )
            ORDER BY ll.ThoiGianBatDau";
        return (await db.QueryAsync<LichLamViec>(sql, new { tuNgay, denNgayExclusive = denNgay.Date.AddDays(1), maNV })).ToList();
    }

    // ── Văn phòng điện tử: Thông báo nội bộ ──────────────────────────────────
    // Lọc theo đối tượng nhắm tới ở tầng C# (danh sách thông báo cho toàn trường thường
    // không lớn) — DoiTuong=0 luôn thấy, =1 khớp đơn vị, =2 khớp giao nhau với quyền hiện tại.
    public async Task<List<ThongBaoNoiBo>> GetThongBaoNoiBoAsync(byte maDV, List<int> quyen, short maNV, byte? mucDo)
    {
        using var db = Open();
        var where = "WHERE (tb.HetHan IS NULL OR tb.HetHan >= GETDATE())";
        if (mucDo.HasValue) where += " AND tb.MucDo=@mucDo";

        var sql = $@"SELECT tb.*, (nv.HoNV+' '+nv.TenNV) AS TenNVDang,
                            CASE WHEN dx.MaNV IS NULL THEN 0 ELSE 1 END AS DaXem
                     FROM ThongBaoNoiBo tb
                     JOIN NhanVien nv ON tb.MaNVDang=nv.MaNV
                     LEFT JOIN ThongBaoNoiBo_DaXem dx ON tb.MaTB=dx.MaTB AND dx.MaNV=@maNV
                     {where} ORDER BY tb.NgayDang DESC";
        var all = (await db.QueryAsync<ThongBaoNoiBo>(sql, new { maNV, mucDo })).ToList();
        return all.Where(tb =>
            tb.DoiTuong == 0
            || (tb.DoiTuong == 1 && (tb.DonViNhan ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Contains(maDV.ToString()))
            || (tb.DoiTuong == 2 && (tb.QuyenNhan ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).Intersect(quyen).Any())
        ).ToList();
    }

    public async Task<ThongBaoNoiBo?> GetThongBaoNoiBoByIdAsync(int maTB, short maNV)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<ThongBaoNoiBo>(
            @"SELECT tb.*, (nv.HoNV+' '+nv.TenNV) AS TenNVDang,
                     CASE WHEN dx.MaNV IS NULL THEN 0 ELSE 1 END AS DaXem
              FROM ThongBaoNoiBo tb
              JOIN NhanVien nv ON tb.MaNVDang=nv.MaNV
              LEFT JOIN ThongBaoNoiBo_DaXem dx ON tb.MaTB=dx.MaTB AND dx.MaNV=@maNV
              WHERE tb.MaTB=@maTB", new { maTB, maNV });
    }

    public async Task<int> ThemThongBaoNoiBoAsync(ThongBaoNoiBo tb, short maNVDang)
    {
        using var db = Open();
        const string sql = @"
            INSERT INTO ThongBaoNoiBo (TieuDe, NoiDung, DoiTuong, DonViNhan, QuyenNhan, MucDo, FileDinhKem, MaNVDang, NgayDang, HetHan, CongKhai)
            OUTPUT INSERTED.MaTB
            VALUES (@TieuDe, @NoiDung, @DoiTuong, @DonViNhan, @QuyenNhan, @MucDo, @FileDinhKem, @MaNVDang, GETDATE(), @HetHan, @CongKhai);";
        return await db.ExecuteScalarAsync<int>(sql, new
        {
            tb.TieuDe, tb.NoiDung, tb.DoiTuong, tb.DonViNhan, tb.QuyenNhan, tb.MucDo, tb.FileDinhKem, MaNVDang = maNVDang, tb.HetHan, tb.CongKhai
        });
    }

    // Thông báo hiện trên trang CÔNG KHAI (/cong-khai, không cần đăng nhập) — chỉ những cái đã được
    // người đăng chủ động tick "Công khai" và còn hạn.
    public async Task<List<ThongBaoNoiBo>> GetThongBaoCongKhaiAsync()
    {
        using var db = Open();
        return (await db.QueryAsync<ThongBaoNoiBo>(
            @"SELECT tb.*, (nv.HoNV+' '+nv.TenNV) AS TenNVDang
              FROM ThongBaoNoiBo tb
              JOIN NhanVien nv ON tb.MaNVDang=nv.MaNV
              WHERE tb.CongKhai=1 AND (tb.HetHan IS NULL OR tb.HetHan >= GETDATE())
              ORDER BY tb.NgayDang DESC")).ToList();
    }

    public async Task XoaThongBaoNoiBoAsync(int maTB)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM ThongBaoNoiBo_DaXem WHERE MaTB=@maTB", new { maTB });
        await db.ExecuteAsync("DELETE FROM ThongBaoNoiBo WHERE MaTB=@maTB", new { maTB });
    }

    public async Task DanhDauDaXemThongBaoAsync(int maTB, short maNV)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "IF NOT EXISTS(SELECT 1 FROM ThongBaoNoiBo_DaXem WHERE MaTB=@maTB AND MaNV=@maNV) " +
            "INSERT INTO ThongBaoNoiBo_DaXem (MaTB, MaNV) VALUES (@maTB, @maNV)",
            new { maTB, maNV });
    }

    public async Task<int> DemThongBaoChuaXemAsync(byte maDV, List<int> quyen, short maNV)
    {
        var all = await GetThongBaoNoiBoAsync(maDV, quyen, maNV, null);
        return all.Count(tb => !tb.DaXem);
    }

    // ── Luồng xử lý (Admin cấu hình trước) + Chuyển xử lý cho đơn vị khác ────
    public async Task<List<LuongXuLy>> GetLuongXuLyAsync()
    {
        using var db = Open();
        return (await db.QueryAsync<LuongXuLy>(
            @"SELECT lxl.*, dvn.TenDV AS TenDVNguon, dvd.TenDV AS TenDVDich
              FROM LuongXuLy lxl
              JOIN DonVi dvn ON lxl.MaDVNguon=dvn.MaDV
              JOIN DonVi dvd ON lxl.MaDVDich=dvd.MaDV
              ORDER BY dvn.TenDV, dvd.TenDV")).ToList();
    }

    public async Task<List<byte>> GetDonViDichChoPhepAsync(byte maDVNguon)
    {
        using var db = Open();
        return (await db.QueryAsync<byte>(
            "SELECT MaDVDich FROM LuongXuLy WHERE MaDVNguon=@maDVNguon", new { maDVNguon })).ToList();
    }

    public async Task ThemLuongXuLyAsync(byte maDVNguon, byte maDVDich, string? ghiChu)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "IF NOT EXISTS(SELECT 1 FROM LuongXuLy WHERE MaDVNguon=@maDVNguon AND MaDVDich=@maDVDich) " +
            "INSERT INTO LuongXuLy (MaDVNguon, MaDVDich, GhiChu) VALUES (@maDVNguon, @maDVDich, @ghiChu)",
            new { maDVNguon, maDVDich, ghiChu });
    }

    public async Task XoaLuongXuLyAsync(int id)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM LuongXuLy WHERE ID=@id", new { id });
    }

    // Chuyển xử lý: đóng dòng hiện tại (TrangThai=4, ghi ChuyenToiMaDV) + mở dòng mới cho đơn vị
    // đích (chủ trì mới, TrangThai=0) — không xóa lịch sử, giữ nguyên dòng cũ để truy vết.
    public async Task<(bool ok, string msg)> ChuyenXuLyDVAsync(string mscv, byte maDVNguon, byte maDVDich, short maNV, string? ghiChu)
    {
        if (maDVNguon == maDVDich) return (false, "Đơn vị đích phải khác đơn vị hiện tại");
        using var db = Open();
        db.Open();
        // Race condition vá 2026-09-12: trước đây "đếm xem đơn vị đích đã có chưa" rồi mới INSERT là
        // 2 câu lệnh RIÊNG, không transaction — 2 request "Chuyển xử lý" cùng lúc từ cùng đơn vị
        // nguồn (vd 2 tab, hoặc 2 người cùng có quyền) đều đọc thấy "chưa có" rồi cùng chuyển thành
        // công, tạo 2 đơn vị "chủ trì" song song cho cùng 1 văn bản. Dùng mức cô lập SERIALIZABLE +
        // transaction để request đến sau phải đợi request trước commit xong rồi mới đọc lại — không
        // còn đọc được trạng thái "chưa có" đã lỗi thời.
        using var tx = db.BeginTransaction(System.Data.IsolationLevel.Serializable);
        try
        {
            var rowNguon = await db.QueryFirstOrDefaultAsync<CongVanDenXuLyDV>(
                "SELECT * FROM CongVanDenXuLyDV WHERE MSCV=@mscv AND MaDV=@maDVNguon", new { mscv, maDVNguon }, tx);
            if (rowNguon == null || rowNguon.TrangThai == 4)
            {
                tx.Rollback();
                return (false, "Đơn vị nguồn không còn đang xử lý văn bản này (có thể đã được chuyển đi bởi thao tác khác) — vui lòng tải lại trang.");
            }

            var daCoDich = await db.QueryFirstOrDefaultAsync<int>(
                "SELECT COUNT(1) FROM CongVanDenXuLyDV WHERE MSCV=@mscv AND MaDV=@maDVDich", new { mscv, maDVDich }, tx);
            if (daCoDich > 0) { tx.Rollback(); return (false, "Đơn vị đích đã có trong danh sách xử lý văn bản này"); }

            await db.ExecuteAsync(
                @"UPDATE CongVanDenXuLyDV SET
                    TrangThai=4, ChuyenToiMaDV=@maDVDich, NgayCapNhat=GETDATE(), MaNVCapNhat=@maNV,
                    GhiChu=ISNULL(NULLIF(@ghiChu,''), GhiChu)
                  WHERE MSCV=@mscv AND MaDV=@maDVNguon AND TrangThai<>4",
                new { mscv, maDVNguon, maDVDich, maNV, ghiChu = ghiChu ?? "" }, tx);

            await db.ExecuteAsync(
                "INSERT INTO CongVanDenXuLyDV (MSCV,MaDV,LoaiDV,TrangThai) VALUES (@mscv,@maDVDich,1,0)",
                new { mscv, maDVDich }, tx);

            // Đồng bộ "đơn vị chủ trì chính" trên chính văn bản (CongVanDen.MaDVXL) — nếu không cập
            // nhật, trường này sẽ đứng yên ở đơn vị CŨ trong khi CongVanDenXuLyDV đã có chủ trì MỚI,
            // gây hiển thị lệch nhau ở những nơi đọc thẳng MaDVXL thay vì tính từ CongVanDenXuLyDV.
            await db.ExecuteAsync(
                "UPDATE CongVanDen SET MaDVXL=@maDVDich WHERE MSCV=@mscv AND MaDVXL=@maDVNguon",
                new { mscv, maDVDich, maDVNguon }, tx);

            await ThemThongBaoCaNhanNoiBoAsync(db, maDVDich, null, 2,
                $"Văn bản {mscv} vừa được chuyển xử lý đến đơn vị bạn", mscv, tx);

            // CVDen_QTXL (lịch sử xử lý, hiển thị ở "Lịch sử xử lý") có FK yêu cầu (MSCV,MaDV) đã tồn
            // tại trong CVDen_DV trước — cùng cách ThemXuLyAsync đang làm.
            await db.ExecuteAsync(
                "IF NOT EXISTS(SELECT 1 FROM CVDen_DV WHERE MSCV=@mscv AND MaDV=@maDVNguon) " +
                "INSERT INTO CVDen_DV(MSCV, MaDV, MaNV, NgayGiao) VALUES(@mscv, @maDVNguon, @maNV, GETDATE())",
                new { mscv, maDVNguon, maNV }, tx);
            await db.ExecuteAsync(
                "INSERT INTO CVDen_QTXL (MSCV, MaDV, MaNV, NgayXL, NoiDungXL) VALUES (@mscv, @maDVNguon, @maNV, GETDATE(), @noiDung)",
                new { mscv, maDVNguon, maNV, noiDung = $"Chuyển xử lý cho đơn vị khác" + (string.IsNullOrWhiteSpace(ghiChu) ? "" : $": {ghiChu}") }, tx);

            tx.Commit();
            return (true, "Đã chuyển xử lý thành công");
        }
        catch { tx.Rollback(); throw; }
    }

    // ── Luồng xử lý theo vai trò: Lãnh đạo đơn vị → BGH → Chuyên viên cụ thể ──

    public async Task<List<CongVanDenChiDao>> GetChiDaoAsync(string mscv)
    {
        using var db = Open();
        return (await db.QueryAsync<CongVanDenChiDao>(
            @"SELECT cd.*, dv.TenDV,
                     (nv.HoNV+' '+nv.TenNV)   AS TenNV,
                     (nvn.HoNV+' '+nvn.TenNV) AS TenNVNhan,
                     (nvp.HoNV+' '+nvp.TenNV) AS TenNVPhanCong
              FROM CongVanDen_ChiDao cd
              JOIN NhanVien nv ON cd.MaNV = nv.MaNV
              LEFT JOIN DonVi dv ON cd.MaDV = dv.MaDV
              LEFT JOIN NhanVien nvn ON cd.MaNVNhan = nvn.MaNV
              LEFT JOIN NhanVien nvp ON cd.MaNVPhanCong = nvp.MaNV
              WHERE cd.MSCV = @mscv
              ORDER BY cd.NgayTao", new { mscv })).ToList();
    }

    // Lãnh đạo đơn vị (hoặc admin/văn thư) chỉ đạo & phân công cho một chuyên viên cụ thể —
    // tự tạo 1 CongViec liên kết ngược lại văn bản gốc qua LoaiNguonGoc/MSCVGoc.
    // maNVPhanCong = người chủ trì thực hiện (bắt buộc); nguoiPhoiHop = những người hỗ trợ thêm
    // (tùy chọn, có thể chọn nhiều) — tái dùng đúng mô hình chủ trì+phối hợp CongViec đã có sẵn,
    // thay vì tạo nhiều Công việc rời rạc cho cùng 1 việc.
    public async Task<int> ChiDaoPhanCongAsync(string mscv, byte maDV, short maNVChiDao, short maNVPhanCong,
        List<short>? nguoiPhoiHop, string? noiDung, DateTime? hanXuLy)
    {
        using var db = Open();
        var trichYeu = await db.QueryFirstOrDefaultAsync<string>(
            "SELECT TrichYeu FROM CongVanDen WHERE MSCV=@mscv", new { mscv }) ?? "";
        string? phoiHopCsv = nguoiPhoiHop != null && nguoiPhoiHop.Count > 0
            ? string.Join(",", nguoiPhoiHop.Where(x => x != maNVPhanCong).Distinct())
            : null;
        if (phoiHopCsv == "") phoiHopCsv = null;

        var maCV = await db.ExecuteScalarAsync<int>(
            @"INSERT INTO CongViec
                (TieuDe, MoTa, MaNVGiao, MaNVChuTri, MaDV, NguoiPhoiHop, NgayGiao, HanXuLy, MucDoUuTien, TrangThai,
                 LoaiNguonGoc, MSCVGoc, GhiChu, MaNVTao, NgayTao)
              OUTPUT INSERTED.MaCV
              VALUES
                (@tieuDe, @noiDung, @maNVChiDao, @maNVPhanCong, @maDV, @phoiHopCsv, GETDATE(), @hanXuLy, 1, 0,
                 1, @mscv, @noiDung, @maNVChiDao, GETDATE())",
            new { tieuDe = "Xử lý văn bản đến: " + trichYeu, noiDung, maNVChiDao, maNVPhanCong, maDV, phoiHopCsv, mscv, hanXuLy });

        await db.ExecuteAsync(
            @"INSERT INTO CongVanDen_ChiDao (MSCV, MaDV, LoaiHanhDong, MaNV, MaNVPhanCong, MaCVLienKet, NoiDung)
              VALUES (@mscv, @maDV, 1, @maNVChiDao, @maNVPhanCong, @maCV, @noiDung)",
            new { mscv, maDV, maNVChiDao, maNVPhanCong, maCV, noiDung });

        // Đồng bộ sang hộp thư cá nhân: chủ trì = Xử lý chính, phối hợp = Đồng xử lý.
        var nhan = new List<NguoiNhanXuLy> { new() { MaNV = maNVPhanCong, VaiTro = VanBanXuLy.VaiTroChinh } };
        if (nguoiPhoiHop != null)
            nhan.AddRange(nguoiPhoiHop.Where(x => x != maNVPhanCong).Distinct()
                .Select(x => new NguoiNhanXuLy { MaNV = x, VaiTro = VanBanXuLy.VaiTroDongXuLy }));
        await ChuyenXuLyAsync(VanBanXuLy.LoaiDen, mscv.Trim(), maNVChiDao, nhan, noiDung, hanXuLy, null);

        await db.ExecuteAsync(
            @"UPDATE CongVanDenXuLyDV SET TrangThai=2, NgayCapNhat=GETDATE(), MaNVCapNhat=@maNVChiDao,
                NgayTiepNhan=ISNULL(NgayTiepNhan,GETDATE())
              WHERE MSCV=@mscv AND MaDV=@maDV AND TrangThai<2",
            new { mscv, maDV, maNVChiDao });

        return maCV;
    }

    // Lãnh đạo đơn vị xin ý kiến chỉ đạo của một lãnh đạo BGH (quyền 12) cụ thể.
    public async Task XinYKienBGHAsync(string mscv, byte maDV, short maNV, short maNVNhan, string noiDung)
    {
        using var db = Open();
        await db.ExecuteAsync(
            @"INSERT INTO CongVanDen_ChiDao (MSCV, MaDV, LoaiHanhDong, MaNV, MaNVNhan, NoiDung)
              VALUES (@mscv, @maDV, 2, @maNV, @maNVNhan, @noiDung)",
            new { mscv, maDV, maNV, maNVNhan, noiDung });
    }

    // BGH ghi ý kiến chỉ đạo trả lời — lãnh đạo đơn vị dựa vào đó để chỉ đạo & phân công tiếp.
    public async Task BGHTraLoiYKienAsync(string mscv, byte maDV, short maNVBGH, string noiDung)
    {
        using var db = Open();
        await db.ExecuteAsync(
            @"INSERT INTO CongVanDen_ChiDao (MSCV, MaDV, LoaiHanhDong, MaNV, NoiDung)
              VALUES (@mscv, @maDV, 3, @maNVBGH, @noiDung)",
            new { mscv, maDV, maNVBGH, noiDung });
    }

    // Trả lại văn thư — dùng khi giao nhầm đơn vị/thiếu thông tin. Giữ lại dòng CongVanDenXuLyDV
    // (TrangThai=5) để truy vết thay vì xoá; check tự-đóng-văn-bản đã loại trừ TrangThai=5.
    public async Task TraLaiXuLyDVAsync(string mscv, byte maDV, short maNV, string lyDo)
    {
        using var db = Open();
        await db.ExecuteAsync(
            @"UPDATE CongVanDenXuLyDV SET TrangThai=5, NgayCapNhat=GETDATE(), MaNVCapNhat=@maNV,
                GhiChu=@lyDo
              WHERE MSCV=@mscv AND MaDV=@maDV",
            new { mscv, maDV, maNV, lyDo });

        await db.ExecuteAsync(
            @"INSERT INTO CongVanDen_ChiDao (MSCV, MaDV, LoaiHanhDong, MaNV, NoiDung)
              VALUES (@mscv, @maDV, 4, @maNV, @lyDo)",
            new { mscv, maDV, maNV, lyDo });

        // Ghi cả vào "Lịch sử xử lý" cũ (CVDen_QTXL) để 2 nơi hiển thị nhất quán — cần guard
        // CVDen_DV như ChuyenXuLyDVAsync đang làm vì CVDen_QTXL có FK ràng buộc theo (MSCV,MaDV).
        await db.ExecuteAsync(
            "IF NOT EXISTS(SELECT 1 FROM CVDen_DV WHERE MSCV=@mscv AND MaDV=@maDV) " +
            "INSERT INTO CVDen_DV(MSCV, MaDV, MaNV, NgayGiao) VALUES(@mscv, @maDV, @maNV, GETDATE())",
            new { mscv, maDV, maNV });
        await db.ExecuteAsync(
            "INSERT INTO CVDen_QTXL (MSCV, MaDV, MaNV, NgayXL, NoiDungXL) VALUES (@mscv, @maDV, @maNV, GETDATE(), @noiDung)",
            new { mscv, maDV, maNV, noiDung = "Trả lại văn thư: " + lyDo });
    }

    // Chuyên viên (chủ trì CongViec) xin trả lại/chuyển người khác — ghi vào nhật ký công việc và,
    // nếu công việc gắn với 1 văn bản đến, ghi cả vào luồng chỉ đạo của văn bản đó để lãnh đạo thấy.
    public async Task ChuyenVienXinTraLaiAsync(int maCV, short maNV, string lyDo)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "INSERT INTO CongViec_NhatKy (MaCV, MaNV, NgayGhi, NoiDung) VALUES (@maCV, @maNV, GETDATE(), @noiDung)",
            new { maCV, maNV, noiDung = "Xin trả lại/chuyển người khác: " + lyDo });

        var cv = await db.QueryFirstOrDefaultAsync<(byte? LoaiNguonGoc, string? MSCVGoc, byte MaDV)>(
            "SELECT LoaiNguonGoc, MSCVGoc, MaDV FROM CongViec WHERE MaCV=@maCV", new { maCV });
        if (cv.LoaiNguonGoc == 1 && !string.IsNullOrWhiteSpace(cv.MSCVGoc))
        {
            await db.ExecuteAsync(
                @"INSERT INTO CongVanDen_ChiDao (MSCV, MaDV, LoaiHanhDong, MaNV, MaCVLienKet, NoiDung)
                  VALUES (@mscv, @maDV, 5, @maNV, @maCV, @noiDung)",
                new { mscv = cv.MSCVGoc.Trim(), maDV = cv.MaDV, maNV, maCV, noiDung = lyDo });
        }
    }

    // ── Phân quyền chi tiết: Vai trò + chức năng ─────────────────────────────
    public async Task<List<VaiTro>> GetVaiTroAsync()
    {
        using var db = Open();
        var roles = (await db.QueryAsync<VaiTro>(
            "SELECT vt.*, (SELECT COUNT(*) FROM NhanVien_VaiTro nvt WHERE nvt.MaVaiTro=vt.MaVaiTro) AS SoNguoi " +
            "FROM VaiTro vt ORDER BY vt.TenVaiTro")).ToList();
        var allMap = (await db.QueryAsync<(int MaVaiTro, string MaChucNang)>(
            "SELECT MaVaiTro, MaChucNang FROM VaiTro_Quyen")).ToList();
        foreach (var r in roles)
            r.DanhSachChucNang = allMap.Where(x => x.MaVaiTro == r.MaVaiTro).Select(x => x.MaChucNang).ToList();
        return roles;
    }

    public async Task<int> ThemVaiTroAsync(string tenVaiTro, string? ghiChu)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<int>(
            "INSERT INTO VaiTro (TenVaiTro, GhiChu) OUTPUT INSERTED.MaVaiTro VALUES (@tenVaiTro, @ghiChu)",
            new { tenVaiTro, ghiChu });
    }

    public async Task XoaVaiTroAsync(int maVaiTro)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM VaiTro_Quyen WHERE MaVaiTro=@maVaiTro", new { maVaiTro });
        await db.ExecuteAsync("DELETE FROM NhanVien_VaiTro WHERE MaVaiTro=@maVaiTro", new { maVaiTro });
        await db.ExecuteAsync("DELETE FROM VaiTro WHERE MaVaiTro=@maVaiTro", new { maVaiTro });
    }

    public async Task SetVaiTroChucNangAsync(int maVaiTro, string maChucNang, bool add)
    {
        using var db = Open();
        if (add)
            await db.ExecuteAsync(
                "IF NOT EXISTS(SELECT 1 FROM VaiTro_Quyen WHERE MaVaiTro=@maVaiTro AND MaChucNang=@maChucNang) " +
                "INSERT INTO VaiTro_Quyen (MaVaiTro, MaChucNang) VALUES (@maVaiTro, @maChucNang)",
                new { maVaiTro, maChucNang });
        else
            await db.ExecuteAsync(
                "DELETE FROM VaiTro_Quyen WHERE MaVaiTro=@maVaiTro AND MaChucNang=@maChucNang",
                new { maVaiTro, maChucNang });
    }

    public async Task<List<int>> GetVaiTroCuaNhanVienAsync(short maNV)
    {
        using var db = Open();
        return (await db.QueryAsync<int>(
            "SELECT MaVaiTro FROM NhanVien_VaiTro WHERE MaNV=@maNV", new { maNV })).ToList();
    }

    public async Task SetNhanVienVaiTroAsync(short maNV, int maVaiTro, bool add)
    {
        using var db = Open();
        if (add)
            await db.ExecuteAsync(
                "IF NOT EXISTS(SELECT 1 FROM NhanVien_VaiTro WHERE MaNV=@maNV AND MaVaiTro=@maVaiTro) " +
                "INSERT INTO NhanVien_VaiTro (MaNV, MaVaiTro) VALUES (@maNV, @maVaiTro)",
                new { maNV, maVaiTro });
        else
            await db.ExecuteAsync(
                "DELETE FROM NhanVien_VaiTro WHERE MaNV=@maNV AND MaVaiTro=@maVaiTro",
                new { maNV, maVaiTro });
    }

    public async Task SetNhanVienChucNangAsync(short maNV, string maChucNang, bool add)
    {
        using var db = Open();
        if (add)
            await db.ExecuteAsync(
                "IF NOT EXISTS(SELECT 1 FROM NhanVien_ChucNang WHERE MaNV=@maNV AND MaChucNang=@maChucNang) " +
                "INSERT INTO NhanVien_ChucNang (MaNV, MaChucNang) VALUES (@maNV, @maChucNang)",
                new { maNV, maChucNang });
        else
            await db.ExecuteAsync(
                "DELETE FROM NhanVien_ChucNang WHERE MaNV=@maNV AND MaChucNang=@maChucNang",
                new { maNV, maChucNang });
    }

    public async Task<List<string>> GetChucNangTrucTiepCuaNhanVienAsync(short maNV)
    {
        using var db = Open();
        return (await db.QueryAsync<string>(
            "SELECT MaChucNang FROM NhanVien_ChucNang WHERE MaNV=@maNV", new { maNV })).ToList();
    }

    // Quyền hiệu lực của 1 người = hợp của (chức năng từ mọi vai trò được gán) ∪ (cấp trực tiếp).
    // Dùng lúc đăng nhập để lưu vào session (tránh truy vấn DB ở mỗi request).
    public async Task<List<string>> GetChucNangHieuLucAsync(short maNV)
    {
        using var db = Open();
        var tuVaiTro = await db.QueryAsync<string>(
            @"SELECT DISTINCT vq.MaChucNang FROM NhanVien_VaiTro nvt
              JOIN VaiTro_Quyen vq ON nvt.MaVaiTro=vq.MaVaiTro
              WHERE nvt.MaNV=@maNV", new { maNV });
        var trucTiep = await db.QueryAsync<string>(
            "SELECT MaChucNang FROM NhanVien_ChucNang WHERE MaNV=@maNV", new { maNV });
        return tuVaiTro.Union(trucTiep).ToList();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NỘI BỘ ĐƠN VỊ — lịch làm việc chung, sổ văn bản riêng, API Key theo đơn vị
    // ═══════════════════════════════════════════════════════════════════════

    // ── Lịch nội bộ đơn vị ──────────────────────────────────────────────────
    public async Task<List<LichNoiBoDonVi>> GetLichNoiBoDonViTheoKhoangNgayAsync(byte maDV, DateTime tuNgay, DateTime denNgay)
    {
        using var db = Open();
        const string sql = @"SELECT l.*, dv.TenDV, (nv.HoNV+' '+nv.TenNV) AS TenNVTao
                     FROM LichNoiBoDonVi l
                     JOIN DonVi dv ON l.MaDV=dv.MaDV
                     JOIN NhanVien nv ON l.MaNVTao=nv.MaNV
                     WHERE l.MaDV=@maDV AND l.ThoiGianBatDau >= @tuNgay AND l.ThoiGianBatDau < @denNgayExclusive
                     ORDER BY l.ThoiGianBatDau";
        return (await db.QueryAsync<LichNoiBoDonVi>(sql, new { maDV, tuNgay, denNgayExclusive = denNgay.Date.AddDays(1) })).ToList();
    }

    public async Task<LichNoiBoDonVi?> GetLichNoiBoDonViByIdAsync(int maLich)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<LichNoiBoDonVi>(
            @"SELECT l.*, dv.TenDV, (nv.HoNV+' '+nv.TenNV) AS TenNVTao
              FROM LichNoiBoDonVi l
              JOIN DonVi dv ON l.MaDV=dv.MaDV
              JOIN NhanVien nv ON l.MaNVTao=nv.MaNV
              WHERE l.MaLich=@maLich", new { maLich });
    }

    public async Task<int> ThemLichNoiBoDonViAsync(LichNoiBoDonVi l, short maNVTao)
    {
        using var db = Open();
        const string sql = @"
            INSERT INTO LichNoiBoDonVi (MaDV, TieuDe, NoiDung, ThoiGianBatDau, ThoiGianKetThuc, DiaDiem, LoaiSuKien, MaNVTao, NgayTao)
            OUTPUT INSERTED.MaLich
            VALUES (@MaDV, @TieuDe, @NoiDung, @ThoiGianBatDau, @ThoiGianKetThuc, @DiaDiem, @LoaiSuKien, @MaNVTao, GETDATE());";
        return await db.ExecuteScalarAsync<int>(sql, new
        {
            l.MaDV, l.TieuDe, l.NoiDung, l.ThoiGianBatDau, l.ThoiGianKetThuc, l.DiaDiem, l.LoaiSuKien, MaNVTao = maNVTao
        });
    }

    public async Task SuaLichNoiBoDonViAsync(LichNoiBoDonVi l)
    {
        using var db = Open();
        const string sql = @"
            UPDATE LichNoiBoDonVi SET
                TieuDe=@TieuDe, NoiDung=@NoiDung, ThoiGianBatDau=@ThoiGianBatDau, ThoiGianKetThuc=@ThoiGianKetThuc,
                DiaDiem=@DiaDiem, LoaiSuKien=@LoaiSuKien
            WHERE MaLich=@MaLich";
        await db.ExecuteAsync(sql, new { l.MaLich, l.TieuDe, l.NoiDung, l.ThoiGianBatDau, l.ThoiGianKetThuc, l.DiaDiem, l.LoaiSuKien });
    }

    public async Task XoaLichNoiBoDonViAsync(int maLich)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM LichNoiBoDonVi WHERE MaLich=@maLich", new { maLich });
    }

    // ── Văn bản nội bộ đơn vị ───────────────────────────────────────────────
    public async Task<List<VanBanNoiBo>> GetVanBanNoiBoAsync(byte maDV, int? nam, string? tuKhoa)
    {
        using var db = Open();
        var where = "WHERE v.MaDV=@maDV";
        if (nam.HasValue) where += " AND YEAR(v.NgayBanHanh)=@nam";
        if (!string.IsNullOrWhiteSpace(tuKhoa)) where += " AND (v.TieuDe LIKE @tk OR v.SoHieu LIKE @tk)";
        var sql = $@"SELECT v.*, dv.TenDV, (tao.HoNV+' '+tao.TenNV) AS TenNVTao, (ky.HoNV+' '+ky.TenNV) AS TenNVKy,
                            (SELECT COUNT(1) FROM VanBanNoiBo_File f WHERE f.VanBanID=v.ID) AS SoFile
                     FROM VanBanNoiBo v
                     JOIN DonVi dv ON v.MaDV=dv.MaDV
                     LEFT JOIN NhanVien tao ON v.MaNVTao=tao.MaNV
                     LEFT JOIN NhanVien ky ON v.MaNVKy=ky.MaNV
                     {where} ORDER BY v.NgayBanHanh DESC, v.STT DESC";
        return (await db.QueryAsync<VanBanNoiBo>(sql, new { maDV, nam, tk = $"%{tuKhoa}%" })).ToList();
    }

    public async Task<VanBanNoiBo?> GetVanBanNoiBoByIdAsync(int id, byte? maDVGioiHan = null)
    {
        using var db = Open();
        var where = "WHERE v.ID=@id";
        if (maDVGioiHan.HasValue) where += " AND v.MaDV=@maDVGioiHan";
        var sql = $@"SELECT v.*, dv.TenDV, (tao.HoNV+' '+tao.TenNV) AS TenNVTao, (ky.HoNV+' '+ky.TenNV) AS TenNVKy,
                            (SELECT COUNT(1) FROM VanBanNoiBo_File f WHERE f.VanBanID=v.ID) AS SoFile
                     FROM VanBanNoiBo v
                     JOIN DonVi dv ON v.MaDV=dv.MaDV
                     LEFT JOIN NhanVien tao ON v.MaNVTao=tao.MaNV
                     LEFT JOIN NhanVien ky ON v.MaNVKy=ky.MaNV
                     {where}";
        return await db.QueryFirstOrDefaultAsync<VanBanNoiBo>(sql, new { id, maDVGioiHan });
    }

    public async Task<int> NextSttVanBanNoiBoAsync(byte maDV, int nam)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<int>(
            "SELECT ISNULL(MAX(STT),0)+1 FROM VanBanNoiBo WHERE MaDV=@maDV AND YEAR(NgayBanHanh)=@nam", new { maDV, nam });
    }

    // ── Mẫu số hiệu tự động của văn bản nội bộ đơn vị ────────────────────────
    public async Task<List<VanBanNoiBoMauSo>> GetVanBanNoiBoMauSoAsync(byte maDV)
    {
        using var db = Open();
        return (await db.QueryAsync<VanBanNoiBoMauSo>(
            "SELECT * FROM VanBanNoiBo_MauSo WHERE MaDV=@maDV ORDER BY MaLoai", new { maDV })).ToList();
    }

    public async Task<int> ThemVanBanNoiBoMauSoAsync(byte maDV, string maLoai, string tenLoai, string mauChuoi)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<int>(
            @"INSERT INTO VanBanNoiBo_MauSo (MaDV, MaLoai, TenLoai, MauChuoi, NgayTao) OUTPUT INSERTED.ID
              VALUES (@maDV, @maLoai, @tenLoai, @mauChuoi, GETDATE());", new { maDV, maLoai, tenLoai, mauChuoi });
    }

    public async Task XoaVanBanNoiBoMauSoAsync(int id)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM VanBanNoiBo_MauSo WHERE ID=@id", new { id });
    }

    // Tính STT tiếp theo VÀ số hiệu gợi ý (nếu đơn vị đã cấu hình mẫu cho maLoai đó) — mirror
    // GetNextSoKyHieuDiAsync/GetNextSoKyHieuDieuHanhAsync nhưng khoanh theo (MaDV, LoaiVanBanNoiBo)
    // thay vì (MaSCV, MaLVB) toàn trường, vì VanBanNoiBo tự quản lý số riêng theo từng đơn vị.
    public async Task<(int Stt, string? SoHieu)> GetNextSoHieuNoiBoAsync(byte maDV, string maLoai, int nam)
    {
        using var db = Open();
        int stt = await db.QueryFirstOrDefaultAsync<int?>(
            "SELECT MAX(STT) FROM VanBanNoiBo WHERE MaDV=@maDV AND LoaiVanBanNoiBo=@maLoai AND YEAR(NgayBanHanh)=@nam",
            new { maDV, maLoai, nam }) ?? 0;
        stt++;

        var mauChuoi = await db.QueryFirstOrDefaultAsync<string>(
            "SELECT MauChuoi FROM VanBanNoiBo_MauSo WHERE MaDV=@maDV AND MaLoai=@maLoai", new { maDV, maLoai });
        if (mauChuoi == null) return (stt, null);

        var soHieu = mauChuoi
            .Replace("{STT3}", stt.ToString("000"))
            .Replace("{STT2}", stt.ToString("00"))
            .Replace("{STT}", stt.ToString())
            .Replace("{NAM2}", (nam % 100).ToString("00"))
            .Replace("{NAM}", nam.ToString());
        return (stt, soHieu);
    }

    public async Task<int> ThemVanBanNoiBoAsync(VanBanNoiBo v)
    {
        using var db = Open();
        const string sql = @"
            INSERT INTO VanBanNoiBo (MaDV, STT, SoHieu, TieuDe, NoiDung, NgayBanHanh, NguoiKy, MaNVKy, TrangThai, MaNVTao, NgayTao, NguonTao, LoaiVanBanNoiBo)
            OUTPUT INSERTED.ID
            VALUES (@MaDV, @STT, @SoHieu, @TieuDe, @NoiDung, @NgayBanHanh, @NguoiKy, @MaNVKy, @TrangThai, @MaNVTao, GETDATE(), @NguonTao, @LoaiVanBanNoiBo);";
        return await db.ExecuteScalarAsync<int>(sql, new
        {
            v.MaDV, v.STT, v.SoHieu, v.TieuDe, v.NoiDung, v.NgayBanHanh, v.NguoiKy, v.MaNVKy, v.TrangThai, v.MaNVTao, v.NguonTao, v.LoaiVanBanNoiBo
        });
    }

    public async Task SuaVanBanNoiBoAsync(VanBanNoiBo v)
    {
        using var db = Open();
        const string sql = @"
            UPDATE VanBanNoiBo SET
                SoHieu=@SoHieu, TieuDe=@TieuDe, NoiDung=@NoiDung, NgayBanHanh=@NgayBanHanh,
                NguoiKy=@NguoiKy, MaNVKy=@MaNVKy, TrangThai=@TrangThai
            WHERE ID=@ID";
        await db.ExecuteAsync(sql, new { v.ID, v.SoHieu, v.TieuDe, v.NoiDung, v.NgayBanHanh, v.NguoiKy, v.MaNVKy, v.TrangThai });
    }

    public async Task XoaVanBanNoiBoAsync(int id)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM VanBanNoiBo WHERE ID=@id", new { id }); // file con tự xóa theo (ON DELETE CASCADE)
    }

    public async Task<List<VanBanNoiBoFile>> GetVanBanNoiBoFilesAsync(int vanBanId)
    {
        using var db = Open();
        return (await db.QueryAsync<VanBanNoiBoFile>(
            "SELECT * FROM VanBanNoiBo_File WHERE VanBanID=@vanBanId ORDER BY NgayUpload", new { vanBanId })).ToList();
    }

    public async Task<VanBanNoiBoFile?> GetVanBanNoiBoFileByIdAsync(int id)
    {
        using var db = Open();
        return await db.QueryFirstOrDefaultAsync<VanBanNoiBoFile>("SELECT * FROM VanBanNoiBo_File WHERE ID=@id", new { id });
    }

    public async Task<int> ThemVanBanNoiBoFileAsync(int vanBanId, string tenFile, string duongDan)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<int>(
            @"INSERT INTO VanBanNoiBo_File (VanBanID, TenFile, DuongDan, NgayUpload) OUTPUT INSERTED.ID
              VALUES (@vanBanId, @tenFile, @duongDan, GETDATE());", new { vanBanId, tenFile, duongDan });
    }

    public async Task XoaVanBanNoiBoFileAsync(int id)
    {
        using var db = Open();
        await db.ExecuteAsync("DELETE FROM VanBanNoiBo_File WHERE ID=@id", new { id });
    }

    // ── API Key theo đơn vị ─────────────────────────────────────────────────
    public async Task<List<DonViApiKey>> GetApiKeysAsync(byte? maDV = null)
    {
        using var db = Open();
        var where = maDV.HasValue ? "WHERE k.MaDV=@maDV" : "WHERE 1=1";
        var sql = $@"SELECT k.*, dv.TenDV, (nv.HoNV+' '+nv.TenNV) AS TenNVTao
                     FROM DonVi_ApiKey k
                     JOIN DonVi dv ON k.MaDV=dv.MaDV
                     JOIN NhanVien nv ON k.MaNVTao=nv.MaNV
                     {where} ORDER BY dv.STT, k.NgayTao DESC";
        return (await db.QueryAsync<DonViApiKey>(sql, new { maDV })).ToList();
    }

    public async Task<int> TaoApiKeyAsync(byte maDV, string tenKey, string apiKeyHash, short maNVTao)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<int>(
            @"INSERT INTO DonVi_ApiKey (MaDV, TenKey, ApiKeyHash, HienThi, NgayTao, MaNVTao) OUTPUT INSERTED.ID
              VALUES (@maDV, @tenKey, @apiKeyHash, 1, GETDATE(), @maNVTao);", new { maDV, tenKey, apiKeyHash, maNVTao });
    }

    public async Task ThuHoiApiKeyAsync(int id)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE DonVi_ApiKey SET HienThi=0 WHERE ID=@id", new { id });
    }

    // Xác thực API Key: trả về (MaDV, ID) nếu key hợp lệ & còn hiệu lực, null nếu không.
    // So khớp bằng hash (SHA-256 của key thật, tính sẵn ở tầng gọi) — không bao giờ lưu/so plaintext.
    public async Task<(byte MaDV, int ID)?> ResolveApiKeyAsync(string apiKeyHash)
    {
        using var db = Open();
        var row = await db.QueryFirstOrDefaultAsync<(byte MaDV, int ID)?>(
            "SELECT MaDV, ID FROM DonVi_ApiKey WHERE ApiKeyHash=@apiKeyHash AND HienThi=1", new { apiKeyHash });
        return row;
    }

    public async Task CapNhatNgaySuDungApiKeyAsync(int id)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE DonVi_ApiKey SET NgaySuDungCuoi=GETDATE() WHERE ID=@id", new { id });
    }

    // ── Thông báo sự kiện xử lý (ThongBaoCaNhan) ─────────────────────────────
    // Overload nội bộ dùng lại connection đã mở sẵn của method gọi (TaoXuLyDVAsync/ChuyenXuLyDVAsync)
    // thay vì mở connection mới — 2 nơi đó đã ở giữa 1 chuỗi thao tác cần cùng 1 kết nối.
    private static async Task ThemThongBaoCaNhanNoiBoAsync(SqlConnection db, byte maDV, short? maNV, byte loai, string noiDung, string? mscv, System.Data.IDbTransaction? tx = null)
    {
        await db.ExecuteAsync(
            "INSERT INTO ThongBaoCaNhan (MaDV, MaNV, Loai, NoiDung, MSCV, NgayTao, DaXem) VALUES (@maDV, @maNV, @loai, @noiDung, @mscv, GETDATE(), 0)",
            new { maDV, maNV, loai, noiDung, mscv }, tx);
    }

    public async Task ThemThongBaoCaNhanAsync(byte maDV, short? maNV, byte loai, string noiDung, string? mscv)
    {
        using var db = Open();
        await ThemThongBaoCaNhanNoiBoAsync(db, maDV, maNV, loai, noiDung, mscv);
    }

    public async Task<int> DemThongBaoChuaXemAsync(short maNV, byte maDV)
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM ThongBaoCaNhan WHERE DaXem=0 AND ((MaDV=@maDV AND MaNV IS NULL) OR MaNV=@maNV)",
            new { maDV, maNV });
    }

    public async Task<List<ThongBaoCaNhan>> GetThongBaoCaNhanAsync(short maNV, byte maDV, int limit)
    {
        using var db = Open();
        return (await db.QueryAsync<ThongBaoCaNhan>(
            "SELECT TOP (@limit) * FROM ThongBaoCaNhan WHERE (MaDV=@maDV AND MaNV IS NULL) OR MaNV=@maNV ORDER BY NgayTao DESC",
            new { maDV, maNV, limit })).ToList();
    }

    public async Task DanhDauDaXemThongBaoAsync(int id)
    {
        using var db = Open();
        await db.ExecuteAsync("UPDATE ThongBaoCaNhan SET DaXem=1, NgayXem=GETDATE() WHERE ID=@id", new { id });
    }

    public async Task DanhDauTatCaDaXemThongBaoAsync(short maNV, byte maDV)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "UPDATE ThongBaoCaNhan SET DaXem=1, NgayXem=GETDATE() WHERE DaXem=0 AND ((MaDV=@maDV AND MaNV IS NULL) OR MaNV=@maNV)",
            new { maDV, maNV });
    }
}
