SET QUOTED_IDENTIFIER ON;
GO
-- Liên kết ngược Văn bản điều hành <-> Văn bản đi: khi nhập văn bản đi, văn thư có thể tick
-- "Cũng là văn bản điều hành" để tự tạo thêm 1 bản ghi bên Sổ văn bản điều hành, dùng CHUNG
-- số ký hiệu (không tự sinh số mới) vì thực chất chỉ là một văn bản duy nhất được xem từ 2 góc.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('VanBanDieuHanh') AND name = 'MSCVDi')
BEGIN
    ALTER TABLE VanBanDieuHanh ADD MSCVDi nchar(20) NULL;
END
GO
