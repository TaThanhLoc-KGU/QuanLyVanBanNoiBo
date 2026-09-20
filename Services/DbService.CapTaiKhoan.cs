using Dapper;
using CongVan.Models;

namespace CongVan.Services;

// Phân CẤP tài khoản theo vai trò nghiệp vụ (dùng để xuất Excel cho quản trị viên và để định tuyến văn bản đi):
//   Lãnh đạo trường = PhanQuyen 12 · Lãnh đạo đơn vị = DonVi.MaNV_TDV · Văn thư cấp 1 (văn thư trường) = PhanQuyen 99
//   hoặc chức năng CongVanDi.Nhap · Văn thư cấp 2 (văn thư đơn vị) = DonVi_VanThu · Chuyên viên = còn lại.
public class TaiKhoanCap
{
    public short MaNV { get; set; }
    public string HoNV { get; set; } = "";
    public string TenNV { get; set; } = "";
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? TenDV { get; set; }
    public DateTime? NgayNghiViec { get; set; }
    public bool LdTruong { get; set; }
    public bool LdDonVi { get; set; }
    public bool VtCap1 { get; set; }
    public bool VtCap2 { get; set; }
    public bool QuanTri { get; set; }

    public string HoTen => $"{HoNV} {TenNV}".Trim();
    public bool ChuyenVien => !LdTruong && !LdDonVi && !VtCap1 && !VtCap2;

    // Cấp cao nhất (mỗi người 1 dòng ở trang "Tất cả")
    public string CapCaoNhat => LdTruong ? "Lãnh đạo trường" : LdDonVi ? "Lãnh đạo đơn vị" : VtCap1 ? "Văn thư cấp 1"
        : VtCap2 ? "Văn thư cấp 2" : "Chuyên viên";

    public string CacVaiTro => string.Join(", ", new[]
    {
        LdTruong ? "Lãnh đạo trường" : null, LdDonVi ? "Lãnh đạo đơn vị" : null, VtCap1 ? "Văn thư cấp 1" : null,
        VtCap2 ? "Văn thư cấp 2" : null, QuanTri ? "Quản trị" : null
    }.Where(x => x != null));
}

public partial class DbService
{
    private const string SqlVanThuCap1 = @"(
        EXISTS(SELECT 1 FROM PhanQuyen WHERE MaNV=nv.MaNV AND MaQuyen=99)
        OR EXISTS(SELECT 1 FROM NhanVien_ChucNang WHERE MaNV=nv.MaNV AND MaChucNang='CongVanDi.Nhap')
        OR EXISTS(SELECT 1 FROM NhanVien_VaiTro nvt JOIN VaiTro_Quyen vq ON nvt.MaVaiTro=vq.MaVaiTro
                  WHERE nvt.MaNV=nv.MaNV AND vq.MaChucNang='CongVanDi.Nhap'))";

    public async Task<List<TaiKhoanCap>> GetTaiKhoanTheoCapAsync(bool baoGomNghiViec)
    {
        using var db = Open();
        var sql = $@"
            SELECT nv.MaNV, nv.HoNV, nv.TenNV, nv.Username, nv.Email, dv.TenDV, nv.NgayNghiViec,
                   CAST(CASE WHEN EXISTS(SELECT 1 FROM PhanQuyen WHERE MaNV=nv.MaNV AND MaQuyen=12) THEN 1 ELSE 0 END AS bit) AS LdTruong,
                   CAST(CASE WHEN EXISTS(SELECT 1 FROM DonVi WHERE MaNV_TDV=nv.MaNV) THEN 1 ELSE 0 END AS bit) AS LdDonVi,
                   CAST(CASE WHEN {SqlVanThuCap1} THEN 1 ELSE 0 END AS bit) AS VtCap1,
                   CAST(CASE WHEN EXISTS(SELECT 1 FROM DonVi_VanThu WHERE MaNV=nv.MaNV) THEN 1 ELSE 0 END AS bit) AS VtCap2,
                   CAST(CASE WHEN EXISTS(SELECT 1 FROM PhanQuyen WHERE MaNV=nv.MaNV AND MaQuyen=0) THEN 1 ELSE 0 END AS bit) AS QuanTri
            FROM NhanVien nv LEFT JOIN DonVi dv ON nv.MaDV=dv.MaDV
            {(baoGomNghiViec ? "" : "WHERE nv.NgayNghiViec IS NULL")}
            ORDER BY dv.STT, nv.HoNV, nv.TenNV";
        return (await db.QueryAsync<TaiKhoanCap>(sql)).ToList();
    }

    // Văn thư cấp 1 (văn thư trường) còn làm việc — nhận bước "ban hành" của văn bản đi cấp trường.
    public async Task<List<short>> GetVanThuCap1Async()
    {
        using var db = Open();
        return (await db.QueryAsync<short>(
            $"SELECT nv.MaNV FROM NhanVien nv WHERE nv.NgayNghiViec IS NULL AND {SqlVanThuCap1}")).ToList();
    }
}
