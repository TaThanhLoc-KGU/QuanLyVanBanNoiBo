-- 024: Cơ chế nội bộ đơn vị — 3 tính năng mới, tách riêng khỏi luồng công văn toàn trường
-- (1) Lịch làm việc nội bộ đơn vị (khác LichLamViec cá nhân lãnh đạo, khác LichCongTac toàn trường)
-- (2) Văn bản nội bộ đơn vị — sổ văn bản riêng theo từng đơn vị (có số hiệu/ký hiệu/người ký như 1 sổ thật)
-- (3) API Key theo đơn vị — cho phép web riêng của 1 đơn vị gọi API văn bản nội bộ của chính đơn vị đó
-- Phân quyền nội bộ đơn vị (ai là văn thư đơn vị) KHÔNG cần bảng mới — dùng lại DonVi_VanThu đã có,
-- chỉ mở thêm 1 trang cho lãnh đạo đơn vị tự quản lý (xem Controllers/DonViController.cs).

IF OBJECT_ID('LichNoiBoDonVi') IS NULL
BEGIN
    CREATE TABLE LichNoiBoDonVi (
        MaLich          INT IDENTITY PRIMARY KEY,
        MaDV            TINYINT NOT NULL,
        TieuDe          NVARCHAR(255) NOT NULL,
        NoiDung         NVARCHAR(MAX) NULL,
        ThoiGianBatDau  DATETIME NOT NULL,
        ThoiGianKetThuc DATETIME NULL,
        DiaDiem         NVARCHAR(255) NULL,
        LoaiSuKien      TINYINT NOT NULL DEFAULT 1, -- 1=Họp,2=Công tác,3=Sự kiện,4=Khác
        MaNVTao         SMALLINT NOT NULL,
        NgayTao         DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_LichNoiBoDonVi_DonVi FOREIGN KEY (MaDV) REFERENCES DonVi(MaDV)
    );
    CREATE INDEX IX_LichNoiBoDonVi_MaDV ON LichNoiBoDonVi(MaDV, ThoiGianBatDau);
END

IF OBJECT_ID('VanBanNoiBo') IS NULL
BEGIN
    CREATE TABLE VanBanNoiBo (
        ID          INT IDENTITY PRIMARY KEY,
        MaDV        TINYINT NOT NULL,
        STT         INT NOT NULL,            -- số thứ tự tự tăng riêng theo (đơn vị, năm)
        SoHieu      NVARCHAR(100) NULL,      -- vd "12/TB-P.QTCSVC", đơn vị tự đặt
        TieuDe      NVARCHAR(500) NOT NULL,
        NoiDung     NVARCHAR(MAX) NULL,
        NgayBanHanh DATE NOT NULL,
        NguoiKy     NVARCHAR(255) NULL,      -- tên người ký (chữ tự do, có thể ngoài hệ thống NhanVien)
        MaNVKy      SMALLINT NULL,           -- nếu người ký có tài khoản trong hệ thống
        TrangThai   TINYINT NOT NULL DEFAULT 1, -- 0=Dự thảo,1=Đã ban hành,2=Thu hồi
        MaNVTao     SMALLINT NOT NULL,
        NgayTao     DATETIME NOT NULL DEFAULT GETDATE(),
        NguonTao    TINYINT NOT NULL DEFAULT 0, -- 0=Web nội bộ,1=API ngoài
        CONSTRAINT FK_VanBanNoiBo_DonVi FOREIGN KEY (MaDV) REFERENCES DonVi(MaDV)
    );
    CREATE INDEX IX_VanBanNoiBo_MaDV ON VanBanNoiBo(MaDV, NgayBanHanh DESC);
END

IF OBJECT_ID('VanBanNoiBo_File') IS NULL
BEGIN
    CREATE TABLE VanBanNoiBo_File (
        ID          INT IDENTITY PRIMARY KEY,
        VanBanID    INT NOT NULL,
        TenFile     NVARCHAR(500) NOT NULL,
        DuongDan    NVARCHAR(500) NOT NULL,
        NgayUpload  DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_VanBanNoiBo_File_VanBan FOREIGN KEY (VanBanID) REFERENCES VanBanNoiBo(ID) ON DELETE CASCADE
    );
END

IF OBJECT_ID('DonVi_ApiKey') IS NULL
BEGIN
    CREATE TABLE DonVi_ApiKey (
        ID              INT IDENTITY PRIMARY KEY,
        MaDV            TINYINT NOT NULL,
        TenKey          NVARCHAR(100) NOT NULL,   -- mô tả, vd "Web Phòng CTSV"
        ApiKeyHash      CHAR(64) NOT NULL UNIQUE, -- SHA-256 hex của key thật — key thật chỉ hiện 1 lần lúc tạo, không lưu plaintext
        HienThi         BIT NOT NULL DEFAULT 1,
        NgayTao         DATETIME NOT NULL DEFAULT GETDATE(),
        MaNVTao         SMALLINT NOT NULL,
        NgaySuDungCuoi  DATETIME NULL,
        CONSTRAINT FK_DonVi_ApiKey_DonVi FOREIGN KEY (MaDV) REFERENCES DonVi(MaDV)
    );
    CREATE INDEX IX_DonVi_ApiKey_MaDV ON DonVi_ApiKey(MaDV);
END
