SET QUOTED_IDENTIFIER ON;
GO
-- Trao đổi/bình luận qua lại theo từng công việc — khác với CongViec_NhatKy (nhật ký xử lý
-- chính thức, chỉ mở khi công việc còn đang thực hiện): đây là kênh thảo luận tự do, mở cho
-- mọi người liên quan xem/viết bất kể trạng thái công việc.

IF OBJECT_ID('CongViec_BinhLuan') IS NULL
BEGIN
    CREATE TABLE CongViec_BinhLuan (
        ID           int IDENTITY(1,1) PRIMARY KEY,
        MaCV         int NOT NULL REFERENCES CongViec(MaCV),
        MaNV         smallint NOT NULL REFERENCES NhanVien(MaNV),
        NoiDung      nvarchar(2000) NOT NULL,
        NgayBinhLuan datetime NOT NULL DEFAULT GETDATE()
    );
END
GO
