namespace CongVan.Models;

public class LoginViewModel
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public class ChangePasswordViewModel
{
    public string MatKhauCu { get; set; } = "";
    public string Email { get; set; } = "";
    public string MatKhauMoi { get; set; } = "";
    public string NhapLaiMatKhau { get; set; } = "";
}

// Thông tin phân trang dùng chung cho mọi danh sách — page 1-based.
public class PageInfo
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 30;
    public int TotalCount { get; set; }
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class CongVanDenFilterViewModel
{
    public int? Nam { get; set; }
    public byte? MaSCV { get; set; }
    public string? TuKhoa { get; set; }
    public byte? MaDVXL { get; set; }
    public string? NguoiKy { get; set; }
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
    public PageInfo Trang { get; set; } = new();
    public List<CongVanDen> DanhSach { get; set; } = new();
    public List<SoCV> DanhSachSoCV { get; set; } = new();
    public List<DonVi> DanhSachDonVi { get; set; } = new();
}

// ViewModel gọn cho 5 danh sách con (DangXuLy/DangXuLyTre/HoanThanhDungHan/HoanThanhTre/ChuaHoanThanh)
// — đã lọc sẵn theo trạng thái nên chỉ cần phân trang, không cần bộ lọc đầy đủ như Index.
public class CongVanDenListViewModel
{
    public PageInfo Trang { get; set; } = new();
    public List<CongVanDen> DanhSach { get; set; } = new();
}

public class CongVanDiFilterViewModel
{
    public int? Nam { get; set; }
    public byte? MaSCV { get; set; }
    public string? TuKhoa { get; set; }
    public bool ChiCongKhai { get; set; }
    public List<CongVanDi> DanhSach { get; set; } = new();
    public List<SoCV> DanhSachSoCV { get; set; } = new();
    public List<DonVi> DanhSachDonVi { get; set; } = new();
}

public class CongVanDenFormViewModel
{
    public CongVanDen CongVanDen { get; set; } = new();
    public List<SoCV> DanhSachSoCV { get; set; } = new();
    public List<CoQuan> DanhSachCoQuan { get; set; } = new();
    public List<LoaiVB> DanhSachLoaiVB { get; set; } = new();
    public List<NhomCV> DanhSachNhomCV { get; set; } = new();
    public List<DonVi> DanhSachDonVi { get; set; } = new();
    public List<NhanVien> DanhSachLanhDao { get; set; } = new();
    public List<byte> DanhSachDVPhoiHop { get; set; } = new();
    public List<CVDenFile> DanhSachFileDinhKem { get; set; } = new();
}

public class CongVanDiFormViewModel
{
    public CongVanDi CongVanDi { get; set; } = new();
    public List<SoCV> DanhSachSoCV { get; set; } = new();
    public List<LoaiVB> DanhSachLoaiVB { get; set; } = new();
    public List<NhomCV> DanhSachNhomCV { get; set; } = new();
    public List<NhanVien> DanhSachLanhDao { get; set; } = new();
    public List<DonVi> DanhSachDonVi { get; set; } = new();
    public List<byte> DanhSachDVNhan { get; set; } = new();
}

public class KetQuaNhapExcelViewModel
{
    public int SoDongThanhCong { get; set; }
    public List<string> Loi { get; set; } = new();
}

public class VanBanDieuHanhFilterViewModel
{
    public int? Nam { get; set; }
    public byte? MaSCV { get; set; }
    public string? TuKhoa { get; set; }
    public List<VanBanDieuHanh> DanhSach { get; set; } = new();
    public List<SoCV> DanhSachSoCV { get; set; } = new();
}

public class VanBanDieuHanhFormViewModel
{
    public VanBanDieuHanh VanBanDieuHanh { get; set; } = new();
    public List<SoCV> DanhSachSoCV { get; set; } = new();
    public List<LoaiVB> DanhSachLoaiVB { get; set; } = new();
    public List<NhanVien> DanhSachLanhDao { get; set; } = new();
}

public class GiaoDVXLViewModel
{
    public CongVanDen CongVanDen { get; set; } = new();
    public List<NhanVien> DanhSachNhanVien { get; set; } = new();
    public List<CVDen_QTXL> LichSuXuLy { get; set; } = new();
    public short MaNVChuTri { get; set; }
    public short MaNVPhoiHop { get; set; }
    public DateTime? NgayYCHT { get; set; }
}

public class CongViecFilterViewModel
{
    public byte? TrangThai { get; set; }
    public byte? MaDV { get; set; }
    public string? TuKhoa { get; set; }
    public bool ChiCuaToi { get; set; }
    public List<CongViec> DanhSach { get; set; } = new();
    public List<DonVi> DanhSachDonVi { get; set; } = new();
}

public class CongViecFormViewModel
{
    public CongViec CongViec { get; set; } = new();
    public List<NhanVien> DanhSachNhanVien { get; set; } = new();
    public List<short> DanhSachNVPhoiHop { get; set; } = new();
}

