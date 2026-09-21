-- 2026-09-21: DỰ THẢO VĂN BẢN ĐI — theo dõi BƯỚC HIỆN TẠI trong chuỗi duyệt.
--   BuocIdx = -1: đang ở người soạn (nháp / bị trả lại); 0..n-1: đang ở bước duyệt thứ i của ChuoiDuyet;
--   n (= số bước): đã duyệt xong, chờ văn thư ban hành.
--   ChuoiDuyet giờ là chuỗi BƯỚC (csv): mỗi phần tử là MaNV (người duyệt/ký cụ thể) hoặc "VT1" (văn thư cấp 1 kiểm tra thể thức).
--   Cấp trường: Người soạn → Lãnh đạo đơn vị → Văn thư (cấp 1) kiểm tra → Lãnh đạo trường ký → Văn thư cấp 1 ban hành.
--   Nội bộ đơn vị: Người soạn → Lãnh đạo đơn vị ký → Văn thư cấp 2 ban hành.

IF COL_LENGTH('DuThaoVanBanDi', 'BuocIdx') IS NULL
    ALTER TABLE DuThaoVanBanDi ADD BuocIdx smallint NOT NULL CONSTRAINT DF_DuThao_BuocIdx DEFAULT -1;
GO
