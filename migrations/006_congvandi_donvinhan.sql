-- Chạy file này với: sqlcmd ... -f 65001 -i 006_congvandi_donvinhan.sql
-- (bắt buộc -f 65001 để sqlcmd đọc đúng UTF-8, nếu không các câu UPDATE có chữ "Đ" sẽ
-- không khớp được điều kiện WHERE do sqlcmd đọc sai encoding của file theo mặc định).
SET QUOTED_IDENTIFIER ON;
GO
-- Công văn đi hiện chỉ có "Nơi nhận" dạng chữ tự do (VD gõ tay "P.QTCSVC"), không gắn với
-- đơn vị thật trong hệ thống -> đơn vị nhận không thấy văn bản đó ở đâu trong tài khoản của
-- họ (mục "Tra cứu văn bản đơn vị" chỉ hiển thị văn bản ĐẾN). Thêm cột DonViNhan (danh sách
-- MaDV cách nhau dấu phẩy, giống CongVanDen.BoPhanPhoiHop) để chọn đơn vị nội bộ có cấu trúc,
-- giữ nguyên NoiNhanCV cho nơi nhận ngoài trường / ghi chú thêm.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CongVanDi') AND name = 'DonViNhan')
BEGIN
    ALTER TABLE CongVanDi ADD DonViNhan nvarchar(500) NULL;
END
GO

-- Backfill dữ liệu cũ: khớp NoiNhanCV hiện có với đơn vị thật (thủ công vì chỉ có 11 giá trị
-- khác nhau trong dữ liệu hiện tại — một số là bên ngoài trường như "KBNN tỉnh Kiên Giang",
-- "Tạp chí Kế toán và Kiểm toán" nên không có MaDV tương ứng, giữ nguyên trong NoiNhanCV).
UPDATE CongVanDi SET DonViNhan = '24' WHERE NoiNhanCV = N'P.QTCSVC' AND DonViNhan IS NULL;
UPDATE CongVanDi SET DonViNhan = '33' WHERE NoiNhanCV = N'P.CTSV&KN' AND DonViNhan IS NULL;
UPDATE CongVanDi SET DonViNhan = '33,9' WHERE NoiNhanCV = N'P.CTSV&KN; P.ĐT' AND DonViNhan IS NULL;
UPDATE CongVanDi SET DonViNhan = '9' WHERE NoiNhanCV = N'P.ĐT' AND DonViNhan IS NULL;
UPDATE CongVanDi SET DonViNhan = '5' WHERE NoiNhanCV = N'P.HTKHCN' AND DonViNhan IS NULL;
UPDATE CongVanDi SET DonViNhan = '3' WHERE NoiNhanCV = N'P.TCNS' AND DonViNhan IS NULL;
UPDATE CongVanDi SET DonViNhan = '32' WHERE NoiNhanCV = N'P.BĐCL&TTPC' AND DonViNhan IS NULL;
UPDATE CongVanDi SET DonViNhan = '19' WHERE NoiNhanCV = N'TTĐTSHTH' AND DonViNhan IS NULL;
UPDATE CongVanDi SET DonViNhan = '26,1' WHERE NoiNhanCV = N'Đảng Ủy, BGH' AND DonViNhan IS NULL;
GO