public class CongViecChiTietViewModel
{
    public CongViec CongViec { get; set; } = new();
    public List<CongViecNhatKy> NhatKy { get; set; } = new();
    public List<CongViecFile> DanhSachFile { get; set; } = new();
    public List<string> TenNVPhoiHop { get; set; } = new();
    public string? TieuDeVanBanGoc { get; set; }
    public string? UrlVanBanGoc { get; set; }
    public List<CongViecBinhLuan> BinhLuan { get; set; } = new();
}

public class HoSoCongViecFilterViewModel
{
    public byte? TrangThai { get; set; }
    public byte? MaDV { get; set; }
    public string? TuKhoa { get; set; }
    public List<HoSoCongViec> DanhSach { get; set; } = new();
    public List<DonVi> DanhSachDonVi { get; set; } = new();
}

public class HoSoCongViecFormViewModel
{
    public HoSoCongViec HoSoCongViec { get; set; } = new();
    public List<NhanVien> DanhSachNhanVien { get; set; } = new();
}

public class HoSoCongViecChiTietViewModel
{
    public HoSoCongViec HoSoCongViec { get; set; } = new();
    public List<CongViec> CongViecLienQuan { get; set; } = new();
    public List<HoSoCongViecVanBan> VanBanLienQuan { get; set; } = new();
    public List<HoSoCongViecFile> DanhSachFile { get; set; } = new();
    public List<CongViec> DanhSachCongViecCoTheGan { get; set; } = new();
}

public class LichCongTacFilterViewModel
{
    public int Nam { get; set; }
    public int Thang { get; set; }
    public byte? MaDV { get; set; }
    public DateTime GridStart { get; set; }
    public DateTime GridEnd { get; set; }
    public List<LichCongTac> DanhSach { get; set; } = new();
    public List<DonVi> DanhSachDonVi { get; set; } = new();
}

public class LichCongTacFormViewModel
{
    public LichCongTac LichCongTac { get; set; } = new();
    public List<DonVi> DanhSachDonVi { get; set; } = new();
    public List<NhanVien> DanhSachNhanVien { get; set; } = new();
    public List<short> DanhSachNguoiThamGia { get; set; } = new();
    public List<PhongHop> DanhSachPhong { get; set; } = new();
}

public class LichLamViecFilterViewModel
{
    public int Nam { get; set; }
    public int Thang { get; set; }
    public DateTime GridStart { get; set; }
    public DateTime GridEnd { get; set; }
    public List<LichLamViec> DanhSach { get; set; } = new();
}

public class LichLamViecFormViewModel
{
    public LichLamViec LichLamViec { get; set; } = new();
    public List<NhanVien> DanhSachNhanVien { get; set; } = new();   // cho "Người kèm theo" — mọi viên chức
    public List<NhanVien> DanhSachLanhDao { get; set; } = new();    // cho ô chọn "Lãnh đạo" — chỉ lãnh đạo
    public List<DonVi> DanhSachDonVi { get; set; } = new();         // cho lịch công tác (LoaiLich=2)
    public List<PhongHop> DanhSachPhong { get; set; } = new();
    public List<short> DanhSachNguoiKemTheo { get; set; } = new();
}

public class LichNoiBoDonViFilterViewModel
{
    public int Nam { get; set; }
    public int Thang { get; set; }
    public byte MaDV { get; set; }
    public DateTime GridStart { get; set; }
    public DateTime GridEnd { get; set; }
    public List<LichNoiBoDonVi> DanhSach { get; set; } = new();
}

public class LichNoiBoDonViFormViewModel
{
    public LichNoiBoDonVi LichNoiBoDonVi { get; set; } = new();
}

public class VanBanNoiBoFilterViewModel
{
    public int? Nam { get; set; }
    public string? TuKhoa { get; set; }
    public byte MaDV { get; set; }
    public string? TenDV { get; set; }
    public List<VanBanNoiBo> DanhSach { get; set; } = new();
}

public class VanBanNoiBoFormViewModel
{
    public VanBanNoiBo VanBanNoiBo { get; set; } = new();
    public List<NhanVien> DanhSachNhanVien { get; set; } = new();
    public List<VanBanNoiBoFile> DanhSachFile { get; set; } = new();
    public List<VanBanNoiBoMauSo> DanhSachMauSo { get; set; } = new();
}

public class DonViApiKeyFormViewModel
{
    public List<DonViApiKey> DanhSach { get; set; } = new();
    public List<DonVi> DanhSachDonVi { get; set; } = new();
    public string? KeyMoiTao { get; set; } // key thật plaintext — chỉ có giá trị ngay sau khi tạo, hiện 1 lần rồi thôi
}

public class ThongBaoNoiBoFilterViewModel
{
    public byte? MucDo { get; set; }
    public List<ThongBaoNoiBo> DanhSach { get; set; } = new();
}

public class ThongBaoNoiBoFormViewModel
{
    public ThongBaoNoiBo ThongBaoNoiBo { get; set; } = new();
    public List<DonVi> DanhSachDonVi { get; set; } = new();
    public List<QuyenXL> DanhSachQuyen { get; set; } = new();
    public List<byte> DanhSachDVNhan { get; set; } = new();
    public List<byte> DanhSachQuyenNhan { get; set; } = new();
}

