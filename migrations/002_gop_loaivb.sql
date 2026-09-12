-- Gộp danh mục Loại văn bản dùng chung cho Công văn đến & Công văn đi
-- Trước khi chạy trên production: sao lưu (backup) database.
-- An toàn chạy lại nhiều lần (idempotent) nhờ các điều kiện IF NOT EXISTS / WHERE NOT EXISTS.

-- 1) Thêm cột Mã ký hiệu (dùng cho tự sinh số văn bản ở giai đoạn sau, VD 'QĐ', 'TB', 'KH')
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('LoaiVB') AND name = 'KyHieu')
BEGIN
    ALTER TABLE LoaiVB ADD KyHieu nvarchar(20) NULL;
END
GO

-- 2) Chép các loại văn bản chỉ có ở LoaiVBCVDi (chưa có trong LoaiVB) sang LoaiVB
INSERT INTO LoaiVB (TenLVB, HienThi)
SELECT lvd.TenLVB, lvd.HienThi
FROM LoaiVBCVDi lvd
WHERE NOT EXISTS (SELECT 1 FROM LoaiVB lv WHERE lv.TenLVB = lvd.TenLVB);
GO

-- 3) Trỏ lại CongVanDi.MaLVB sang mã tương ứng bên LoaiVB (khớp theo tên)
UPDATE cv
SET cv.MaLVB = lv.MaLVB
FROM CongVanDi cv
JOIN LoaiVBCVDi old ON cv.MaLVB = old.MaLVB
JOIN LoaiVB lv ON lv.TenLVB = old.TenLVB
WHERE cv.MaLVB <> lv.MaLVB;
GO

-- 4) Đổi khoá ngoại CongVanDi.MaLVB từ LoaiVBCVDi sang LoaiVB (bắt buộc — nếu không,
--    lưu công văn đi với loại văn bản mới thêm ở LoaiVB sẽ bị lỗi vi phạm khoá ngoại)
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CongVanDi_LoaiVBCVDi')
BEGIN
    ALTER TABLE CongVanDi DROP CONSTRAINT FK_CongVanDi_LoaiVBCVDi;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CongVanDi_LoaiVB')
BEGIN
    ALTER TABLE CongVanDi ADD CONSTRAINT FK_CongVanDi_LoaiVB FOREIGN KEY (MaLVB) REFERENCES LoaiVB(MaLVB);
END
GO

-- Ghi chú: KHÔNG xoá bảng LoaiVBCVDi ở bước này (giữ lại để rollback nếu cần).
-- Sẽ dọn dẹp (DROP TABLE LoaiVBCVDi) ở một đợt migration sau khi đã xác nhận ổn định trên production.
