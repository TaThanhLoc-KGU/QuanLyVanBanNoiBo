-- 027: Tự động sinh số hiệu cho văn bản nội bộ đơn vị — mỗi đơn vị tự định nghĩa "loại" của riêng
-- mình (vd "QĐ"=Quyết định, "TB"=Thông báo) kèm mẫu chuỗi số hiệu, không cần qua Admin trung tâm
-- (khác MauSoKyHieu/SoCV dùng chung cho CongVanDi/VanBanDieuHanh — những bảng đó không có cột MaDV
-- nên không tự nhiên dùng riêng theo từng đơn vị được).
IF OBJECT_ID('VanBanNoiBo_MauSo') IS NULL
BEGIN
    CREATE TABLE VanBanNoiBo_MauSo (
        ID       INT IDENTITY PRIMARY KEY,
        MaDV     TINYINT NOT NULL,
        MaLoai   NVARCHAR(20) NOT NULL,   -- đơn vị tự đặt, vd "QĐ", "TB", "CV"
        TenLoai  NVARCHAR(100) NOT NULL,  -- vd "Quyết định", "Thông báo"
        MauChuoi NVARCHAR(150) NOT NULL,  -- vd "{STT2}/QĐ-P.QTCSVC" — placeholder giống MauSoKyHieu: {STT}/{STT2}/{STT3}/{NAM}/{NAM2}
        NgayTao  DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_VanBanNoiBo_MauSo_DonVi FOREIGN KEY (MaDV) REFERENCES DonVi(MaDV),
        CONSTRAINT UX_VanBanNoiBo_MauSo UNIQUE (MaDV, MaLoai)
    );
END

IF COL_LENGTH('VanBanNoiBo', 'LoaiVanBanNoiBo') IS NULL
    ALTER TABLE VanBanNoiBo ADD LoaiVanBanNoiBo NVARCHAR(20) NULL;
