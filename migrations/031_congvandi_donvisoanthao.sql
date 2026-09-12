-- 2026-08-27: Sổ đăng ký văn bản đi cần tách riêng "Đơn vị soạn thảo" khỏi "Người soạn thảo"
-- (trước đây gộp chung trong cột NguoiSoanThao). Cả 2 đều KHÔNG bắt buộc.
-- Lưu dạng text (tên/tên viết tắt đơn vị) cho đồng bộ với NguoiSoanThao, không dùng khóa ngoại
-- để không vướng luồng tạo văn bản đến mirror.

IF COL_LENGTH('CongVanDi', 'DonViSoanThao') IS NULL
BEGIN
    ALTER TABLE CongVanDi ADD DonViSoanThao nvarchar(150) NULL;
END
GO
