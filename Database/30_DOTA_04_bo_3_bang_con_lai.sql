/* =============================================================================
   DOT A - buoc A5: bo 3 bang con lai => 19 -> 16 bang dbo.
     HT_TaiKhoanDoiTac : chet cung bo man doi tac UB (V1)
     HT_ThietBi        : bang ghi-roi-bo, 0 cho doc (V11)
     DM_GioiTinh       : danh muc 3 dong -> hang trong C# + CHECK (A06)
   Chay SAU A02. Duong lui: bak2.*_A0 da chup o A01 -> xem R04.
   ========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
/* --- go khoa ngoai tro VAO 3 bang nay truoc --------------------------- */
/* FK_HT_PushDangKy_ThietBi duoc go o A05 cung luc bo cot IDThietBi; o day
   go them cho chac neu A05 chay sau. */
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_HT_PushDangKy_ThietBi')
    ALTER TABLE dbo.HT_PushDangKy DROP CONSTRAINT FK_HT_PushDangKy_ThietBi;
GO
/* --- doi chieu: khong con dong nao trong 3 bang bi tham chieu ---------- */
DECLARE @conSot int =
    (SELECT COUNT(*) FROM dbo.HT_PushDangKy
      WHERE COL_LENGTH('dbo.HT_PushDangKy','IDThietBi') IS NOT NULL);
SELECT 'A04 truoc khi xoa' AS Buoc,
       (SELECT COUNT(*) FROM dbo.HT_TaiKhoanDoiTac) AS TaiKhoanDoiTac,
       (SELECT COUNT(*) FROM dbo.HT_ThietBi)        AS ThietBi,
       (SELECT COUNT(*) FROM dbo.DM_GioiTinh)       AS GioiTinh,
       (SELECT COUNT(*) FROM bak2.HT_TaiKhoanDoiTac_A0) AS DaChup_TKDT,
       (SELECT COUNT(*) FROM bak2.HT_ThietBi_A0)        AS DaChup_TB,
       (SELECT COUNT(*) FROM bak2.DM_GioiTinh_A0)       AS DaChup_GT;
GO
/* Chi xoa khi ban chup o A01 that su co du lieu tuong ung. */
IF (SELECT COUNT(*) FROM bak2.HT_TaiKhoanDoiTac_A0) <> (SELECT COUNT(*) FROM dbo.HT_TaiKhoanDoiTac)
   OR (SELECT COUNT(*) FROM bak2.HT_ThietBi_A0)     <> (SELECT COUNT(*) FROM dbo.HT_ThietBi)
   OR (SELECT COUNT(*) FROM bak2.DM_GioiTinh_A0)    <> (SELECT COUNT(*) FROM dbo.DM_GioiTinh)
BEGIN
    RAISERROR(N'A04 DUNG LAI: ban chup A01 khong khop hien trang, chay lai A01 truoc.', 16, 1);
    RETURN;
END;
GO
DROP TABLE dbo.HT_TaiKhoanDoiTac;
DROP TABLE dbo.HT_ThietBi;
DROP TABLE dbo.DM_GioiTinh;
GO
/* --- tu kiem: dbo phai con dung 16 bang ------------------------------- */
SELECT 'A04 xong' AS Buoc,
       (SELECT COUNT(*) FROM sys.tables WHERE SCHEMA_NAME(schema_id) = 'dbo') AS SoBangDbo;

SELECT 'A04 danh sach 16 bang' AS Buoc, name AS Bang
FROM sys.tables WHERE SCHEMA_NAME(schema_id) = 'dbo' ORDER BY name;
GO
