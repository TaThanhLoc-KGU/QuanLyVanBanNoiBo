SET QUOTED_IDENTIFIER ON;
GO
-- Trình ký tuần tự nhiều cấp — mở rộng ký số đơn giản (Phase 10) sang 1 chuỗi duyệt có thứ tự
-- (VD: chuyên viên soạn -> lãnh đạo đơn vị duyệt -> BGH ký chính thức) trước khi văn bản được
-- đánh dấu Đã ký số. Áp dụng cho CongVanDi và VanBanDieuHanh (2 module đã có ký số đơn giản),
-- dùng chung LoaiVanBan 2/3 theo đúng quy ước đã có ở HoSoCongViec_VanBan.

IF OBJECT_ID('TrinhKy') IS NULL
BEGIN
    CREATE TABLE TrinhKy (
        ID         int IDENTITY(1,1) PRIMARY KEY,
        LoaiVanBan tinyint NOT NULL,
        MSCV       nchar(20) NOT NULL,
        TrangThai  tinyint NOT NULL DEFAULT 0,
        MaNVTrinh  smallint NOT NULL REFERENCES NhanVien(MaNV),
        NgayTrinh  datetime NOT NULL DEFAULT GETDATE(),
        GhiChu     nvarchar(500) NULL
    );
    CREATE INDEX IX_TrinhKy_VanBan ON TrinhKy (LoaiVanBan, MSCV);
END
GO

IF OBJECT_ID('TrinhKy_Cap') IS NULL
BEGIN
    CREATE TABLE TrinhKy_Cap (
        ID        int IDENTITY(1,1) PRIMARY KEY,
        TrinhKyID int NOT NULL REFERENCES TrinhKy(ID),
        ThuTu     tinyint NOT NULL,
        MaNVDuyet smallint NOT NULL REFERENCES NhanVien(MaNV),
        TrangThai tinyint NOT NULL DEFAULT 0,
        NgayXL    datetime NULL,
        GhiChu    nvarchar(500) NULL
    );
END
GO
