-- 2026-09-20: HỘP THƯ CÁ NHÂN — quy trình xử lý văn bản theo TỪNG NGƯỜI (mô phỏng VNPT iOffice của Tỉnh):
-- văn bản được chuyển cho từng người với vai trò Xử lý chính / Đồng xử lý (cho ý kiến) / Đồng gửi (xem để
-- biết), người nhận chuyển tiếp / trả lại / kết thúc xử lý; mọi bước đều có nhật ký.
-- Kèm cờ "Dùng chung" cho văn bản đến: viên chức thường chỉ thấy văn bản đến nếu được chuyển cho họ hoặc
-- văn bản đó là văn bản dùng chung nội bộ (quyết định, thông báo, biên bản...).

SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('VanBan_XuLy') IS NULL
BEGIN
    CREATE TABLE VanBan_XuLy (
        ID          int IDENTITY(1,1) PRIMARY KEY,
        LoaiVB      tinyint NOT NULL,             -- 1=Văn bản đến, 2=Dự thảo văn bản đi
        MSCV        nvarchar(20) NOT NULL,
        MaNVNhan    smallint NOT NULL,
        MaNVGui     smallint NULL,                -- NULL = hệ thống/văn thư vào sổ
        VaiTro      tinyint NOT NULL,             -- 1=Xử lý chính, 2=Đồng xử lý (cho ý kiến), 3=Đồng gửi (xem để biết)
        TrangThai   tinyint NOT NULL DEFAULT 0,   -- 0=Chờ xử lý, 1=Đang xử lý (đã mở), 2=Đã chuyển tiếp/cho ý kiến, 3=Đã kết thúc, 4=Đã trả lại, 5=Bị thu hồi
        NoiDungGui  nvarchar(1000) NULL,          -- ý kiến chỉ đạo của người chuyển
        YKien       nvarchar(2000) NULL,          -- ý kiến / kết quả xử lý của người nhận
        HanXuLy     smalldatetime NULL,
        NgayGui     datetime NOT NULL DEFAULT GETDATE(),
        NgayXem     datetime NULL,
        NgayXuLy    datetime NULL,
        IDCha       int NULL                      -- dòng đã sinh ra dòng này (chuỗi chuyển tiếp)
    );
    CREATE INDEX IX_VanBan_XuLy_Nhan ON VanBan_XuLy(MaNVNhan, TrangThai, LoaiVB);
    CREATE INDEX IX_VanBan_XuLy_VB   ON VanBan_XuLy(LoaiVB, MSCV);
END
GO

IF OBJECT_ID('VanBan_NhatKy') IS NULL
BEGIN
    CREATE TABLE VanBan_NhatKy (
        ID          int IDENTITY(1,1) PRIMARY KEY,
        LoaiVB      tinyint NOT NULL,
        MSCV        nvarchar(20) NOT NULL,
        MaNV        smallint NOT NULL,            -- người thao tác
        HanhDong    nvarchar(30) NOT NULL,        -- ChuyenXuLy, ChuyenTiep, YKien, TraLai, KetThuc, ThuHoi
        NoiDung     nvarchar(2000) NULL,
        MaNVLienQuan smallint NULL,               -- người nhận / người bị trả lại
        Ngay        datetime NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_VanBan_NhatKy_VB ON VanBan_NhatKy(LoaiVB, MSCV, Ngay);
END
GO

IF COL_LENGTH('LoaiVB', 'DungChung') IS NULL
    ALTER TABLE LoaiVB ADD DungChung bit NOT NULL CONSTRAINT DF_LoaiVB_DungChung DEFAULT 0;
GO
IF COL_LENGTH('CongVanDen', 'DungChung') IS NULL
    ALTER TABLE CongVanDen ADD DungChung bit NOT NULL CONSTRAINT DF_CongVanDen_DungChung DEFAULT 0;
GO

-- Mặc định các loại văn bản mang tính phổ biến nội bộ được đánh dấu dùng chung (admin chỉnh lại được):
-- Quyết định, Biên bản, Thông báo, Kế hoạch, Điều lệ, Thông tư, Nghị quyết, Hướng dẫn, Kết luận,
-- Chương trình, Quy chế, Quy chế phối hợp.
UPDATE LoaiVB SET DungChung = 1 WHERE MaLVB IN (2,4,6,7,8,9,14,15,24,26,34,36) AND DungChung = 0;
GO
