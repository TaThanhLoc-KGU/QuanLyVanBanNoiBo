SET QUOTED_IDENTIFIER ON;
GO
-- Thôi việc/nghỉ việc thay vì xóa cứng: NgayNghiViec NOT NULL = đã ẩn khỏi mọi danh sách chọn
-- người (giao việc mới, chọn lãnh đạo/văn thư đơn vị, mời tham gia lịch...) và không đăng nhập
-- được nữa, nhưng KHÔNG xóa bản ghi — mọi văn bản/công việc lịch sử liên quan vẫn hiển thị đúng
-- tên. Trang Admin/NhanVien vẫn thấy + có thể "Khôi phục" hoặc xóa vĩnh viễn (nếu không còn ràng
-- buộc dữ liệu nào tham chiếu tới).
-- Phần "Văn phòng điện tử" — Phase 13 (theo yêu cầu người dùng, cùng đợt với RBAC v3).

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('NhanVien') AND name = 'NgayNghiViec')
BEGIN
    ALTER TABLE NhanVien ADD NgayNghiViec date NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('NhanVien') AND name = 'LyDoNghiViec')
BEGIN
    ALTER TABLE NhanVien ADD LyDoNghiViec nvarchar(255) NULL;
END
GO
