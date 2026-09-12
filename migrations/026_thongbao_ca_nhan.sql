-- 026: Thông báo sự kiện xử lý (khác ThongBaoNoiBo là bảng tin nội bộ tự đăng) — tự động sinh khi
-- văn bản được giao mới hoặc chuyển xử lý sang đơn vị khác, để đơn vị nhận biết ngay thay vì chỉ
-- phát hiện được khi có link trực tiếp (xem CongVanDen.MaDVXL và các danh sách lọc theo nó).
IF OBJECT_ID('ThongBaoCaNhan') IS NULL
BEGIN
    CREATE TABLE ThongBaoCaNhan (
        ID       INT IDENTITY PRIMARY KEY,
        MaDV     TINYINT NOT NULL,
        MaNV     SMALLINT NULL,     -- NULL = cho cả đơn vị (ai xử lý được đơn vị đó đều thấy); có giá trị = riêng 1 người
        Loai     TINYINT NOT NULL,  -- 1=Giao xử lý mới,2=Chuyển xử lý đến,3=Chỉ đạo & phân công,4=Xin ý kiến BGH,5=Trả lại văn thư,6=BGH trả lời ý kiến
        NoiDung  NVARCHAR(500) NOT NULL,
        MSCV     NVARCHAR(20) NULL,
        NgayTao  DATETIME NOT NULL DEFAULT GETDATE(),
        DaXem    BIT NOT NULL DEFAULT 0,
        NgayXem  DATETIME NULL
    );
    CREATE INDEX IX_ThongBaoCaNhan_MaDV ON ThongBaoCaNhan(MaDV, DaXem, NgayTao DESC);
    CREATE INDEX IX_ThongBaoCaNhan_MaNV ON ThongBaoCaNhan(MaNV, DaXem, NgayTao DESC);
END
