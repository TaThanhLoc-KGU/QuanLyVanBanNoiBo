SET QUOTED_IDENTIFIER ON;
GO
-- Lịch công tác: kết hợp lịch cá nhân/lãnh đạo + lịch dùng chung toàn trường (MaDV=NULL).
-- Hạn xử lý của Công việc (CongViec.HanXuLy) KHÔNG lưu trùng ở đây — Index sẽ UNION đọc
-- trực tiếp từ CongViec khi hiển thị theo khoảng ngày.
-- Phần "Văn phòng điện tử" — Phase 3/4.

IF OBJECT_ID('LichCongTac') IS NULL
BEGIN
    CREATE TABLE LichCongTac (
        MaLich          int IDENTITY(1,1) PRIMARY KEY,
        TieuDe          nvarchar(500)  NOT NULL,
        NoiDung         nvarchar(max)  NULL,
        ThoiGianBatDau  smalldatetime  NOT NULL,
        ThoiGianKetThuc smalldatetime  NULL,
        DiaDiem         nvarchar(255)  NULL,
        MaDV            tinyint        NULL,
        NguoiThamGia    nvarchar(1000) NULL,
        LoaiSuKien      tinyint        NOT NULL DEFAULT 1,
        MaNVTao         smallint       NOT NULL,
        NgayTao         smalldatetime  NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_LichCongTac_DV    FOREIGN KEY (MaDV)    REFERENCES DonVi(MaDV),
        CONSTRAINT FK_LichCongTac_NVTao FOREIGN KEY (MaNVTao) REFERENCES NhanVien(MaNV)
    );
END
GO
