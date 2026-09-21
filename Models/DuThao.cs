namespace CongVan.Models;

// Dự thảo văn bản đi do viên chức soạn — xem migrations/038_du_thao_van_ban_di.sql.
public class DuThao
{
    public const byte Nhap = 0, DangXuLy = 1, DaBanHanh = 3;

    public int ID { get; set; }
    public string TrichYeu { get; set; } = "";
    public short? MaLVB { get; set; }
    public byte DoKhan { get; set; }
    public short? MaNCV { get; set; }
    public byte MaDVSoan { get; set; }
    public short MaNVSoan { get; set; }
    public byte HinhThuc { get; set; } = 1;
    public DateTime? HanXuLy { get; set; }
    public short? MaNVKy { get; set; }
    public string? NoiDungXuLy { get; set; }
    public string? DonViNhan { get; set; }
    public string? NoiNhanKhac { get; set; }
    public string? MSCVDen { get; set; }
    public byte TrangThai { get; set; }
    public DateTime NgayTao { get; set; }
    public DateTime NgayCapNhat { get; set; }
    public string? MSCVDi { get; set; }
    public DateTime? NgayBanHanh { get; set; }
    public short? MaNVBanHanh { get; set; }
    public byte PhamVi { get; set; } = 1;          // 1 = Cấp trường (văn thư cấp 1 ban hành), 2 = Nội bộ đơn vị (văn thư cấp 2 ban hành)
    public string? ChuoiDuyet { get; set; }        // csv MaNV còn phải duyệt/ký theo thứ tự
    public int? MaVBNoiBo { get; set; }            // văn bản nội bộ đơn vị sinh ra khi ban hành PhamVi=2
    public short BuocIdx { get; set; } = -1;       // -1 = ở người soạn; 0..n-1 = bước duyệt thứ i; n = chờ văn thư ban hành

    public const string BuocVanThu1 = "VT1"; // bước "Văn thư cấp 1 kiểm tra thể thức" trong chuỗi
    public List<string> ChuoiBuoc => (ChuoiDuyet ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
    public bool DaKhoaCauHinh => TrangThai == DangXuLy && BuocIdx >= 0; // đã trình: không đổi phạm vi/người ký/chuỗi nữa
    public string TenPhamVi => PhamVi == 2 ? "Nội bộ đơn vị" : "Cấp trường";

    // Hiển thị
    public string? TenLVB { get; set; }
    public string? TenNCV { get; set; }
    public string? TenDVSoan { get; set; }
    public string? TenNVSoan { get; set; }
    public string? TenNVKy { get; set; }
    public string? TrichYeuDen { get; set; }

    public string TenDoKhan => DoKhan switch { 1 => "Khẩn", 2 => "Hỏa tốc", _ => "Thường" };
    public string TenHinhThuc => HinhThuc switch { 2 => "Thay thế", 3 => "Bổ sung", _ => "Văn bản mới" };
    public string TenTrangThai => TrangThai switch { 0 => "Nháp", 1 => "Đang xử lý", 3 => "Đã ban hành", _ => "—" };
}

public class DuThaoFile
{
    public int ID { get; set; }
    public int MaDT { get; set; }
    public string TenFile { get; set; } = "";
    public string DuongDan { get; set; } = "";
    public DateTime NgayTao { get; set; }
}

public class DuThaoFormViewModel
{
    public DuThao DuThao { get; set; } = new();
    public List<LoaiVB> DanhSachLoaiVB { get; set; } = new();
    public List<NhomCV> DanhSachNhomCV { get; set; } = new();
    public List<DonVi> DanhSachDonVi { get; set; } = new();
    public List<NhanVien> DanhSachNguoiKy { get; set; } = new();
    public List<DuThaoFile> Files { get; set; } = new();
    public List<byte> DVNhan { get; set; } = new();
    public string TenDVSoan { get; set; } = "";
    public List<NhanVien> DanhSachKyTruong { get; set; } = new();   // lãnh đạo trường (người ký văn bản cấp trường)
    public NhanVien? LanhDaoDonVi { get; set; }                     // lãnh đạo đơn vị soạn (người duyệt/ký văn bản nội bộ)
    public bool TacGiaLaLanhDaoDonVi { get; set; }
    public bool Khoa { get; set; }                                   // đã trình: khóa phạm vi/người ký
    public bool NguoiSuaLaTacGia { get; set; } = true;
    public bool LaVanThuCap1 { get; set; }
    public bool LaVanThuCap2 { get; set; }
}

public class DuThaoChiTietViewModel
{
    public DuThao DuThao { get; set; } = new();
    public List<DuThaoFile> Files { get; set; } = new();
    public XuLyPanelViewModel Panel { get; set; } = new();
    public bool LaNguoiSoan { get; set; }
    public bool CoTheTrinh { get; set; }
    public bool CoTheBanHanh { get; set; }
    public string TenLanhDaoDonVi { get; set; } = "";
    public string BuocHienTai { get; set; } = "soan"; // soan | duyet | banhanh | xong
    public List<string> TenDVNhan { get; set; } = new();
    public bool CoTheBanHanhNgay { get; set; }
    public bool CoTheDuyet { get; set; }
    public bool CoTheSua { get; set; }
    public bool CoTheTraLai { get; set; }
    public string NhanNutDuyet { get; set; } = "Duyệt & chuyển tiếp";
    public string MoTaBuocHienTai { get; set; } = "";
    public List<FlowNode> Flow { get; set; } = new();
    public string LinkBanHanhController { get; set; } = "CongVanDi";
    public string TenVanThu { get; set; } = "Văn thư";
    public string CapVanThu { get; set; } = "cấp 1";
    public List<string> TenChuoiConLai { get; set; } = new();
    public string TenNguoiDangGiu { get; set; } = "";
}

public class FlowNode
{
    public string Ten { get; set; } = "";
    public string Phu { get; set; } = "";
    public string Icon { get; set; } = "person-check";
    public string Trang { get; set; } = ""; // "" | on | done
}
