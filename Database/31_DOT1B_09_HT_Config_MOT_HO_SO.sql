/* =============================================================================
   DOT 1B - buoc B09: seed 2 dong HT_Config
   ---------------------------------------------------------------------------
   MOT_HO_SO        - toggle C3/C4/C5:
       HieuLuc = 0 -> mot SDT nhieu ho so (hanh vi hien nay)
       HieuLuc = 1 -> kieu B: CHAN tao moi + CHI HIEN 1 ho so, chon theo CCCD
                      cua phien (luat R2); lech CCCD thi chan.
       Pham vi TOAN HE (khong co IDCoSo) - giu nguyen UK_HT_Config_MaChucNang.

   LOG_DON_LAN_CUOI - moc tiet che don log (C17b):
       Dung cot Ngay (kieu date) lam moc. Bang HT_Config KHONG co cot datetime,
       ma luat du an cam noi rong schema khong can thiet => tiet che theo NGAY
       LICH thay vi theo 24 gio dong ho. Dung voi y C17b: 1 lan quet/ngay.
       HieuLuc = 1 -> bat viec don; 0 -> tat han.

   Chay lai duoc nhieu lan (MERGE theo MaChucNang).
   ========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UK_HT_Config_MaChucNang')
    THROW 50090, 'Thieu UK_HT_Config_MaChucNang (dot A / A8) - DUNG LAI.', 1;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.HT_Config WHERE MaChucNang = 'MOT_HO_SO')
    INSERT INTO dbo.HT_Config (MaChucNang, HieuLuc, Ghichu, Ngay, GiaTri, Nhom)
    VALUES ('MOT_HO_SO', 0,
            N'Bật = mỗi số điện thoại chỉ giữ MỘT hồ sơ tại một cơ sở (chặn tạo mới, chỉ hiện 1 hồ sơ theo CCCD của phiên). Tắt = một số điện thoại nhiều hồ sơ.',
            NULL, NULL, N'Hồ sơ');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.HT_Config WHERE MaChucNang = 'LOG_DON_LAN_CUOI')
    INSERT INTO dbo.HT_Config (MaChucNang, HieuLuc, Ghichu, Ngay, GiaTri, Nhom)
    VALUES ('LOG_DON_LAN_CUOI', 1,
            N'Mốc lần dọn HT_LogApiCoSo gần nhất (cột Ngày). Mỗi ngày lịch dọn tối đa một lần, xoá bản ghi quá 3 tháng. Tắt Hiệu lực để dừng hẳn việc dọn.',
            NULL, NULL, N'Hệ thống');
GO

SELECT 'B09 HT_Config' AS Buoc,
       (SELECT COUNT(*) FROM dbo.HT_Config)                                      AS Tong_ky_vong_3,
       (SELECT COUNT(*) FROM dbo.HT_Config WHERE MaChucNang = 'MOT_HO_SO')       AS MotHoSo_phai_1,
       (SELECT CAST(HieuLuc AS int) FROM dbo.HT_Config WHERE MaChucNang = 'MOT_HO_SO')        AS MotHoSo_mac_dinh_0,
       (SELECT COUNT(*) FROM dbo.HT_Config WHERE MaChucNang = 'LOG_DON_LAN_CUOI') AS MocDonLog_phai_1;
GO
