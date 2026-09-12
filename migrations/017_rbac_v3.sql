SET QUOTED_IDENTIFIER ON;
GO
-- RBAC v3 — xây dựng lại phân quyền chi tiết áp dụng cho TOÀN BỘ hệ thống (không chỉ trang Admin
-- như Phase 8). Thay đổi cốt lõi: BaseController.CoQuyen() KHÔNG còn tự động cho quyền 99 (văn thư)
-- đi qua mọi cổng kiểm tra nữa — chỉ quyền 0 (admin) còn là "siêu người dùng". Quyền 99/12 giờ đây
-- phải có mặt trong 1 Vai trò (VaiTro) được cấp đúng mã chức năng thì mới thao tác được — để không
-- phá vỡ hành vi hiện tại, migration này AUTO gán mọi người đang giữ quyền 99/12/MaNV_TDV vào đúng
-- Vai trò tương ứng kèm đầy đủ quyền mặc định tương đương những gì họ đang làm được hôm nay.
--
-- 4 vai trò mặc định theo yêu cầu người dùng:
--   - "Văn thư"         : văn thư trung tâm, tương đương quyền 99 cũ (toàn quyền văn bản + admin).
--   - "Văn thư đơn vị"  : MỚI — không có quyền toàn cục nào cả, quyền của họ hoàn toàn đến từ bảng
--                         DonVi_VanThu (được gán riêng cho TỪNG đơn vị ở trang Lãnh đạo phòng) —
--                         chỉ thao tác được văn bản đến của (các) đơn vị họ được gán.
--                         Vì không có bảng gán sẵn từ trước nên bắt đầu rỗng, admin gán thủ công.
--   - "Lãnh đạo đơn vị" : tương ứng DonVi.MaNV_TDV (đã có sẵn từ trước, dùng ở Phase 11) — quyền
--                         theo-đơn-vị của họ vẫn đi qua check MaNV_TDV trực tiếp trong code, vai
--                         trò này chỉ mang thêm 1-2 quyền toàn cục không cần theo đơn vị.
--   - "Lãnh đạo trường" : tương đương quyền 12 (BGH) cũ.
-- Phần "Văn phòng điện tử" — Phase 12 (phân quyền siêu chi tiết, theo yêu cầu người dùng).

IF OBJECT_ID('DonVi_VanThu') IS NULL
BEGIN
    CREATE TABLE DonVi_VanThu (
        MaDV tinyint  NOT NULL,
        MaNV smallint NOT NULL,
        PRIMARY KEY (MaDV, MaNV),
        CONSTRAINT FK_DVVanThu_DV FOREIGN KEY (MaDV) REFERENCES DonVi(MaDV),
        CONSTRAINT FK_DVVanThu_NV FOREIGN KEY (MaNV) REFERENCES NhanVien(MaNV)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM VaiTro WHERE TenVaiTro = N'Văn thư')
    INSERT INTO VaiTro (TenVaiTro, GhiChu) VALUES (N'Văn thư', N'Văn thư trung tâm — xử lý toàn bộ văn bản đến/đi/điều hành toàn trường (tương đương quyền 99 cũ)');
IF NOT EXISTS (SELECT 1 FROM VaiTro WHERE TenVaiTro = N'Văn thư đơn vị')
    INSERT INTO VaiTro (TenVaiTro, GhiChu) VALUES (N'Văn thư đơn vị', N'Người phụ trách xử lý văn bản đến của riêng đơn vị mình — gán theo từng đơn vị ở trang Lãnh đạo phòng, không thấy đơn vị khác');
IF NOT EXISTS (SELECT 1 FROM VaiTro WHERE TenVaiTro = N'Lãnh đạo đơn vị')
    INSERT INTO VaiTro (TenVaiTro, GhiChu) VALUES (N'Lãnh đạo đơn vị', N'Trưởng đơn vị/phòng ban — gán qua trường "Lãnh đạo phụ trách" của từng đơn vị');
IF NOT EXISTS (SELECT 1 FROM VaiTro WHERE TenVaiTro = N'Lãnh đạo trường')
    INSERT INTO VaiTro (TenVaiTro, GhiChu) VALUES (N'Lãnh đạo trường', N'Ban Giám Hiệu (tương đương quyền 12 cũ)');
GO

DECLARE @vtVanThu INT = (SELECT MaVaiTro FROM VaiTro WHERE TenVaiTro = N'Văn thư');
DECLARE @vtLanhDaoDV INT = (SELECT MaVaiTro FROM VaiTro WHERE TenVaiTro = N'Lãnh đạo đơn vị');
DECLARE @vtLanhDaoTruong INT = (SELECT MaVaiTro FROM VaiTro WHERE TenVaiTro = N'Lãnh đạo trường');

