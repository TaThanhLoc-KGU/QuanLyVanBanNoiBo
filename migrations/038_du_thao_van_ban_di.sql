-- 2026-09-20: DỰ THẢO VĂN BẢN ĐI — viên chức thường soạn dự thảo, trình lãnh đạo đơn vị, lãnh đạo duyệt hoặc
-- trả lại, văn thư ban hành (sinh số ký hiệu vào sổ văn bản đi). Chuỗi xử lý dùng lại bảng VanBan_XuLy
-- (LoaiVB=2, MSCV = ID dự thảo) của migration 037.

SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('DuThaoVanBanDi') IS NULL
BEGIN
    CREATE TABLE DuThaoVanBanDi (
        ID           int IDENTITY(1,1) PRIMARY KEY,
        TrichYeu     nvarchar(500) NOT NULL,
        MaLVB        smallint NULL,
        DoKhan       tinyint NOT NULL DEFAULT 0,      -- 0=Thường, 1=Khẩn, 2=Hỏa tốc
        MaNCV        smallint NULL,                    -- lĩnh vực / nhóm văn bản
        MaDVSoan     tinyint NOT NULL,
        MaNVSoan     smallint NOT NULL,
        HinhThuc     tinyint NOT NULL DEFAULT 1,      -- 1=Văn bản mới, 2=Thay thế, 3=Bổ sung
        HanXuLy      smalldatetime NULL,
        MaNVKy       smallint NULL,                    -- người ký dự kiến
        NoiDungXuLy  nvarchar(2000) NULL,
        DonViNhan    nvarchar(300) NULL,               -- csv MaDV nơi nhận nội bộ
        NoiNhanKhac  nvarchar(500) NULL,               -- nơi nhận ngoài trường (tự do)
        MSCVDen      nvarchar(20) NULL,                -- soạn để trả lời văn bản đến này
        TrangThai    tinyint NOT NULL DEFAULT 0,      -- 0=Nháp, 1=Đang xử lý (đã trình), 3=Đã ban hành
        NgayTao      datetime NOT NULL DEFAULT GETDATE(),
        NgayCapNhat  datetime NOT NULL DEFAULT GETDATE(),
        MSCVDi       nchar(10) NULL,                   -- số văn bản đi sinh ra khi ban hành
        NgayBanHanh  datetime NULL,
        MaNVBanHanh  smallint NULL
    );
    CREATE INDEX IX_DuThao_NguoiSoan ON DuThaoVanBanDi(MaNVSoan, TrangThai);
END
GO

IF OBJECT_ID('DuThao_File') IS NULL
BEGIN
    CREATE TABLE DuThao_File (
        ID        int IDENTITY(1,1) PRIMARY KEY,
        MaDT      int NOT NULL REFERENCES DuThaoVanBanDi(ID) ON DELETE CASCADE,
        TenFile   nvarchar(260) NOT NULL,
        DuongDan  nvarchar(500) NOT NULL,
        NgayTao   datetime NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_DuThao_File ON DuThao_File(MaDT);
END
GO
