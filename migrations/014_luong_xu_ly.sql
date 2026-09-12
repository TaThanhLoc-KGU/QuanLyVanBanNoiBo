SET QUOTED_IDENTIFIER ON;
GO
-- Luồng xử lý: Admin cấu hình trước các cặp (đơn vị nguồn → đơn vị đích) được phép chuyển tiếp
-- xử lý văn bản đến. Hành động "Chuyển xử lý" tạo 1 dòng CongVanDenXuLyDV mới cho đơn vị đích
-- (chủ trì mới) và đánh dấu dòng cũ TrangThai=4 (Đã chuyển tiếp — không phải Hoàn thành, không
-- tính là "xong" nhưng cũng không còn chặn tự-đóng-văn-bản vì check hiện có là TrangThai<3).
-- Phần "Văn phòng điện tử" — Phase 9/10.

IF OBJECT_ID('LuongXuLy') IS NULL
BEGIN
    CREATE TABLE LuongXuLy (
        ID         int IDENTITY(1,1) PRIMARY KEY,
        MaDVNguon  tinyint NOT NULL,
        MaDVDich   tinyint NOT NULL,
        GhiChu     nvarchar(500) NULL,
        NgayTao    smalldatetime NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_LuongXuLy_Nguon FOREIGN KEY (MaDVNguon) REFERENCES DonVi(MaDV),
        CONSTRAINT FK_LuongXuLy_Dich  FOREIGN KEY (MaDVDich)  REFERENCES DonVi(MaDV),
        CONSTRAINT UQ_LuongXuLy UNIQUE (MaDVNguon, MaDVDich)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CongVanDenXuLyDV') AND name = 'ChuyenToiMaDV')
BEGIN
    ALTER TABLE CongVanDenXuLyDV ADD ChuyenToiMaDV tinyint NULL;
END
GO
