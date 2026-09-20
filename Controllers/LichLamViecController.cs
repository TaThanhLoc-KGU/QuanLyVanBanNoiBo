using Microsoft.AspNetCore.Mvc;
using CongVan.Models;
using CongVan.Services;
using ClosedXML.Excel;

namespace CongVan.Controllers;

// Lịch làm việc riêng của lãnh đạo (đơn vị + trường) — khác Lịch công tác (toàn trường, ai cũng
// thêm được). Chỉ người có "LichLamViec.Tao" mới tạo, và chỉ quản lý lịch của chính mình. Ở mức
// tổng quan (lưới tháng) chỉ hiện SỐ LƯỢNG lãnh đạo có lịch/ngày — không hiện tên, không hiện
// người kèm theo — bấm vào 1 ngày mới thấy chi tiết từng lãnh đạo + người kèm theo của họ.
public class LichLamViecController : BaseController
{
    private readonly DbService _db;
    private readonly IConfiguration _config;
    public LichLamViecController(DbService db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    // Đường dẫn feed .ics riêng của người đang đăng nhập — xem Program.cs (endpoint /calendar/*.ics)
    // và Services/IcsFeedService.cs. Trả về null nếu chưa cấu hình IcsFeed:SecretKey (tính năng tắt).
    private string? DuongDanFeedIcs(HttpRequest request)
    {
        var secretKey = IcsFeedService.LayKhoaHopLe(_config["IcsFeed:SecretKey"]);
        if (secretKey == null || MaNV <= 0) return null;
        var chuKy = IcsFeedService.TinhChuKy(MaNV, secretKey);
        return $"{request.Scheme}://{request.Host}/calendar/{MaNV}-{chuKy}.ics";
    }

    // LoaiLich=1 (lịch lãnh đạo): cần quyền LichLamViec.Tao.
    // LoaiLich=2 (lịch công tác — gộp từ module Lịch công tác cũ): ai cũng tạo được, như trước đây.
    private bool CoTheTao => CoQuyen("LichLamViec.Tao");
    private bool CoTheTaoLoai(byte loaiLich) => loaiLich == 2 || CoTheTao;
    private bool CoLienQuan(LichLamViec ll) => Quyen.Contains(0) || ll.MaNVLanhDao == MaNV || ll.MaNVTao == MaNV;

    public async Task<IActionResult> Index(int? nam, int? thang)
    {
        var today = DateTime.Today;
        int year = nam ?? today.Year;
        int month = thang ?? today.Month;
        var firstOfMonth = new DateTime(year, month, 1);
        var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);
        int leadingDays = (int)firstOfMonth.DayOfWeek == 0 ? 6 : (int)firstOfMonth.DayOfWeek - 1;
        var gridStart = firstOfMonth.AddDays(-leadingDays);
        int trailingDays = (int)lastOfMonth.DayOfWeek == 0 ? 0 : 7 - (int)lastOfMonth.DayOfWeek;
        var gridEnd = lastOfMonth.AddDays(trailingDays);

        var vm = new LichLamViecFilterViewModel
        {
            Nam = year,
            Thang = month,
            GridStart = gridStart,
            GridEnd = gridEnd,
            DanhSach = await _db.GetLichLamViecTheoKhoangNgayAsync(gridStart, gridEnd)
        };
        // Lịch nội bộ của đơn vị người dùng cũng hiện chung trên lưới (chỉ đọc) — yêu cầu "link
        // lịch nội bộ đơn vị với lịch làm việc". Bấm vào sẽ nhảy sang module Lịch nội bộ đơn vị.
        ViewBag.LichNoiBo = await _db.GetLichNoiBoTheoKhoangNgayAsync(gridStart, gridEnd, MaDV == 0 ? null : MaDV);
        ViewBag.CoTheTao = CoTheTao;
        // baoGomNghiViec:true — modal chi tiết ngày có thể hiện tháng cũ, người kèm theo lúc đó
        // có thể đã nghỉ việc, vẫn cần hiện đúng tên thay vì rơi về "NV#123".
        ViewBag.TatCaNhanVien = await _db.GetAllNhanVienAsync(baoGomNghiViec: true);
        ViewBag.LinkFeedIcs = DuongDanFeedIcs(Request);
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Nhap(int? id, byte loaiLich = 1)
    {
        bool isAdmin = Quyen.Contains(0);
        var vm = new LichLamViecFormViewModel
        {
            DanhSachNhanVien = await _db.GetAllNhanVienAsync(),
            DanhSachLanhDao = await _db.GetLanhDaoTruongVaDonViAsync(),
            DanhSachDonVi = await _db.GetDonViAsync(),
            DanhSachPhong = await _db.GetPhongHopAsync()
        };
        if (id.HasValue)
        {
            var ll = await _db.GetLichLamViecByIdAsync(id.Value);
            if (ll == null) return NotFound();
            if (!CoLienQuan(ll)) return Forbid();
            if (!CoTheTaoLoai(ll.LoaiLich)) return Forbid();
            vm.LichLamViec = ll;
            vm.DanhSachNguoiKemTheo = (ll.NguoiKemTheo ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(short.Parse).ToList();
        }
        else
        {
            if (!CoTheTaoLoai(loaiLich)) return Forbid();
            vm.LichLamViec = new LichLamViec
            {
                ThoiGianBatDau = DateTime.Today.AddHours(8), MaNVLanhDao = MaNV, LoaiSuKien = 1,
                LoaiLich = loaiLich, MaDV = loaiLich == 2 ? MaDV : null
            };
        }
        ViewBag.IsAdmin = isAdmin;
        ViewBag.CoTheTaoLichLanhDao = CoTheTao;
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Nhap(LichLamViecFormViewModel vm, List<short> nguoiKemTheo)
    {
        var ll = vm.LichLamViec;
        if (ll.LoaiLich != 2) ll.LoaiLich = 1;
        if (!CoTheTaoLoai(ll.LoaiLich)) return Forbid();
        bool isNew = ll.MaLich == 0;
        bool isAdmin = Quyen.Contains(0);

        if (!isNew)
        {
            var existing = await _db.GetLichLamViecByIdAsync(ll.MaLich);
            if (existing == null) return NotFound();
            if (!CoLienQuan(existing)) return Forbid();
        }

        if (ll.LoaiLich == 2)
        {
            // Lịch công tác: "chủ lịch" là người tạo, không có khái niệm lãnh đạo riêng.
            ll.MaNVLanhDao = isNew ? MaNV : ll.MaNVLanhDao == 0 ? MaNV : ll.MaNVLanhDao;
        }
        else
        {
            ll.MaDV = null; ll.MaPhong = null;
            // Chỉ admin mới được chọn lãnh đạo khác — người thường luôn tạo lịch của chính mình.
            if (!isAdmin) ll.MaNVLanhDao = MaNV;
            else if (ll.MaNVLanhDao == 0) ll.MaNVLanhDao = MaNV;
        }

        if (string.IsNullOrWhiteSpace(ll.TieuDe))
        {
            TempData["Error"] = "Tiêu đề không được để trống.";
            vm.DanhSachNhanVien = await _db.GetAllNhanVienAsync();
            vm.DanhSachLanhDao = await _db.GetLanhDaoTruongVaDonViAsync();
            vm.DanhSachDonVi = await _db.GetDonViAsync();
            vm.DanhSachPhong = await _db.GetPhongHopAsync();
            vm.DanhSachNguoiKemTheo = nguoiKemTheo;
            ViewBag.IsAdmin = isAdmin;
            ViewBag.CoTheTaoLichLanhDao = CoTheTao;
            return View(vm);
        }

        ll.NguoiKemTheo = nguoiKemTheo.Count > 0 ? string.Join(",", nguoiKemTheo.Distinct()) : null;

        if (isNew)
            ll.MaLich = await _db.ThemLichLamViecAsync(ll, MaNV);
        else
            await _db.SuaLichLamViecAsync(ll);

        TempData["Success"] = "Lưu thành công!";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(int maLich)
    {
        var ll = await _db.GetLichLamViecByIdAsync(maLich);
        if (ll == null) return NotFound();
        if (!CoLienQuan(ll)) return Forbid();

        await _db.XoaLichLamViecAsync(maLich);
        TempData["Success"] = "Đã xóa lịch làm việc.";
        return RedirectToAction("Index");
    }

    // ── Nhập hàng loạt từ Excel ────────────────────────────────────────────
    // Thứ tự cột: 1 Loại lịch(*) 2 Tiêu đề(*) 3 Nội dung 4 Ngày bắt đầu(*) 5 Giờ bắt đầu(*)
    // 6 Ngày kết thúc 7 Giờ kết thúc 8 Địa điểm 9 Loại sự kiện 10 Đơn vị (chỉ áp dụng khi Loại
    // lịch=Công tác — để trống = toàn trường) 11 Phòng họp 12 Người kèm theo (tên, cách nhau dấu ;)
    private const string TenLoaiLichLanhDao = "Lãnh đạo";
    private const string TenLoaiLichCongTac = "Công tác";
    private static readonly string[] TenLoaiSuKien = { "Họp", "Công tác", "Sự kiện", "Khác" };

    private static readonly string[] ExcelHeaders =
    {
        "Loại lịch (Lãnh đạo/Công tác) (*)", "Tiêu đề (*)", "Nội dung",
        "Ngày bắt đầu (dd/MM/yyyy) (*)", "Giờ bắt đầu (HH:mm) (*)",
        "Ngày kết thúc (dd/MM/yyyy)", "Giờ kết thúc (HH:mm)",
        "Địa điểm", "Loại sự kiện (Họp/Công tác/Sự kiện/Khác)",
        "Đơn vị (chỉ dùng cho Lịch công tác — để trống = toàn trường)",
        "Phòng họp", "Người kèm theo (tên, cách nhau dấu ;)"
    };

    [HttpGet]
    public async Task<IActionResult> TaiMauExcel()
    {
        var donVi = await _db.GetDonViAsync();
        var phong = await _db.GetPhongHopAsync();

        using var wb = new XLWorkbook();

        // ── Sheet "DanhMuc": nguồn cho ô chọn xổ xuống ──
        var dm = wb.Worksheets.Add("DanhMuc");
        dm.Cell(1, 1).Value = "Loại lịch"; dm.Cell(1, 2).Value = "Loại sự kiện";
        dm.Cell(1, 3).Value = "Đơn vị"; dm.Cell(1, 4).Value = "Phòng họp";
        dm.Row(1).Style.Font.Bold = true;
        dm.Cell(2, 1).Value = TenLoaiLichLanhDao; dm.Cell(3, 1).Value = TenLoaiLichCongTac;
        for (int i = 0; i < TenLoaiSuKien.Length; i++) dm.Cell(i + 2, 2).Value = TenLoaiSuKien[i];
        for (int i = 0; i < donVi.Count; i++) dm.Cell(i + 2, 3).Value = string.IsNullOrWhiteSpace(donVi[i].TenTat) ? donVi[i].TenDV : donVi[i].TenTat!.Trim();
        for (int i = 0; i < phong.Count; i++) dm.Cell(i + 2, 4).Value = phong[i].TenPhong;
        dm.Columns().AdjustToContents();

        void DatTen(string ten, int col, int count)
        {
            if (count <= 0) return;
            wb.DefinedNames.Add(ten, dm.Range(2, col, count + 1, col));
        }
        DatTen("DS_LoaiLich", 1, 2);
        DatTen("DS_LoaiSuKien", 2, TenLoaiSuKien.Length);
        DatTen("DS_DonViLich", 3, donVi.Count);
        DatTen("DS_PhongLich", 4, phong.Count);

        // ── Sheet mẫu nhập ──
        var ws = wb.Worksheets.Add("Mau nhap lich lam viec");
        for (int i = 0; i < ExcelHeaders.Length; i++) ws.Cell(1, i + 1).Value = ExcelHeaders[i];
        ws.Row(1).Style.Font.Bold = true;
        ws.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F5FF");
        ws.SheetView.FreezeRows(1);

        ws.Cell(2, 1).Value = TenLoaiLichCongTac;
        ws.Cell(2, 2).Value = "Ví dụ: Họp giao ban đầu tuần";
        ws.Cell(2, 4).Value = DateTime.Today.ToString("dd/MM/yyyy");
        ws.Cell(2, 5).Value = "08:00";
        ws.Cell(2, 7).Value = "09:30";
        ws.Cell(2, 9).Value = TenLoaiSuKien[0];

        void AddList(int col, string tenVung, int count)
        {
            if (count <= 0) return;
            var v = ws.Range(2, col, 2000, col).CreateDataValidation();
            v.List("=" + tenVung, true);
            v.IgnoreBlanks = true;
        }
        AddList(1, "DS_LoaiLich", 2);
        AddList(9, "DS_LoaiSuKien", TenLoaiSuKien.Length);
        AddList(10, "DS_DonViLich", donVi.Count);
        AddList(11, "DS_PhongLich", phong.Count);

        ws.Columns().AdjustToContents();
        ws.Column(2).Width = 35; ws.Column(3).Width = 40; ws.Column(12).Width = 30;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Mau_NhapLichLamViec.xlsx");
    }

    [HttpPost]
    public async Task<IActionResult> NhapExcel(IFormFile? file)
    {
        var ketQua = new KetQuaNhapExcelViewModel();
        if (file == null || file.Length == 0)
        {
            ketQua.Loi.Add("Vui lòng chọn file Excel để nhập.");
            return View("KetQuaNhapExcel", ketQua);
        }

        bool isAdmin = Quyen.Contains(0);
        var donViList = await _db.GetDonViAsync(chiLayConHoatDong: false);
        var phongList = await _db.GetPhongHopAsync(chiHienThi: false);

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ketQua.Loi.Add("Chỉ nhận file Excel định dạng .xlsx (tải file mẫu ở nút \"Tải file mẫu\").");
            return View("KetQuaNhapExcel", ketQua);
        }

        XLWorkbook wb;
        try { wb = new XLWorkbook(file.OpenReadStream()); }
        catch
        {
            ketQua.Loi.Add("Không đọc được file — file hỏng hoặc không phải Excel .xlsx hợp lệ.");
            return View("KetQuaNhapExcel", ketQua);
        }
        using var _wb = wb;
        var ws = wb.Worksheets.FirstOrDefault(w => !w.Name.Equals("DanhMuc", StringComparison.OrdinalIgnoreCase));
        if (ws == null)
        {
            ketQua.Loi.Add("File không có sheet dữ liệu. Hãy dùng đúng file mẫu.");
            return View("KetQuaNhapExcel", ketQua);
        }

        foreach (var row in ws.RowsUsed().Skip(1))
        {
            int dong = row.RowNumber(); // số dòng THẬT trong Excel (không lệch khi có dòng trống xen giữa)
            var loaiLichTx = row.Cell(1).GetString().Trim();
            var tieuDe = row.Cell(2).GetString().Trim();
            var noiDung = row.Cell(3).GetString().Trim();
            var ngayBD = DocNgayExcel(row.Cell(4));
            var gioBDTx = DocGioExcel(row.Cell(5));
            var ngayKT = DocNgayExcel(row.Cell(6));
            var gioKTTx = DocGioExcel(row.Cell(7));
            var diaDiem = row.Cell(8).GetString().Trim();
            var loaiSuKienTx = row.Cell(9).GetString().Trim();
            var donViTx = row.Cell(10).GetString().Trim();
            var phongTx = row.Cell(11).GetString().Trim();
            var nguoiKemTx = row.Cell(12).GetString().Trim();

            if (string.IsNullOrWhiteSpace(tieuDe) && string.IsNullOrWhiteSpace(loaiLichTx) && ngayBD == null)
                continue; // dòng trống hoàn toàn, bỏ qua âm thầm
            if (tieuDe.StartsWith("Ví dụ:", StringComparison.OrdinalIgnoreCase))
                continue; // dòng ví dụ của file mẫu — không nhập nhầm vào lịch thật

            if (string.IsNullOrWhiteSpace(tieuDe))
            {
                ketQua.Loi.Add($"Dòng {dong}: thiếu Tiêu đề, đã bỏ qua.");
                continue;
            }
            if (!ngayBD.HasValue)
            {
                ketQua.Loi.Add($"Dòng {dong}: Ngày bắt đầu trống hoặc sai định dạng (dd/MM/yyyy), đã bỏ qua.");
                continue;
            }
            if (!TimeSpan.TryParse(gioBDTx, out var gioBD))
            {
                ketQua.Loi.Add($"Dòng {dong}: Giờ bắt đầu trống hoặc sai định dạng (HH:mm), đã bỏ qua.");
                continue;
            }

            byte loaiLich;
            if (string.Equals(loaiLichTx, TenLoaiLichCongTac, StringComparison.OrdinalIgnoreCase)) loaiLich = 2;
            else if (string.Equals(loaiLichTx, TenLoaiLichLanhDao, StringComparison.OrdinalIgnoreCase)) loaiLich = 1;
            else
            {
                ketQua.Loi.Add($"Dòng {dong}: Loại lịch \"{loaiLichTx}\" không hợp lệ (chỉ nhận \"{TenLoaiLichLanhDao}\" hoặc \"{TenLoaiLichCongTac}\"), đã bỏ qua.");
                continue;
            }
            if (!CoTheTaoLoai(loaiLich))
            {
                ketQua.Loi.Add($"Dòng {dong}: bạn không có quyền tạo Lịch lãnh đạo, đã bỏ qua (chỉ tạo được Lịch công tác).");
                continue;
            }

            var thoiGianBatDau = ngayBD.Value.Date + gioBD;
            DateTime? thoiGianKetThuc = null;
            if (TimeSpan.TryParse(gioKTTx, out var gioKT))
                thoiGianKetThuc = (ngayKT ?? ngayBD).Value.Date + gioKT;
            else if (ngayKT.HasValue)
                thoiGianKetThuc = ngayKT.Value.Date + gioBD;

            byte loaiSuKien = (byte)(Array.FindIndex(TenLoaiSuKien, t => string.Equals(t, loaiSuKienTx, StringComparison.OrdinalIgnoreCase)) + 1);
            if (loaiSuKien == 0) loaiSuKien = 4; // "Khác" nếu không khớp/để trống

            byte? maDV = null;
            if (loaiLich == 2 && !string.IsNullOrWhiteSpace(donViTx))
            {
                var dv = donViList.FirstOrDefault(d =>
                    string.Equals(d.TenDV?.Trim(), donViTx, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(d.TenTat?.Trim(), donViTx, StringComparison.OrdinalIgnoreCase));
                if (dv != null) maDV = dv.MaDV;
                else ketQua.Loi.Add($"Dòng {dong}: không nhận ra đơn vị \"{donViTx}\" — để trống (toàn trường).");
            }

            byte? maPhong = null;
            if (!string.IsNullOrWhiteSpace(phongTx))
            {
                var ph = phongList.FirstOrDefault(p => string.Equals(p.TenPhong?.Trim(), phongTx, StringComparison.OrdinalIgnoreCase));
                if (ph != null) maPhong = ph.MaPhong;
                else ketQua.Loi.Add($"Dòng {dong}: không nhận ra phòng họp \"{phongTx}\" — bỏ qua chọn phòng.");
            }

            var nguoiKemTheo = new List<short>();
            foreach (var ten in nguoiKemTx.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = ten.Trim();
                if (t.Length == 0) continue;
                var maNVKem = await _db.FindNhanVienTheoTenAsync(t);
                if (maNVKem.HasValue) { if (!nguoiKemTheo.Contains(maNVKem.Value)) nguoiKemTheo.Add(maNVKem.Value); }
                else ketQua.Loi.Add($"Dòng {dong}: không tìm thấy người kèm theo \"{t}\" — bỏ qua người này.");
            }

            try
            {
                if (maPhong.HasValue)
                {
                    var trung = await _db.GetTrungPhongAsync(maPhong.Value, thoiGianBatDau, thoiGianKetThuc ?? thoiGianBatDau.AddHours(1));
                    if (trung.Count > 0)
                    {
                        ketQua.Loi.Add($"Dòng {dong}: phòng \"{phongTx}\" đã có lịch trùng giờ (\"{trung[0].TieuDe}\"), đã bỏ qua chọn phòng.");
                        maPhong = null;
                    }
                }

                var ll = new LichLamViec
                {
                    TieuDe = tieuDe,
                    NoiDung = string.IsNullOrWhiteSpace(noiDung) ? null : noiDung,
                    ThoiGianBatDau = thoiGianBatDau,
                    ThoiGianKetThuc = thoiGianKetThuc,
                    DiaDiem = string.IsNullOrWhiteSpace(diaDiem) ? null : diaDiem,
                    LoaiSuKien = loaiSuKien,
                    LoaiLich = loaiLich,
                    MaDV = loaiLich == 2 ? maDV : null,
                    MaPhong = maPhong,
                    // Lịch công tác: chủ lịch = người nhập. Lịch lãnh đạo: người thường luôn là
                    // lịch của chính mình qua Excel (không nhập hộ người khác) — admin cũng vậy để
                    // tránh nhầm lẫn hàng loạt, muốn tạo hộ người khác thì dùng form Nhập từng cái.
                    MaNVLanhDao = MaNV,
                    NguoiKemTheo = nguoiKemTheo.Count > 0 ? string.Join(",", nguoiKemTheo) : null
                };
                await _db.ThemLichLamViecAsync(ll, MaNV);
                ketQua.SoDongThanhCong++;
            }
            catch (Exception ex)
            {
                ketQua.Loi.Add($"Dòng {dong}: lỗi khi lưu — {ex.Message}");
            }
        }

        return View("KetQuaNhapExcel", ketQua);
    }

    // Ô giờ trong Excel thường là kiểu thời gian thật (GetString trả "8:00:00 AM" tùy định dạng ô →
    // TimeSpan.TryParse hỏng) — đọc thẳng giá trị, chỉ rơi về đọc chữ nếu ô là text "08:00".
    private static string DocGioExcel(IXLCell cell)
    {
        if (cell.TryGetValue(out TimeSpan ts)) return ts.ToString(@"hh\:mm");
        if (cell.TryGetValue(out DateTime dt)) return dt.ToString("HH:mm");
        return cell.GetString().Trim();
    }

    private static DateTime? DocNgayExcel(IXLCell cell)
    {
        if (cell.TryGetValue(out DateTime dt)) return dt;
        var s = cell.GetString().Trim();
        if (string.IsNullOrEmpty(s)) return null;
        if (DateTime.TryParseExact(s, new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" },
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed))
            return parsed;
        if (DateTime.TryParse(s, System.Globalization.CultureInfo.GetCultureInfo("vi-VN"),
                System.Globalization.DateTimeStyles.None, out var parsed2))
            return parsed2;
        return null;
    }
}
