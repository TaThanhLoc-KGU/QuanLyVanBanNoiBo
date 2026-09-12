SET QUOTED_IDENTIFIER ON;
GO
-- Ký số: theo hướng "tải file cần ký về → ký bằng phần mềm VGCA/VNPT-CA sẵn có trên máy văn thư
-- → tải file đã ký lên lại hệ thống" (không gọi trực tiếp API nội bộ của phần mềm ký vì không có
-- tài liệu kỹ thuật xác thực cho từng phiên bản — làm vậy để chắc chắn hoạt động được thật, có
-- thể nâng cấp lên ký trực tiếp trong trình duyệt sau nếu có tài liệu SDK chính xác từ nhà cung cấp).
-- Áp dụng cho Văn bản đi và Văn bản điều hành. Phần "Văn phòng điện tử" — Phase 10/10.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CongVanDi') AND name = 'DaKySo')
BEGIN
    ALTER TABLE CongVanDi ADD
        DaKySo        bit NOT NULL DEFAULT 0,
        NgayKySo      smalldatetime NULL,
        MaNVKySo      smallint NULL,
        LoaiChungThu  nvarchar(50) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('VanBanDieuHanh') AND name = 'DaKySo')
BEGIN
    ALTER TABLE VanBanDieuHanh ADD
        DaKySo        bit NOT NULL DEFAULT 0,
        NgayKySo      smalldatetime NULL,
        MaNVKySo      smallint NULL,
        LoaiChungThu  nvarchar(50) NULL;
END
GO
