-- 025: Cho phép upload NHIỀU file kết quả xử lý mỗi dòng CongVanDenXuLyDV, có chi tiết từng file
-- (tên, người upload, ngày upload) thay vì chỉ 1 file/dòng (upload mới đè mất file cũ như trước).
IF OBJECT_ID('CVDenXuLyDV_File') IS NULL
BEGIN
    CREATE TABLE CVDenXuLyDV_File (
        ID          INT IDENTITY PRIMARY KEY,
        XuLyDVID    INT NOT NULL,
        TenFile     NVARCHAR(500) NOT NULL,
        DuongDan    NVARCHAR(500) NOT NULL,
        NgayUpload  DATETIME NOT NULL DEFAULT GETDATE(),
        MaNVUpload  SMALLINT NULL,
        CONSTRAINT FK_CVDenXuLyDV_File_XuLyDV FOREIGN KEY (XuLyDVID) REFERENCES CongVanDenXuLyDV(ID) ON DELETE CASCADE
    );
    CREATE INDEX IX_CVDenXuLyDV_File_XuLyDVID ON CVDenXuLyDV_File(XuLyDVID);

    -- Chuyển dữ liệu file đơn đã có (2 cột cũ FilePath/TenFile trên CongVanDenXuLyDV) sang bảng mới —
    -- không xóa 2 cột cũ (tránh rủi ro), chỉ ngừng đọc/ghi chúng từ code trở đi.
    INSERT INTO CVDenXuLyDV_File (XuLyDVID, TenFile, DuongDan, NgayUpload, MaNVUpload)
    SELECT ID, TenFile, FilePath, ISNULL(NgayCapNhat, GETDATE()), MaNVCapNhat
    FROM CongVanDenXuLyDV
    WHERE FilePath IS NOT NULL AND FilePath <> '';
END
