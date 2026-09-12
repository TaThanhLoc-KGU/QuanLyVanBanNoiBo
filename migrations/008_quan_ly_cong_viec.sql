SET QUOTED_IDENTIFIER ON;
GO
-- Quản lý công việc: giao việc nội bộ (không nhất thiết gắn với 1 văn bản), mô hình
-- 1 người chủ trì + nhiều người phối hợp (giống LoaiDV 1=Chủ trì/2=Phối hợp đang dùng ở
-- CongVanDenXuLyDV). Khoá chính dùng int IDENTITY (không sinh chuỗi MSCV kiểu yyyyMM+seq)
-- vì đây là bản ghi nghiệp vụ nội bộ, không phải văn bản cần số hiệu chính thức.
-- Phần "Văn phòng điện tử" — Phase 1/4.

IF OBJECT_ID('CongViec') IS NULL
BEGIN
    CREATE TABLE CongViec (
        MaCV            int IDENTITY(1,1) PRIMARY KEY,
        TieuDe          nvarchar(500)  NOT NULL,
        MoTa            nvarchar(max)  NULL,
        MaNVGiao        smallint       NOT NULL,
        MaNVChuTri      smallint       NOT NULL,
        MaDV            tinyint        NOT NULL,
        NguoiPhoiHop    nvarchar(500)  NULL,
        NgayGiao        smalldatetime  NOT NULL DEFAULT GETDATE(),
        HanXuLy         smalldatetime  NULL,
        MucDoUuTien     tinyint        NOT NULL DEFAULT 1,
        TrangThai       tinyint        NOT NULL DEFAULT 0,
        NgayHoanThanh   smalldatetime  NULL,
        LoaiNguonGoc    tinyint        NULL,
        MSCVGoc         nchar(20)      NULL,
        GhiChu          nvarchar(1000) NULL,
        MaNVTao         smallint       NOT NULL,
        NgayTao         smalldatetime  NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_CongViec_NVGiao   FOREIGN KEY (MaNVGiao)   REFERENCES NhanVien(MaNV),
        CONSTRAINT FK_CongViec_NVChuTri FOREIGN KEY (MaNVChuTri) REFERENCES NhanVien(MaNV),
        CONSTRAINT FK_CongViec_DV       FOREIGN KEY (MaDV)       REFERENCES DonVi(MaDV),
        CONSTRAINT FK_CongViec_NVTao    FOREIGN KEY (MaNVTao)    REFERENCES NhanVien(MaNV)
    );
END
GO

IF OBJECT_ID('CongViec_NhatKy') IS NULL
BEGIN
    CREATE TABLE CongViec_NhatKy (
        ID              int IDENTITY(1,1) PRIMARY KEY,
        MaCV            int             NOT NULL,
        MaNV            smallint        NOT NULL,
        NgayGhi         datetime        NOT NULL DEFAULT GETDATE(),
        NoiDung         nvarchar(2000)  NOT NULL,
        TrangThaiMoi    tinyint         NULL,
        FileDinhKem     nvarchar(500)   NULL,
        CONSTRAINT FK_CongViecNhatKy_CV FOREIGN KEY (MaCV) REFERENCES CongViec(MaCV),
        CONSTRAINT FK_CongViecNhatKy_NV FOREIGN KEY (MaNV) REFERENCES NhanVien(MaNV)
    );
END
GO

IF OBJECT_ID('CongViec_File') IS NULL
BEGIN
    CREATE TABLE CongViec_File (
        ID          int             IDENTITY(1,1) PRIMARY KEY,
        MaCV        int             NOT NULL,
        TenFile     nvarchar(255)   NOT NULL,
        DuongDan    nvarchar(500)   NOT NULL,
        NgayUpload  datetime        NOT NULL DEFAULT GETDATE(),
        MaNVUpload  smallint        NOT NULL,
        CONSTRAINT FK_CongViecFile_CV FOREIGN KEY (MaCV)       REFERENCES CongViec(MaCV),
        CONSTRAINT FK_CongViecFile_NV FOREIGN KEY (MaNVUpload) REFERENCES NhanVien(MaNV)
    );
END
GO
