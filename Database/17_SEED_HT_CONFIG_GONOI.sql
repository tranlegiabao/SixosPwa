/* =============================================================================
   17_SEED_HT_CONFIG_GONOI.sql -- Seed cau hinh GONOI trong bang HT_Config
   DB: HIS_CSKH. Chay lai duoc (Idempotent).

   MUC DICH:
     - Bat tinh nang cho phep benh nhan go noi ma ho so tai man Sua ho so (/benh-nhan/ho-so/sua).
     - MaChucNang = 'GONOI'
     - HieuLuc = 1 (Bat)
     - Nhom = N'Hồ sơ'
     - Ghichu = N'Cho phép bệnh nhân tự gỡ nối mã hồ sơ tại màn sửa hồ sơ'
   ============================================================================= */

SET NOCOUNT ON;
GO

PRINT N'=== BAT DAU SEED CAU HINH GONOI TRONG dbo.HT_Config ===';

IF EXISTS (SELECT 1 FROM dbo.HT_Config WHERE MaChucNang = 'GONOI')
BEGIN
    PRINT N'-> Cau hinh GONOI da ton tai. Tien hanh cap nhat HieuLuc = 1...';
    UPDATE dbo.HT_Config
       SET HieuLuc = 1,
           Nhom = N'Hồ sơ',
           Ghichu = N'Cho phép bệnh nhân tự gỡ nối mã hồ sơ tại màn sửa hồ sơ'
     WHERE MaChucNang = 'GONOI';
    PRINT N'   Da cap nhat thanh cong.';
END
ELSE
BEGIN
    PRINT N'-> Dang them moi cau hinh GONOI...';
    INSERT INTO dbo.HT_Config (MaChucNang, SoLuong, HieuLuc, Ghichu, Ngay, GiaTri, Nhom)
    VALUES ('GONOI', NULL, 1, N'Cho phép bệnh nhân tự gỡ nối mã hồ sơ tại màn sửa hồ sơ', NULL, NULL, N'Hồ sơ');
    PRINT N'   Da them moi thanh cong.';
END
GO

PRINT N'=== KET QUA HIEN TAI TRONG dbo.HT_Config ===';
SELECT ID, MaChucNang, SoLuong, HieuLuc, Ghichu, Nhom FROM dbo.HT_Config WHERE MaChucNang = 'GONOI';
GO
