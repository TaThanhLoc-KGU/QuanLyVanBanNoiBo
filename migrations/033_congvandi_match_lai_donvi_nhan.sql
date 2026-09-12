-- 2026-08-28: Sửa dữ liệu công văn đi — trước đây nhập lên (Excel cũ) đơn vị nhận bị đổ hết vào
-- cột NoiNhanCV (nơi nhận ngoài trường, dạng chữ tự do) thay vì cột DonViNhan (mã đơn vị nội bộ).
-- Script này map lại: NoiNhanCV nào là tên/tên viết tắt của 1 đơn vị -> chuyển sang DonViNhan,
-- xoá NoiNhanCV. Chỉ đụng các dòng DonViNhan còn trống. 2 giá trị đúng là "ngoài trường"
-- (KBNN tỉnh Kiên Giang, Tạp chí Kế toán và Kiểm toán) được giữ nguyên ở NoiNhanCV.
-- Chạy được nhiều lần (chỉ tác động khi DonViNhan còn trống).

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;   -- bắt buộc vì CongVanDi có filtered index (IX_CongVanDi_CongKhai)

-- Sao lưu giá trị NoiNhanCV gốc của các dòng sắp bị sửa (phòng khi cần đối chiếu / hoàn tác).
IF OBJECT_ID('_bak_033_congvandi_noinhan') IS NULL
    CREATE TABLE _bak_033_congvandi_noinhan (MSCV nchar(20) PRIMARY KEY, NoiNhanCV_Cu nvarchar(500), NgayLuu datetime DEFAULT GETDATE());

INSERT INTO _bak_033_congvandi_noinhan (MSCV, NoiNhanCV_Cu)
SELECT cv.MSCV, cv.NoiNhanCV
FROM CongVanDi cv
WHERE (cv.DonViNhan IS NULL OR LTRIM(RTRIM(cv.DonViNhan)) = '')
  AND cv.NoiNhanCV IS NOT NULL AND LTRIM(RTRIM(cv.NoiNhanCV)) <> ''
  AND NOT EXISTS (SELECT 1 FROM _bak_033_congvandi_noinhan b WHERE b.MSCV = cv.MSCV);

;WITH map AS (
    SELECT * FROM (VALUES
        (N'CTSV&KN',                       33),
        (N'HLTH',                          31),
        (N'HTKHCN',                         5),
        (N'Khoa Chính trị Luật',           10),
        (N'Khoa Kinh tế',                  11),
        (N'NN&PTNT',                       14),
        (N'P. BĐCL&TT-PC',                  7),
        (N'P. Đào tạo',                     9),
        (N'p.đa',                           9),
        (N'P.Đào tạo',                      9),
        (N'P.ĐT',                           9),
        (N'P.QTCSVC',                      24),
        (N'p.TCNS',                         3),
        (N'QTCSVC',                        24),
        (N'TCNS',                           3),
        (N'TTĐTSHTH',                      19)
    ) v(NoiNhan, MaDV)
)
UPDATE cv
SET cv.DonViNhan = CAST(m.MaDV AS varchar(10)),
    cv.NoiNhanCV = NULL
FROM CongVanDi cv
JOIN map m ON LTRIM(RTRIM(cv.NoiNhanCV)) = m.NoiNhan
WHERE cv.DonViNhan IS NULL OR LTRIM(RTRIM(cv.DonViNhan)) = '';

PRINT N'033: da match lai ' + CAST(@@ROWCOUNT AS nvarchar(10)) + N' cong van di.';
