-- 028: Tìm kiếm theo nội dung PDF của văn bản đến — trích xuất chữ 1 lần lúc upload (đọc trực tiếp
-- nếu PDF có sẵn lớp chữ, OCR qua Tesseract nếu là bản scan/ảnh), lưu vào cột này rồi tìm bằng LIKE
-- cùng ô "Tìm kiếm" đã có, không cần thêm ô tìm kiếm riêng.
IF COL_LENGTH('CVDenFile', 'NoiDungTrichXuat') IS NULL
    ALTER TABLE CVDenFile ADD NoiDungTrichXuat NVARCHAR(MAX) NULL;

IF COL_LENGTH('CVDenFile', 'TrangThaiTrichXuat') IS NULL
    ALTER TABLE CVDenFile ADD TrangThaiTrichXuat TINYINT NOT NULL DEFAULT 0; -- 0=Chưa xử lý,1=Xong,2=Lỗi
