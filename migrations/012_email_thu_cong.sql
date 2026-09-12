SET QUOTED_IDENTIFIER ON;
GO
-- Gửi email thông báo văn bản đến: đổi từ tự động (fire-and-forget khi lưu) sang thủ công
-- (văn thư/đơn vị bấm nút mới gửi). Thêm cột theo dõi đã gửi hay chưa trên CongVanDen — dùng
-- chung cho cả luồng nhập trực tiếp và luồng mirror từ văn bản đi (mỗi mirror là 1 dòng
-- CongVanDen riêng nên không cần bảng/cột riêng cho luồng thứ hai).

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CongVanDen') AND name = 'NgayGuiEmail')
BEGIN
    ALTER TABLE CongVanDen ADD NgayGuiEmail datetime2 NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CongVanDen') AND name = 'MaNVGuiEmail')
BEGIN
    ALTER TABLE CongVanDen ADD MaNVGuiEmail smallint NULL;
END
GO
