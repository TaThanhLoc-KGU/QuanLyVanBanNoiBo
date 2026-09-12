SET QUOTED_IDENTIFIER ON;
GO
-- Sổ văn bản theo năm + tự sinh số ký hiệu cho văn bản đi (thay thủ tục lấy số giấy).
-- Số thứ tự (STT) của CongVanDi vẫn tự tính theo (Sổ, Năm) như CongVanDen đang làm
-- (không cần bảng đếm riêng) — phần mới ở đây là:
--   1) Cờ DemRiengTheoLoai trên SoCV: mỗi Sổ tự chọn đếm số CHUNG cho mọi loại văn bản
--      (mặc định, giống hành vi hiện tại) hay đếm RIÊNG theo từng loại văn bản trong sổ đó
--      (VD Quyết định có dãy số 01,02.. độc lập với Thông báo 01,02..).
--   2) Bảng MauSoKyHieu: mẫu chuỗi số ký hiệu tự sinh, cấu hình theo (Sổ, Loại văn bản) —
--      cho phép "sổ công văn đi" và "sổ văn bản nội bộ" (sau này là văn bản điều hành)
--      có mẫu số khác nhau như đã thống nhất với người dùng.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SoCV') AND name = 'DemRiengTheoLoai')
BEGIN
    ALTER TABLE SoCV ADD DemRiengTheoLoai bit NOT NULL DEFAULT 0;
END
GO

IF OBJECT_ID('MauSoKyHieu') IS NULL
BEGIN
    CREATE TABLE MauSoKyHieu (
        ID        int IDENTITY PRIMARY KEY,
        MaSCV     tinyint NOT NULL,
        MaLVB     smallint NULL,     -- NULL = mẫu mặc định cho cả sổ khi loại văn bản không có mẫu riêng
        MauChuoi  nvarchar(200) NOT NULL,
        NgayTao   datetime NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_MauSoKyHieu_SoCV FOREIGN KEY (MaSCV) REFERENCES SoCV(MaSCV),
        CONSTRAINT FK_MauSoKyHieu_LoaiVB FOREIGN KEY (MaLVB) REFERENCES LoaiVB(MaLVB)
    );
END
GO

SET QUOTED_IDENTIFIER ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_MauSoKyHieu_Loai')
    CREATE UNIQUE INDEX UX_MauSoKyHieu_Loai ON MauSoKyHieu(MaSCV, MaLVB) WHERE MaLVB IS NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_MauSoKyHieu_MacDinh')
    CREATE UNIQUE INDEX UX_MauSoKyHieu_MacDinh ON MauSoKyHieu(MaSCV) WHERE MaLVB IS NULL;
GO

-- Mẫu placeholder hỗ trợ: {STT} {STT2} {STT3} (số thứ tự, có/không đệm 0) {NAM} {NAM2} (năm 4/2 số)
-- {KyHieu} (mã ký hiệu của Loại văn bản, VD "QĐ", "TB"). Xử lý thay thế ở tầng ứng dụng (C#).

-- Mẫu mặc định ban đầu cho "Sổ công văn" (MaSCV=1) để tính năng có sẵn ngay, chỉnh sửa lại
-- trong màn Quản trị hệ thống > Sổ văn bản nếu cần.
IF NOT EXISTS (SELECT 1 FROM MauSoKyHieu WHERE MaSCV=1 AND MaLVB IS NULL)
    INSERT INTO MauSoKyHieu (MaSCV, MaLVB, MauChuoi) VALUES (1, NULL, N'{STT}/{KyHieu}-VNKGU');
GO
