-- 2026-09-20: DỰ THẢO VĂN BẢN ĐI — phạm vi ban hành + chuỗi duyệt/ký nhiều cấp.
--   PhamVi: 1 = Cấp trường (ra ngoài/toàn trường; văn thư CẤP 1 = văn thư trường ban hành, cấp số ở sổ văn bản đi)
--           2 = Nội bộ đơn vị (chỉ trong đơn vị; văn thư CẤP 2 = văn thư đơn vị ban hành vào "Văn bản nội bộ đơn vị")
--   ChuoiDuyet: danh sách MaNV còn phải duyệt/ký theo thứ tự (csv). Hết chuỗi mới tới văn thư ban hành.
--   MaVBNoiBo: ID văn bản nội bộ đơn vị sinh ra khi ban hành với PhamVi=2.

IF COL_LENGTH('DuThaoVanBanDi', 'PhamVi') IS NULL
    ALTER TABLE DuThaoVanBanDi ADD PhamVi tinyint NOT NULL CONSTRAINT DF_DuThao_PhamVi DEFAULT 1;
GO
IF COL_LENGTH('DuThaoVanBanDi', 'ChuoiDuyet') IS NULL
    ALTER TABLE DuThaoVanBanDi ADD ChuoiDuyet nvarchar(200) NULL;
GO
IF COL_LENGTH('DuThaoVanBanDi', 'MaVBNoiBo') IS NULL
    ALTER TABLE DuThaoVanBanDi ADD MaVBNoiBo int NULL;
GO
