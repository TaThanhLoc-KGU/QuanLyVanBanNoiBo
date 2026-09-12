SET QUOTED_IDENTIFIER ON;
GO
-- Phân quyền chi tiết: mô hình Vai trò (Role) = tập hợp các mã chức năng chi tiết
-- (VD "Admin.NhanVien", "ThongBao.Dang"), gán cho nhân viên qua NhanVien_VaiTro; vẫn giữ
-- khả năng cấp quyền TRỰC TIẾP cho 1 người ngoài vai trò (override) qua NhanVien_ChucNang.
-- Quyền hiệu lực = hợp của (mọi vai trò được gán) ∪ (cấp trực tiếp) — chỉ cộng thêm, không có
-- cơ chế thu hồi/deny, đơn giản và nhất quán với PhanQuyen hiện có (cũng chỉ cộng).
-- Admin (MaQuyen=0) và Văn thư (MaQuyen=99) trong hệ PhanQuyen cũ vẫn luôn được coi là có mọi
-- quyền chi tiết — không cần gán vai trò riêng, tránh phá vỡ hành vi hiện tại.
-- Phần "Văn phòng điện tử" — Phase 8/10 (phân quyền).

IF OBJECT_ID('VaiTro') IS NULL
BEGIN
    CREATE TABLE VaiTro (
        MaVaiTro   int IDENTITY(1,1) PRIMARY KEY,
        TenVaiTro  nvarchar(200) NOT NULL,
        GhiChu     nvarchar(500) NULL,
        NgayTao    smalldatetime NOT NULL DEFAULT GETDATE()
    );
END
GO

IF OBJECT_ID('VaiTro_Quyen') IS NULL
BEGIN
    CREATE TABLE VaiTro_Quyen (
        MaVaiTro   int NOT NULL,
        MaChucNang nvarchar(100) NOT NULL,
        PRIMARY KEY (MaVaiTro, MaChucNang),
        CONSTRAINT FK_VaiTroQuyen_VaiTro FOREIGN KEY (MaVaiTro) REFERENCES VaiTro(MaVaiTro)
    );
END
GO

IF OBJECT_ID('NhanVien_VaiTro') IS NULL
BEGIN
    CREATE TABLE NhanVien_VaiTro (
        MaNV      smallint NOT NULL,
        MaVaiTro  int NOT NULL,
        PRIMARY KEY (MaNV, MaVaiTro),
        CONSTRAINT FK_NVVaiTro_NV     FOREIGN KEY (MaNV)     REFERENCES NhanVien(MaNV),
        CONSTRAINT FK_NVVaiTro_VaiTro FOREIGN KEY (MaVaiTro) REFERENCES VaiTro(MaVaiTro)
    );
END
GO

IF OBJECT_ID('NhanVien_ChucNang') IS NULL
BEGIN
    CREATE TABLE NhanVien_ChucNang (
        MaNV       smallint NOT NULL,
        MaChucNang nvarchar(100) NOT NULL,
        PRIMARY KEY (MaNV, MaChucNang),
        CONSTRAINT FK_NVChucNang_NV FOREIGN KEY (MaNV) REFERENCES NhanVien(MaNV)
    );
END
GO
