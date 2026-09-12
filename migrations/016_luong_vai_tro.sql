SET QUOTED_IDENTIFIER ON;
GO
-- Luồng xử lý theo vai trò (thay thế mô hình "chỉ chuyển thẳng đơn vị-đơn vị" quá đơn giản ở
-- Phase 9 bằng luồng có nấc: Lãnh đạo đơn vị (DonVi.MaNV_TDV) xem xét trước — nếu cần thì xin ý
-- kiến BGH (quyền 12) — rồi phân công cho MỘT chuyên viên cụ thể (tự tạo CongViec liên kết qua
-- LoaiNguonGoc/MSCVGoc) — kèm khả năng "Trả lại" ở mỗi nấc. Cơ chế LuongXuLy/ChuyenXuLy (Phase 9,
-- chuyển thẳng đơn vị-đơn vị) vẫn giữ nguyên song song làm phương án linh hoạt bổ sung.
-- Phần "Văn phòng điện tử" — Phase 11 (theo phản hồi người dùng về luồng xử lý còn quá đơn giản).

IF OBJECT_ID('CongVanDen_ChiDao') IS NULL
BEGIN
    CREATE TABLE CongVanDen_ChiDao (
        ID           int IDENTITY(1,1) PRIMARY KEY,
        MSCV         nchar(20)      NOT NULL,
        MaDV         tinyint        NOT NULL,
        -- 1=Chỉ đạo & phân công chuyên viên, 2=Xin ý kiến BGH, 3=BGH cho ý kiến chỉ đạo,
        -- 4=Trả lại văn thư, 5=Chuyên viên xin trả lại (từ Công việc liên kết)
        LoaiHanhDong tinyint        NOT NULL,
        MaNV         smallint       NOT NULL,
        MaNVNhan     smallint       NULL,
        MaNVPhanCong smallint       NULL,
        MaCVLienKet  int            NULL,
        NoiDung      nvarchar(2000) NULL,
        NgayTao      datetime       NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_CVDChiDao_NV      FOREIGN KEY (MaNV)         REFERENCES NhanVien(MaNV),
        CONSTRAINT FK_CVDChiDao_NVNhan  FOREIGN KEY (MaNVNhan)     REFERENCES NhanVien(MaNV),
        CONSTRAINT FK_CVDChiDao_NVPC    FOREIGN KEY (MaNVPhanCong) REFERENCES NhanVien(MaNV),
        CONSTRAINT FK_CVDChiDao_CV      FOREIGN KEY (MaCVLienKet)  REFERENCES CongViec(MaCV)
    );
    CREATE INDEX IX_CVDChiDao_MSCV ON CongVanDen_ChiDao(MSCV);
END
GO
