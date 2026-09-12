-- 2026-08-27: "Dấu công khai" cho văn bản đi — đánh dấu văn bản nào được phép hiển thị ra ngoài
-- qua API công khai (api/v1/vanban-cong-khai) để các trang web công khai khác đọc.
-- CongKhai = 0 (mặc định, KHÔNG lộ) / 1 (công khai). NgayCongKhai + MaNVCongKhai để truy vết.

IF COL_LENGTH('CongVanDi', 'CongKhai') IS NULL
    ALTER TABLE CongVanDi ADD CongKhai bit NOT NULL CONSTRAINT DF_CongVanDi_CongKhai DEFAULT 0;
GO

IF COL_LENGTH('CongVanDi', 'NgayCongKhai') IS NULL
    ALTER TABLE CongVanDi ADD NgayCongKhai datetime NULL;
GO

IF COL_LENGTH('CongVanDi', 'MaNVCongKhai') IS NULL
    ALTER TABLE CongVanDi ADD MaNVCongKhai smallint NULL;
GO

-- Truy vấn API công khai luôn lọc theo CongKhai=1 + sắp theo ngày — index đỡ quét bảng.
-- (filtered index cần QUOTED_IDENTIFIER ON; sqlcmd mặc định OFF nên set lại trong batch này)
SET QUOTED_IDENTIFIER ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CongVanDi_CongKhai')
    CREATE INDEX IX_CongVanDi_CongKhai ON CongVanDi(CongKhai, NgayCongVan DESC) WHERE CongKhai = 1;
GO
