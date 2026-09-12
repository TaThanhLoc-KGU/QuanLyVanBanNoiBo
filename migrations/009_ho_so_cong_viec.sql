SET QUOTED_IDENTIFIER ON;
GO
-- Quản lý hồ sơ công việc: module case-file độc lập, gom nhiều Công việc (CongViec) +
-- nhiều văn bản (từ cả 3 sổ: CongVanDen/CongVanDi/VanBanDieuHanh) lại thành 1 bộ hồ sơ
-- theo dõi xuyên suốt 1 vụ việc. Liên kết văn bản là polymorphic (LoaiVanBan + MSCV,
-- không FK cứng vì 3 bảng nguồn khác nhau) — resolve ở tầng C# khi hiển thị.
-- Phần "Văn phòng điện tử" — Phase 2/4.

IF OBJECT_ID('HoSoCongViec') IS NULL
BEGIN
    CREATE TABLE HoSoCongViec (
        MaHoSo          int IDENTITY(1,1) PRIMARY KEY,
        TieuDe          nvarchar(500)  NOT NULL,
        MoTa            nvarchar(max)  NULL,
        MaDVPhuTrach    tinyint        NOT NULL,
        MaNVPhuTrach    smallint       NOT NULL,
        TrangThai       tinyint        NOT NULL DEFAULT 0,
        NgayMo          smalldatetime  NOT NULL DEFAULT GETDATE(),
        NgayDong        smalldatetime  NULL,
        GhiChu          nvarchar(1000) NULL,
        MaNVTao         smallint       NOT NULL,
        NgayTao         smalldatetime  NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_HoSoCongViec_DV     FOREIGN KEY (MaDVPhuTrach) REFERENCES DonVi(MaDV),
        CONSTRAINT FK_HoSoCongViec_NVPT   FOREIGN KEY (MaNVPhuTrach) REFERENCES NhanVien(MaNV),
        CONSTRAINT FK_HoSoCongViec_NVTao  FOREIGN KEY (MaNVTao)      REFERENCES NhanVien(MaNV)
    );
END
GO

IF OBJECT_ID('HoSoCongViec_CongViec') IS NULL
BEGIN
    CREATE TABLE HoSoCongViec_CongViec (
        MaHoSo int NOT NULL,
        MaCV   int NOT NULL,
        PRIMARY KEY (MaHoSo, MaCV),
        CONSTRAINT FK_HSCV_CV_HoSo FOREIGN KEY (MaHoSo) REFERENCES HoSoCongViec(MaHoSo),
        CONSTRAINT FK_HSCV_CV_CV   FOREIGN KEY (MaCV)   REFERENCES CongViec(MaCV)
    );
END
GO

IF OBJECT_ID('HoSoCongViec_VanBan') IS NULL
BEGIN
    CREATE TABLE HoSoCongViec_VanBan (
        ID          int IDENTITY(1,1) PRIMARY KEY,
        MaHoSo      int NOT NULL,
        LoaiVanBan  tinyint NOT NULL,   -- 1=CongVanDen,2=CongVanDi,3=VanBanDieuHanh
        MSCV        nchar(20) NOT NULL,
        NgayGan     datetime NOT NULL DEFAULT GETDATE(),
        MaNVGan     smallint NOT NULL,
        CONSTRAINT FK_HSCV_VB_HoSo FOREIGN KEY (MaHoSo)  REFERENCES HoSoCongViec(MaHoSo),
        CONSTRAINT FK_HSCV_VB_NV   FOREIGN KEY (MaNVGan) REFERENCES NhanVien(MaNV)
    );
END
GO

IF OBJECT_ID('HoSoCongViec_File') IS NULL
BEGIN
    CREATE TABLE HoSoCongViec_File (
        ID          int IDENTITY(1,1) PRIMARY KEY,
        MaHoSo      int NOT NULL,
        TenFile     nvarchar(255) NOT NULL,
        DuongDan    nvarchar(500) NOT NULL,
        NgayUpload  datetime NOT NULL DEFAULT GETDATE(),
        MaNVUpload  smallint NOT NULL,
        CONSTRAINT FK_HSCV_File_HoSo FOREIGN KEY (MaHoSo)     REFERENCES HoSoCongViec(MaHoSo),
        CONSTRAINT FK_HSCV_File_NV   FOREIGN KEY (MaNVUpload) REFERENCES NhanVien(MaNV)
    );
END
GO
