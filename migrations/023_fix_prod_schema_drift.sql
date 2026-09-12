SET QUOTED_IDENTIFIER ON;
GO
-- Vá lệch schema giữa bản dữ liệu thật (db_thuc_te.sql, export ~21/8/2026) và code hiện tại. Các
-- bảng "gốc" (CongVanDen, CongVanDi, DonVi, CoQuan, LoaiVB, LoaiVBCVDi, CongVanDenXuLyDV,
-- CVDen_DaXem, CVDenFile, EmailConfig...) có từ trước khi dự án dùng migration script (xem
-- DOCKER.md) — DEV DB đã có đúng default constraint/kiểu dữ liệu qua nhiều lần chỉnh tay ngoài
-- migration trong suốt quá trình phát triển, nhưng bản dump thật lại thiếu các thay đổi đó. Phát
-- hiện bằng cách so sánh trực tiếp sys.columns/sys.default_constraints giữa DEV và PROD (2026-08-22).
-- Toàn bộ đã kiểm tra dữ liệu hiện có an toàn để đổi (không mất dữ liệu, không tràn phạm vi).

-- 1. STT: float -> int (không có dữ liệu thập phân trên cả 2 bảng, an toàn đổi)
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CongVanDen') AND name='STT' AND system_type_id=TYPE_ID('float'))
    ALTER TABLE CongVanDen ALTER COLUMN STT int NOT NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CongVanDi') AND name='STT' AND system_type_id=TYPE_ID('float'))
    ALTER TABLE CongVanDi ALTER COLUMN STT int NOT NULL;
GO

-- 2. CongVanDi.MaLDKy: cho phép NULL (khớp dev, không có dòng nào NULL nên nới lỏng an toàn)
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('CongVanDi') AND name='MaLDKy' AND is_nullable=0)
    ALTER TABLE CongVanDi ALTER COLUMN MaLDKy smallint NULL;
GO

-- 3. DonVi.NgayGiaiThe: datetime -> smalldatetime (khớp dev; dữ liệu hiện có trong phạm vi an toàn)
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('DonVi') AND name='NgayGiaiThe' AND system_type_id=TYPE_ID('datetime'))
    ALTER TABLE DonVi ALTER COLUMN NgayGiaiThe smalldatetime NULL;
GO

-- 4. LoaiVBCVDi: thiếu hẳn cột HienThi
IF COL_LENGTH('LoaiVBCVDi', 'HienThi') IS NULL
    ALTER TABLE LoaiVBCVDi ADD HienThi bit NOT NULL DEFAULT 1;
GO

-- 5. Các DEFAULT constraint bị thiếu — INSERT bỏ qua cột (dựa vào default) sẽ lỗi nếu thiếu
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID('CongVanDenXuLyDV') AND parent_column_id=(SELECT column_id FROM sys.columns WHERE object_id=OBJECT_ID('CongVanDenXuLyDV') AND name='LoaiDV'))
    ALTER TABLE CongVanDenXuLyDV ADD CONSTRAINT DF_CongVanDenXuLyDV_LoaiDV DEFAULT 1 FOR LoaiDV;
GO
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID('CongVanDenXuLyDV') AND parent_column_id=(SELECT column_id FROM sys.columns WHERE object_id=OBJECT_ID('CongVanDenXuLyDV') AND name='TrangThai'))
    ALTER TABLE CongVanDenXuLyDV ADD CONSTRAINT DF_CongVanDenXuLyDV_TrangThai DEFAULT 0 FOR TrangThai;
GO
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID('CoQuan') AND parent_column_id=(SELECT column_id FROM sys.columns WHERE object_id=OBJECT_ID('CoQuan') AND name='HienThi'))
    ALTER TABLE CoQuan ADD CONSTRAINT DF_CoQuan_HienThi DEFAULT 1 FOR HienThi;
GO
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID('CVDen_DaXem') AND parent_column_id=(SELECT column_id FROM sys.columns WHERE object_id=OBJECT_ID('CVDen_DaXem') AND name='NgayXem'))
    ALTER TABLE CVDen_DaXem ADD CONSTRAINT DF_CVDen_DaXem_NgayXem DEFAULT GETDATE() FOR NgayXem;
