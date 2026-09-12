-- Them truong "Nguoi soan thao" cho So dang ky van ban di — 1 trong 10 truong bat buoc theo
-- quy dinh cong tac van thu (So, ky hieu; Ngay thang; Ten loai & trich yeu; Nguoi ky; Noi nhan;
-- Don vi/nguoi nhan ban luu; So luong ban; So luong trang; NGUOI SOAN THAO VA DON VI SOAN THAO;
-- Ghi chu). Idempotent — chi them cot neu chua co.
IF COL_LENGTH('CongVanDi', 'NguoiSoanThao') IS NULL
BEGIN
    ALTER TABLE CongVanDi ADD NguoiSoanThao nvarchar(150) NULL;
END
