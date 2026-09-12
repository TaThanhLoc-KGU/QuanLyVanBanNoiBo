SET QUOTED_IDENTIFIER ON;
GO
-- Quản lý văn bản điều hành: sổ riêng biệt cho văn bản nội bộ trường tự ban hành
-- (Quyết định, Kế hoạch, Thông báo...) để điều hành hoạt động — KHÔNG gửi ra cơ quan
-- bên ngoài (khác với CongVanDi). Dùng chung danh mục LoaiVB và hạ tầng Sổ văn bản /
-- Mẫu số ký hiệu đã có (xem 004_so_van_ban_tu_sinh_so.sql). Không bắt buộc file đính kèm,
-- theo đúng yêu cầu chung của cả hệ thống (nhập thông tin trước, scan sau).

IF OBJECT_ID('VanBanDieuHanh') IS NULL
BEGIN
    CREATE TABLE VanBanDieuHanh (
        MSCV          nchar(20)      NOT NULL PRIMARY KEY,
        STT           int            NOT NULL,
        STT1          nvarchar(100)  NULL,
        NgayBanHanh   smalldatetime  NOT NULL,
        MaSCV         tinyint        NOT NULL,
        MaLVB         smallint       NOT NULL,
        TrichYeu      nvarchar(1000) NOT NULL,
        MaLDKy        smallint       NULL,
        PhamViApDung  nvarchar(1000) NULL,
        GhiChu        nvarchar(1000) NULL,
        FileDinhKem   nvarchar(500)  NULL,
        MaVT          smallint       NOT NULL,
        NgayNhap      smalldatetime  NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_VanBanDieuHanh_SoCV    FOREIGN KEY (MaSCV)  REFERENCES SoCV(MaSCV),
        CONSTRAINT FK_VanBanDieuHanh_LoaiVB  FOREIGN KEY (MaLVB)  REFERENCES LoaiVB(MaLVB),
        CONSTRAINT FK_VanBanDieuHanh_LDKy    FOREIGN KEY (MaLDKy) REFERENCES NhanVien(MaNV),
        CONSTRAINT FK_VanBanDieuHanh_VanThu  FOREIGN KEY (MaVT)   REFERENCES NhanVien(MaNV)
    );
END
GO
