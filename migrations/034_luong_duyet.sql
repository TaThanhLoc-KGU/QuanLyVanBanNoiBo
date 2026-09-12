-- 2026-09-06: CẤU HÌNH LUỒNG DUYỆT VĂN BẢN (trình ký) — admin định nghĩa sẵn 1 chuỗi bước duyệt
-- có thứ tự (VD: Trình lãnh đạo đơn vị -> Văn thư trường kiểm tra thể thức -> BGH ký), mỗi bước
-- chỉ rõ AI duyệt (theo chức danh/quyền/người cụ thể) và có cho "Trả lại" bước trước hay không.
-- Khi trình ký, chọn 1 luồng -> hệ thống tự sinh các cấp duyệt từ mẫu này.
--
-- Dùng lại bảng TrinhKy / TrinhKy_Cap sẵn có (LoaiVanBan 2=Văn bản đi, 3=Văn bản điều hành),
-- chỉ bổ sung cột + thêm bảng mẫu + nhật ký.

SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('LuongDuyet') IS NULL
BEGIN
    CREATE TABLE LuongDuyet (
        ID         int IDENTITY(1,1) PRIMARY KEY,
        Ten        nvarchar(200) NOT NULL,
        LoaiVanBan tinyint NOT NULL,           -- 2=Văn bản đi, 3=Văn bản điều hành
        MoTa       nvarchar(500) NULL,
        MacDinh    bit NOT NULL DEFAULT 0,     -- luồng gợi ý sẵn cho loại văn bản này
        HienThi    bit NOT NULL DEFAULT 1,
        NgayTao    datetime NOT NULL DEFAULT GETDATE()
    );
END
GO

IF OBJECT_ID('LuongDuyet_Buoc') IS NULL
BEGIN
    CREATE TABLE LuongDuyet_Buoc (
        ID             int IDENTITY(1,1) PRIMARY KEY,
        LuongID        int NOT NULL REFERENCES LuongDuyet(ID),
        ThuTu          tinyint NOT NULL,
        TenBuoc        nvarchar(200) NOT NULL,
        -- 1=Người trình (người soạn), 2=Lãnh đạo đơn vị của người trình,
        -- 3=Người có chức năng (GiaTri = mã chức năng), 4=Người thuộc vai trò (GiaTri = ID vai trò),
        -- 5=Người cụ thể (GiaTri = MaNV), 6=Ban Giám Hiệu (quyền 12)
        LoaiNguoiDuyet tinyint NOT NULL,
        GiaTri         nvarchar(100) NULL,
        ChoPhepTraLai  bit NOT NULL DEFAULT 1,
        LaBuocKy       bit NOT NULL DEFAULT 0  -- bước ký chính thức (người duyệt tải file đã ký lên)
    );
    CREATE INDEX IX_LuongDuyetBuoc_Luong ON LuongDuyet_Buoc(LuongID, ThuTu);
END
GO

IF COL_LENGTH('TrinhKy', 'LuongID') IS NULL
    ALTER TABLE TrinhKy ADD LuongID int NULL;
GO

IF COL_LENGTH('TrinhKy_Cap', 'TenBuoc') IS NULL
    ALTER TABLE TrinhKy_Cap ADD TenBuoc nvarchar(200) NULL;
GO
IF COL_LENGTH('TrinhKy_Cap', 'ChoPhepTraLai') IS NULL
    ALTER TABLE TrinhKy_Cap ADD ChoPhepTraLai bit NOT NULL DEFAULT 1;
GO
IF COL_LENGTH('TrinhKy_Cap', 'LaBuocKy') IS NULL
    ALTER TABLE TrinhKy_Cap ADD LaBuocKy bit NOT NULL DEFAULT 0;
GO
-- Lưu lại loại/giá trị người duyệt của bước để controller cho phép BẤT KỲ người khớp điều kiện
-- bước đó cùng xử lý (không cứng nhắc 1 MaNVDuyet đại diện).
IF COL_LENGTH('TrinhKy_Cap', 'LoaiNguoiDuyet') IS NULL
    ALTER TABLE TrinhKy_Cap ADD LoaiNguoiDuyet tinyint NULL, GiaTriNguoiDuyet nvarchar(100) NULL;
GO

-- TrangThai TrinhKy_Cap: 0=Chờ,1=Đã duyệt,2=Từ chối(hủy chuỗi),3=Đã ký,4=Đã trả lại(chờ bước trước làm lại)
IF OBJECT_ID('TrinhKy_NhatKy') IS NULL
BEGIN
    CREATE TABLE TrinhKy_NhatKy (
        ID         int IDENTITY(1,1) PRIMARY KEY,
        TrinhKyID  int NOT NULL REFERENCES TrinhKy(ID),
        CapID      int NULL,
        HanhDong   nvarchar(30) NOT NULL,     -- 'trinh','duyet','tra_lai','tu_choi','ky','hoan_tat'
        MaNV       smallint NOT NULL,
        NoiDung    nvarchar(500) NULL,
        NgayTao    datetime NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_TrinhKyNhatKy ON TrinhKy_NhatKy(TrinhKyID, NgayTao);
END
GO

-- ── Seed: 1 luồng mẫu cho VĂN BẢN ĐI khớp mô tả của người dùng ──
IF NOT EXISTS (SELECT 1 FROM LuongDuyet WHERE LoaiVanBan = 2)
BEGIN
    DECLARE @lid int;
    INSERT INTO LuongDuyet (Ten, LoaiVanBan, MoTa, MacDinh, HienThi)
    VALUES (N'Trình ký văn bản đi (đơn vị → văn thư trường → BGH)', 2,
            N'Văn thư đơn vị soạn → trình lãnh đạo đơn vị → văn thư trường kiểm tra thể thức → BGH ký', 1, 1);
    SET @lid = SCOPE_IDENTITY();

    INSERT INTO LuongDuyet_Buoc (LuongID, ThuTu, TenBuoc, LoaiNguoiDuyet, GiaTri, ChoPhepTraLai, LaBuocKy) VALUES
    (@lid, 1, N'Lãnh đạo đơn vị duyệt',              2, NULL,               1, 0),
    (@lid, 2, N'Văn thư trường kiểm tra thể thức',   3, N'CongVanDi.KySo',  1, 0),
    (@lid, 3, N'Ban Giám Hiệu ký',                   6, NULL,               1, 1);
END
GO
