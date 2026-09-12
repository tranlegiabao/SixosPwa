-- ============================================================================
-- 22 ROLLBACK — don sach du lieu nghiem thu Dot 2
--
-- Doi xung voi 22_SEED_NGHIEM_THU_DOT2.sql. CHAY TREN: HIS_CSKH.
--
-- 🔴 CHI don thu SEED da tao:
--   * 3 dong tai lieu mang tien to [NT2] (khong do theo ID — ID khac nhau moi lan)
--   * cau hinh kho cua co so 77121
-- KHONG dung cot NguonKho, KHONG dung bang HT_KhoFtpCoSo: do la lo trinh tiep,
-- muon bo han thi xem 99_ROLLBACK.sql.
-- ============================================================================

SET NOCOUNT ON;
GO

DECLARE @IDCoSo bigint = (SELECT ID FROM dbo.DM_CSKCB WHERE MaCoSo = N'77121');

IF @IDCoSo IS NULL
BEGIN
    PRINT N'Khong co co so 77121 — khong co gi de don.';
    RETURN;
END;

DECLARE @SoDong int;

DELETE FROM dbo.QL_TaiLieuBenhNhan
WHERE IDCoSo = @IDCoSo
  AND MaBN = N'145691'
  AND TenTaiLieu LIKE N'[[]NT2]%';
SET @SoDong = @@ROWCOUNT;
PRINT N'✔ Da xoa ' + CAST(@SoDong AS nvarchar(10)) + N' dong tai lieu [NT2].';

DELETE FROM dbo.HT_KhoFtpCoSo WHERE IDCoSo = @IDCoSo;
SET @SoDong = @@ROWCOUNT;
PRINT N'✔ Da xoa ' + CAST(@SoDong AS nvarchar(10)) + N' cau hinh kho cua co so 77121.';
GO

-- Doi chieu sau khi don (ca hai phai ra 0):
-- SELECT COUNT(*) FROM dbo.QL_TaiLieuBenhNhan WHERE TenTaiLieu LIKE N'[[]NT2]%';
-- SELECT COUNT(*) FROM dbo.HT_KhoFtpCoSo
--   WHERE IDCoSo = (SELECT ID FROM dbo.DM_CSKCB WHERE MaCoSo = N'77121');
