-- 2026-09-13: "Công khai" cho Thông báo — đánh dấu thông báo nào được phép hiển thị ra trang công
-- khai (/cong-khai, không cần đăng nhập) cùng đợt với trang lịch làm việc công khai + feed .ics
-- công khai. Cùng ý tưởng "dấu công khai" đã dùng cho CongVanDi (migration 032) — mặc định KHÔNG lộ,
-- người đăng thông báo phải chủ động tick mới hiện ra trang công khai.

IF COL_LENGTH('ThongBaoNoiBo', 'CongKhai') IS NULL
    ALTER TABLE ThongBaoNoiBo ADD CongKhai bit NOT NULL CONSTRAINT DF_ThongBaoNoiBo_CongKhai DEFAULT 0;
GO

SET QUOTED_IDENTIFIER ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ThongBaoNoiBo_CongKhai')
    CREATE INDEX IX_ThongBaoNoiBo_CongKhai ON ThongBaoNoiBo(CongKhai, NgayDang DESC) WHERE CongKhai = 1;
GO