GO
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID('CVDenFile') AND parent_column_id=(SELECT column_id FROM sys.columns WHERE object_id=OBJECT_ID('CVDenFile') AND name='NgayUpload'))
BEGIN
    ALTER TABLE CVDenFile ALTER COLUMN NgayUpload datetime NULL;
    ALTER TABLE CVDenFile ADD CONSTRAINT DF_CVDenFile_NgayUpload DEFAULT GETDATE() FOR NgayUpload;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID('LoaiVB') AND parent_column_id=(SELECT column_id FROM sys.columns WHERE object_id=OBJECT_ID('LoaiVB') AND name='HienThi'))
    ALTER TABLE LoaiVB ADD CONSTRAINT DF_LoaiVB_HienThi DEFAULT 1 FOR HienThi;
GO

-- 6. EmailConfig: hàng loạt default bị thiếu
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID('EmailConfig') AND parent_column_id=(SELECT column_id FROM sys.columns WHERE object_id=OBJECT_ID('EmailConfig') AND name='IsActive'))
    ALTER TABLE EmailConfig ADD CONSTRAINT DF_EmailConfig_IsActive DEFAULT 0 FOR IsActive;
GO
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID('EmailConfig') AND parent_column_id=(SELECT column_id FROM sys.columns WHERE object_id=OBJECT_ID('EmailConfig') AND name='SenderEmail'))
    ALTER TABLE EmailConfig ADD CONSTRAINT DF_EmailConfig_SenderEmail DEFAULT '' FOR SenderEmail;
GO
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID('EmailConfig') AND parent_column_id=(SELECT column_id FROM sys.columns WHERE object_id=OBJECT_ID('EmailConfig') AND name='SenderName'))
    ALTER TABLE EmailConfig ADD CONSTRAINT DF_EmailConfig_SenderName DEFAULT N'Hệ thống Quản lý Văn bản VNKGU' FOR SenderName;
GO
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID('EmailConfig') AND parent_column_id=(SELECT column_id FROM sys.columns WHERE object_id=OBJECT_ID('EmailConfig') AND name='SmtpHost'))
    ALTER TABLE EmailConfig ADD CONSTRAINT DF_EmailConfig_SmtpHost DEFAULT '' FOR SmtpHost;
GO
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID('EmailConfig') AND parent_column_id=(SELECT column_id FROM sys.columns WHERE object_id=OBJECT_ID('EmailConfig') AND name='SmtpPassword'))
    ALTER TABLE EmailConfig ADD CONSTRAINT DF_EmailConfig_SmtpPassword DEFAULT '' FOR SmtpPassword;
GO
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID('EmailConfig') AND parent_column_id=(SELECT column_id FROM sys.columns WHERE object_id=OBJECT_ID('EmailConfig') AND name='SmtpPort'))
    ALTER TABLE EmailConfig ADD CONSTRAINT DF_EmailConfig_SmtpPort DEFAULT 587 FOR SmtpPort;
GO
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID('EmailConfig') AND parent_column_id=(SELECT column_id FROM sys.columns WHERE object_id=OBJECT_ID('EmailConfig') AND name='SmtpUser'))
    ALTER TABLE EmailConfig ADD CONSTRAINT DF_EmailConfig_SmtpUser DEFAULT '' FOR SmtpUser;
GO
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID('EmailConfig') AND parent_column_id=(SELECT column_id FROM sys.columns WHERE object_id=OBJECT_ID('EmailConfig') AND name='UpdatedAt'))
    ALTER TABLE EmailConfig ADD CONSTRAINT DF_EmailConfig_UpdatedAt DEFAULT GETDATE() FOR UpdatedAt;
GO
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID('EmailConfig') AND parent_column_id=(SELECT column_id FROM sys.columns WHERE object_id=OBJECT_ID('EmailConfig') AND name='UseSSL'))
    ALTER TABLE EmailConfig ADD CONSTRAINT DF_EmailConfig_UseSSL DEFAULT 1 FOR UseSSL;
GO
