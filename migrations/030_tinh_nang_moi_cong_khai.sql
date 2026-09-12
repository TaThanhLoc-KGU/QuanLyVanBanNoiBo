-- Cong tac an tam thoi cac tinh nang moi (ngoai cong van den/di) khoi nguoi dung thuong, chi
-- tai khoan Admin (quyen 0) thay de test truoc. Bat cong khai qua route rieng /Admin/TinhNangMoi.
IF OBJECT_ID('CauHinhHeThong') IS NULL
BEGIN
    CREATE TABLE CauHinhHeThong (
        Ten     nvarchar(50) NOT NULL PRIMARY KEY,
        GiaTri  nvarchar(50) NOT NULL
    );
    INSERT INTO CauHinhHeThong (Ten, GiaTri) VALUES ('TinhNangMoiCongKhai', '0');
END
