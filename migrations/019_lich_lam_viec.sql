SET QUOTED_IDENTIFIER ON;
GO
-- Lịch làm việc — lịch riêng của lãnh đạo (đơn vị + trường), KHÁC với Lịch công tác (toàn trường,
-- ai cũng thêm được cho mình/đơn vị mình). Chỉ người có mã chức năng "LichLamViec.Tao" (vai trò
-- "Lãnh đạo đơn vị"/"Lãnh đạo trường", đã cấp sẵn ở migration 017) mới tạo được, và chỉ quản lý
-- lịch của chính mình (MaNVLanhDao). "Người kèm theo" (NguoiKemTheo) CỐ Ý không hiện ở mức tổng
-- quan (lưới lịch chỉ hiện SỐ LƯỢNG lãnh đạo có lịch/ngày) — chỉ hiện khi xem chi tiết 1 ngày cụ
-- thể, theo đúng yêu cầu người dùng.
-- Phần "Văn phòng điện tử" — Phase 14.

IF OBJECT_ID('LichLamViec') IS NULL
BEGIN
    CREATE TABLE LichLamViec (
        MaLich          int IDENTITY(1,1) PRIMARY KEY,
        TieuDe          nvarchar(500)  NOT NULL,
        NoiDung         nvarchar(max)  NULL,
        ThoiGianBatDau  smalldatetime  NOT NULL,
        ThoiGianKetThuc smalldatetime  NULL,
        DiaDiem         nvarchar(255)  NULL,
        MaNVLanhDao     smallint       NOT NULL,
        NguoiKemTheo    nvarchar(1000) NULL,
        LoaiSuKien      tinyint        NOT NULL DEFAULT 1,
        MaNVTao         smallint       NOT NULL,
        NgayTao         smalldatetime  NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_LLV_LanhDao FOREIGN KEY (MaNVLanhDao) REFERENCES NhanVien(MaNV),
        CONSTRAINT FK_LLV_NVTao   FOREIGN KEY (MaNVTao)     REFERENCES NhanVien(MaNV)
    );
    CREATE INDEX IX_LichLamViec_ThoiGian ON LichLamViec(ThoiGianBatDau);
END
GO
