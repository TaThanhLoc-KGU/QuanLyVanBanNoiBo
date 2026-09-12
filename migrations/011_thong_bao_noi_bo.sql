SET QUOTED_IDENTIFIER ON;
GO
-- Thông báo nội bộ: có nhắm đối tượng (toàn trường/theo đơn vị/theo vai trò-quyền) + theo
-- dõi đã đọc, đồng bộ đúng cơ chế CVDen_DaXem đã có (bảng con 1 dòng/người/thông báo).
-- Phần "Văn phòng điện tử" — Phase 4/4.

IF OBJECT_ID('ThongBaoNoiBo') IS NULL
BEGIN
    CREATE TABLE ThongBaoNoiBo (
        MaTB        int IDENTITY(1,1) PRIMARY KEY,
        TieuDe      nvarchar(500)  NOT NULL,
        NoiDung     nvarchar(max)  NOT NULL,
        DoiTuong    tinyint        NOT NULL DEFAULT 0,
        DonViNhan   nvarchar(500)  NULL,
        QuyenNhan   nvarchar(200)  NULL,
        MucDo       tinyint        NOT NULL DEFAULT 1,
        FileDinhKem nvarchar(500)  NULL,
        MaNVDang    smallint       NOT NULL,
        NgayDang    smalldatetime  NOT NULL DEFAULT GETDATE(),
        HetHan      smalldatetime  NULL,
        CONSTRAINT FK_ThongBaoNoiBo_NV FOREIGN KEY (MaNVDang) REFERENCES NhanVien(MaNV)
    );
END
GO

IF OBJECT_ID('ThongBaoNoiBo_DaXem') IS NULL
BEGIN
    CREATE TABLE ThongBaoNoiBo_DaXem (
        MaTB    int NOT NULL,
        MaNV    smallint NOT NULL,
        NgayXem datetime NOT NULL DEFAULT GETDATE(),
        PRIMARY KEY (MaTB, MaNV),
        CONSTRAINT FK_TBDaXem_TB FOREIGN KEY (MaTB) REFERENCES ThongBaoNoiBo(MaTB),
        CONSTRAINT FK_TBDaXem_NV FOREIGN KEY (MaNV) REFERENCES NhanVien(MaNV)
    );
END
GO
