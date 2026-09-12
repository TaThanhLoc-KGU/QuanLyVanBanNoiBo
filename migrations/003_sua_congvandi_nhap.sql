-- Sửa lỗi lưu "Vào số văn bản đi": stored procedure CongVanDi_Them/CongVanDi_Sua cũ đã lỗi thời
-- (thiếu tham số @KyTu, tra cứu loại văn bản/nhóm văn bản theo TÊN vào bảng LoaiVBCVDi không còn
-- dùng nữa sau khi gộp danh mục Loại văn bản — xem 002_gop_loaivb.sql) nên mọi lần lưu đều báo lỗi
-- 500. Code (DbService.ThemCongVanDiAsync/SuaCongVanDiAsync) đã chuyển sang SQL trực tiếp như
-- CongVanDen, dùng MaNCV/MaLVB theo ID có sẵn từ form thay vì tự tạo theo tên.
--
-- CongVanDi.MaNCV hiện đang NOT NULL nhưng màn hình nhập cho phép bỏ trống "Nhóm văn bản"
-- (giống CongVanDen.MaNCV vốn đã NULL-able) -> phải cho phép NULL để không bị lỗi khoá ngoại
-- khi người dùng không chọn nhóm.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CongVanDi') AND name = 'MaNCV' AND is_nullable = 0)
BEGIN
    ALTER TABLE CongVanDi ALTER COLUMN MaNCV smallint NULL;
END
GO

-- Ghi chú: KHÔNG xoá stored procedure CongVanDi_Them/CongVanDi_Sua (không còn được gọi từ code,
-- giữ lại phòng khi cần đối chiếu/rollback).
