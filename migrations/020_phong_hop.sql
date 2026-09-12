SET QUOTED_IDENTIFIER ON;
GO
-- Đăng ký phòng họp: danh mục phòng (quản lý ở Admin) + liên kết tùy chọn từ Lịch công tác
-- (LichCongTac.MaPhong) — 1 sự kiện "Họp" có thể chọn phòng, hệ thống cảnh báo/chặn trùng lịch
-- cùng phòng thay vì xây 1 hệ đặt phòng song song riêng biệt.

IF OBJECT_ID('PhongHop') IS NULL
BEGIN
    CREATE TABLE PhongHop (
        MaPhong   tinyint IDENTITY(1,1) PRIMARY KEY,
        TenPhong  nvarchar(200) NOT NULL,
        ViTri     nvarchar(255) NULL,
        SucChua   smallint NULL,
        GhiChu    nvarchar(500) NULL,
        HienThi   bit NOT NULL DEFAULT 1
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LichCongTac') AND name = 'MaPhong')
BEGIN
    ALTER TABLE LichCongTac ADD MaPhong tinyint NULL REFERENCES PhongHop(MaPhong);
END
GO

IF NOT EXISTS (SELECT 1 FROM VaiTro_Quyen WHERE MaVaiTro = 2 AND MaChucNang = 'Admin.PhongHop')
    INSERT INTO VaiTro_Quyen (MaVaiTro, MaChucNang) VALUES (2, 'Admin.PhongHop');
GO
