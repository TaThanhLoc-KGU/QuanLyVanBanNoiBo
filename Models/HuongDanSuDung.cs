namespace CongVan.Models;

// Nội dung "Hướng dẫn sử dụng" theo từng trang — tra theo (Controller, Action), hiện qua nút ở
// header (xem _Layout.cshtml + Views/Shared/_HuongDan.cshtml). Định nghĩa trong code (không phải
// DB) để dev sửa/thêm nhanh cùng lúc với sửa tính năng, giống cách ChucNangDangKy đăng ký quyền.
// Chưa có mục cho trang nào thì nút vẫn hiện, chỉ hiện thông báo "chưa có hướng dẫn riêng".
public static class HuongDanSuDung
{
    public record MucHuongDan(string TieuDe, string NoiDungHtml);

    // Key: "Controller/Action" (không phân biệt hoa thường), Action rỗng nghĩa là áp dụng cho MỌI
    // action của controller đó nếu không có key riêng khớp chính xác hơn.
    private static readonly Dictionary<string, MucHuongDan> Mục = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CongVanDen/Index"] = new("Sổ văn bản đến",
            """
            <ul>
              <li><b>Tìm kiếm</b> ở ô "Tìm kiếm" quét cả trích yếu, số văn bản, cơ quan ban hành — và cả
                  <b>nội dung chữ bên trong file PDF đính kèm</b> (kể cả bản scan, hệ thống tự nhận diện chữ
                  khi upload). Không cần biết văn bản nào có đính kèm, cứ gõ từ khóa là tìm được.</li>
              <li><b>Đơn vị xử lý / Người ký / Ngày đến từ-đến</b>: lọc thêm, kết hợp được với ô tìm kiếm.</li>
              <li>Danh sách dài được <b>chia trang</b> (mặc định 30 văn bản/trang) — dùng thanh điều hướng
                  cuối bảng để chuyển trang, hoặc gõ thẳng số trang vào ô số.</li>
              <li>Icon <i class="bi bi-eye-fill"></i>/<i class="bi bi-eye-slash"></i> cạnh tên đơn vị xử lý cho biết
                  đơn vị đó ĐÃ/CHƯA mở xem văn bản — khác với chấm "Chưa xem" (đó là bạn chưa xem).</li>
              <li>Bấm vào dòng "Trạng thái" để đánh dấu đã xem nhanh mà không cần vào chi tiết.</li>
            </ul>
            """),
        ["CongVanDen/ChiTiet"] = new("Chi tiết văn bản đến",
            """
            <ul>
              <li>Thanh tiến trình đầu trang (Tiếp nhận → Đang xử lý → Hoàn thành) cho biết văn bản đang ở
                  đâu — màu xanh dương = đang xử lý, đỏ = quá hạn, xanh lá = xong.</li>
              <li>Nếu văn bản đang chờ <b>đơn vị của bạn</b> xử lý, sẽ có thanh <b>"Việc của đơn vị bạn"</b>
                  ngay dưới thanh tiến trình — bấm nút ở đó luôn, không cần kéo xuống bảng.</li>
              <li>Mở trang này lần đầu khi đơn vị bạn còn "Chưa tiếp nhận" sẽ <b>tự động chuyển sang "Đã tiếp
                  nhận"</b> — muốn báo đang xử lý thật thì vẫn phải tự bấm nút "Đang xử lý".</li>
              <li>Bảng "Tình trạng liên thông đơn vị": dòng nào <b>tô vàng có dấu !</b> là đơn vị đó (mà bạn
                  có quyền xử lý) còn việc cần làm — không phải dò từng dòng.</li>
              <li><b>"Chuyển xử lý"</b>: chỉ đơn vị chủ trì mới chuyển được cho đơn vị khác — đơn vị nhận sẽ
                  có thông báo ở chuông đầu trang.</li>
              <li>Cột "File" trong bảng xử lý: bấm vào số để xem/tải từng file kết quả đã upload (upload
                  được nhiều file cùng lúc, không giới hạn 1 file như trước).</li>
              <li>"Luồng xử lý theo vai trò": dành cho lãnh đạo đơn vị — chỉ đạo & phân công cho 1 chuyên
                  viên cụ thể, hoặc xin ý kiến BGH nếu cần cấp cao hơn quyết định.</li>
            </ul>
            """),
        ["Notification/GetBadge"] = new("Chuông thông báo",
            """
            <ul>
              <li>Mục <b>"Việc mới giao / chuyển đến"</b>: báo khi có văn bản mới giao cho đơn vị bạn, hoặc
                  được đơn vị khác chuyển xử lý đến — bấm vào để mở thẳng văn bản đó.</li>
              <li>Mục <b>"Văn bản chưa xem"</b>: liệt kê văn bản năm nay đơn vị bạn chưa mở xem lần nào.</li>
              <li>"Đánh dấu đã xem" ở mục thông báo chỉ ẩn các thông báo cũ, không ảnh hưởng gì tới trạng
                  thái xử lý thật của văn bản.</li>
            </ul>
            """),
        ["DonVi/PhanQuyenNoiBo"] = new("Phân quyền nội bộ đơn vị",
            """
            <ul>
              <li>Trang này chỉ dành cho <b>lãnh đạo phụ trách đơn vị</b> (hoặc Admin) — tự quản lý ai trong
                  đơn vị mình là "văn thư đơn vị", không cần xin Admin trung tâm mỗi lần đổi người.</li>
              <li>"Văn thư đơn vị" là người được phép tiếp nhận/cập nhật trạng thái/upload file cho các văn
                  bản đến giao cho đơn vị này — chọn/bỏ chọn trong ô bên dưới, lưu tự động ngay khi đổi.</li>
              <li>Chỉ quản lý được đơn vị của chính bạn — không thấy/sửa được đơn vị khác.</li>
            </ul>
            """),
        ["LichNoiBoDonVi/Index"] = new("Lịch nội bộ đơn vị",
            """
            <ul>
              <li>Lịch dùng chung cho <b>cả đơn vị</b> — khác lịch cá nhân của lãnh đạo và lịch công tác
                  toàn trường. Ai trong đơn vị cũng xem và tạo được.</li>
              <li>Bấm "Thêm lịch mới" để tạo — sự kiện luôn thuộc về đơn vị của người tạo, không tạo hộ
                  đơn vị khác được (trừ Admin).</li>
              <li>Ngày có nhiều hơn 3 sự kiện sẽ hiện "+N khác" — bấm vào để xem đầy đủ trong ngày đó.</li>
            </ul>
            """),
        ["VanBanNoiBo/Index"] = new("Văn bản nội bộ đơn vị",
            """
            <ul>
              <li>Sổ văn bản <b>riêng của đơn vị bạn</b> — tách hẳn khỏi sổ văn bản đến/đi toàn trường,
                  đơn vị tự quản lý hoàn toàn.</li>
              <li><b>Tự sinh số hiệu</b>: vào "Thêm văn bản mới", bấm nút "+" cạnh ô "Loại văn bản" để tạo 1
                  loại (vd "QĐ" = Quyết định) kèm mẫu số, vd <code>{STT2}/QĐ-TÊN_ĐƠN_VỊ</code>. Lần sau chọn
                  đúng loại đó, số hiệu tự nhảy (01, 02, 03...) theo từng năm.</li>
              <li>Đã cấu hình <b>API riêng theo đơn vị</b> — nếu đơn vị có web riêng muốn tự động lấy/gửi văn
                  bản qua lại, liên hệ Admin để được cấp API Key (mục "API Key đơn vị" trong Quản trị hệ
                  thống). Chi tiết kỹ thuật xem file <code>API_VANBANNOIBO.md</code> trong mã nguồn.</li>
            </ul>
            """),
        ["Admin/PhanQuyen"] = new("Phân quyền (toàn trường)",
            """
            <ul>
              <li>3 khối độc lập trên trang này: <b>Vai trò</b> (1 vai trò = 1 tập chức năng, gán cho nhiều
                  người), <b>Vai trò được gán</b> (chọn 1 nhân viên rồi tick vai trò họ có), và <b>Quyền cũ
                  (legacy)</b> bên dưới — 3 hệ thống riêng, không phải trùng lặp.</li>
              <li>Mỗi công tắc bật là <b>lưu ngay lập tức</b>, không có nút "Lưu" tổng — tắt/bật xong là có
                  hiệu lực.</li>
              <li>Dùng ô "Tìm chức năng theo tên..." trong mỗi vai trò để lọc nhanh thay vì kéo qua hết ~40
                  chức năng. Số "N/M" cạnh mỗi nhóm cho biết đã cấp bao nhiêu trong nhóm đó.</li>
              <li>Đổi vai trò/quyền của 1 người đang đăng nhập chỉ có hiệu lực sau khi họ <b>đăng nhập
                  lại</b> (quyền được nạp vào phiên đăng nhập lúc vào hệ thống).</li>
            </ul>
            """),
    };

    public static MucHuongDan? Tra(string controller, string action)
    {
        if (Mục.TryGetValue($"{controller}/{action}", out var muc)) return muc;
        if (Mục.TryGetValue($"{controller}/", out var mucChung)) return mucChung;
        return null;
    }
}