INSERT INTO VaiTro_Quyen (MaVaiTro, MaChucNang)
SELECT @vtVanThu, x.MaChucNang FROM (VALUES
    ('Global.XemToanTruong'),('CongVanDen.Nhap'),('CongVanDen.Xoa'),('CongVanDen.GuiEmail'),
    ('CongVanDen.XacNhanHoanThanh'),('CongVanDen.XuLyDV'),('CongVanDen.ChuyenXuLy'),
    ('CongVanDen.ChiDaoPhanCong'),('CongVanDen.XinYKienBGH'),('CongVanDen.TraLai'),
    ('CongVanDi.Nhap'),('CongVanDi.Xoa'),('CongVanDi.GuiEmail'),('CongVanDi.KySo'),('CongVanDi.Excel'),
    ('VanBanDieuHanh.Nhap'),('VanBanDieuHanh.Xoa'),('VanBanDieuHanh.KySo'),('VanBanDieuHanh.Excel'),
    ('CongViec.QuanLyTatCa'),('HoSoCongViec.QuanLyTatCa'),('LichCongTac.QuanLyTatCa'),('ThongBao.Dang'),
    ('Admin.NhanVien'),('Admin.PhanQuyen'),('Admin.LanhDaoPhong'),('Admin.BanGiamHieu'),
    ('Admin.DonVi'),('Admin.CoQuan'),('Admin.LoaiVanBan'),('Admin.SoVanBan'),
    ('Admin.CauHinhEmail'),('Admin.LuongXuLy')
) AS x(MaChucNang)
WHERE NOT EXISTS (SELECT 1 FROM VaiTro_Quyen vq WHERE vq.MaVaiTro=@vtVanThu AND vq.MaChucNang=x.MaChucNang);

INSERT INTO VaiTro_Quyen (MaVaiTro, MaChucNang)
SELECT @vtLanhDaoTruong, x.MaChucNang FROM (VALUES
    ('Global.XemToanTruong'),('CongVanDen.ChiDaoPhanCong'),('CongVanDen.TraLai'),
    ('CongVanDen.BGHTraLoiYKien'),('LichCongTac.ToanTruong'),('ThongBao.Dang'),('LichLamViec.Tao')
) AS x(MaChucNang)
WHERE NOT EXISTS (SELECT 1 FROM VaiTro_Quyen vq WHERE vq.MaVaiTro=@vtLanhDaoTruong AND vq.MaChucNang=x.MaChucNang);

IF NOT EXISTS (SELECT 1 FROM VaiTro_Quyen WHERE MaVaiTro=@vtLanhDaoDV AND MaChucNang='LichLamViec.Tao')
    INSERT INTO VaiTro_Quyen (MaVaiTro, MaChucNang) VALUES (@vtLanhDaoDV, 'LichLamViec.Tao');
GO

-- Auto-gán người đang giữ quyền/vai trò tương ứng — để không ai mất quyền đang có khi rollout.
DECLARE @vtVanThu2 INT = (SELECT MaVaiTro FROM VaiTro WHERE TenVaiTro = N'Văn thư');
DECLARE @vtLanhDaoDV2 INT = (SELECT MaVaiTro FROM VaiTro WHERE TenVaiTro = N'Lãnh đạo đơn vị');
DECLARE @vtLanhDaoTruong2 INT = (SELECT MaVaiTro FROM VaiTro WHERE TenVaiTro = N'Lãnh đạo trường');

INSERT INTO NhanVien_VaiTro (MaNV, MaVaiTro)
SELECT DISTINCT pq.MaNV, @vtVanThu2 FROM PhanQuyen pq
WHERE pq.MaQuyen = 99 AND NOT EXISTS (SELECT 1 FROM NhanVien_VaiTro nvt WHERE nvt.MaNV=pq.MaNV AND nvt.MaVaiTro=@vtVanThu2);

INSERT INTO NhanVien_VaiTro (MaNV, MaVaiTro)
SELECT DISTINCT pq.MaNV, @vtLanhDaoTruong2 FROM PhanQuyen pq
WHERE pq.MaQuyen = 12 AND NOT EXISTS (SELECT 1 FROM NhanVien_VaiTro nvt WHERE nvt.MaNV=pq.MaNV AND nvt.MaVaiTro=@vtLanhDaoTruong2);

-- Một vài DonVi.MaNV_TDV là dữ liệu cũ trỏ tới MaNV=0 ("chưa gán") hoặc 1 MaNV đã không còn tồn
-- tại trong NhanVien (nhân sự cũ) — lọc bỏ để tránh vỡ FK, không phải lỗi của migration này.
INSERT INTO NhanVien_VaiTro (MaNV, MaVaiTro)
SELECT DISTINCT dv.MaNV_TDV, @vtLanhDaoDV2 FROM DonVi dv
WHERE dv.MaNV_TDV IS NOT NULL AND dv.MaNV_TDV > 0
  AND EXISTS (SELECT 1 FROM NhanVien nv WHERE nv.MaNV = dv.MaNV_TDV)
  AND NOT EXISTS (SELECT 1 FROM NhanVien_VaiTro nvt WHERE nvt.MaNV=dv.MaNV_TDV AND nvt.MaVaiTro=@vtLanhDaoDV2);
GO
