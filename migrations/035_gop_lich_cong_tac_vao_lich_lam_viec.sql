-- 2026-09-06: GỘP "Lịch công tác" thành 1 PHẦN của "Lịch làm việc" (yêu cầu người dùng).
-- Từ nay chỉ còn 1 module Lịch làm việc, phân biệt bằng cột LoaiLich:
--   1 = Lịch của lãnh đạo (như LichLamViec cũ — chỉ người có LichLamViec.Tao mới tạo)
--   2 = Lịch công tác (như LichCongTac cũ — sự kiện toàn trường / theo đơn vị, ai cũng tạo được)
-- Bảng LichCongTac ĐƯỢC GIỮ NGUYÊN (không xóa) để còn đối chiếu; dữ liệu được COPY sang.

SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('LichLamViec', 'LoaiLich') IS NULL
    ALTER TABLE LichLamViec ADD LoaiLich tinyint NOT NULL CONSTRAINT DF_LichLamViec_LoaiLich DEFAULT 1;
GO
IF COL_LENGTH('LichLamViec', 'MaDV') IS NULL
    ALTER TABLE LichLamViec ADD MaDV tinyint NULL;   -- chỉ dùng cho LoaiLich=2; NULL = toàn trường
GO
IF COL_LENGTH('LichLamViec', 'MaPhong') IS NULL
    ALTER TABLE LichLamViec ADD MaPhong tinyint NULL; -- phòng họp (từ Lịch công tác)
GO
-- Đánh dấu dòng đã copy từ LichCongTac để không copy trùng khi chạy lại
IF COL_LENGTH('LichLamViec', 'MaLichCongTacGoc') IS NULL
    ALTER TABLE LichLamViec ADD MaLichCongTacGoc int NULL;
GO

-- MaNVLanhDao đang NOT NULL nhưng lịch công tác không có "lãnh đạo" — dùng người tạo làm chủ lịch.
INSERT INTO LichLamViec (TieuDe, NoiDung, ThoiGianBatDau, ThoiGianKetThuc, DiaDiem,
                         MaNVLanhDao, NguoiKemTheo, LoaiSuKien, MaNVTao, NgayTao,
                         LoaiLich, MaDV, MaPhong, MaLichCongTacGoc)
SELECT ct.TieuDe, ct.NoiDung, ct.ThoiGianBatDau, ct.ThoiGianKetThuc, ct.DiaDiem,
       ct.MaNVTao, ct.NguoiThamGia, ct.LoaiSuKien, ct.MaNVTao, ct.NgayTao,
       2, ct.MaDV, ct.MaPhong, ct.MaLich
FROM LichCongTac ct
WHERE NOT EXISTS (SELECT 1 FROM LichLamViec l WHERE l.MaLichCongTacGoc = ct.MaLich);
GO

PRINT N'035: da gop Lich cong tac vao Lich lam viec (LoaiLich=2).';
GO
