/* =============================================================================
   DOT A - buoc A9 (tuy chon, user chot 18-09): don sach schema bak.
   29 bang: 10 bang `_V001` (dot 2, 08/2026) + 19 bang `_20260917` (do tai 20k).

   ⚠️ KHONG khoi phuc duoc. bak2.* (chup o A01 cho chinh dot A) KHONG bi dung toi.
   Manifest so dong duoc ghi lai vao bak2.ManifestBakDaXoa_A09 truoc khi xoa, de
   sau con doi chieu duoc con so da bao cao trong dot do tai.
   ========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('bak2.ManifestBakDaXoa_A09') IS NOT NULL DROP TABLE bak2.ManifestBakDaXoa_A09;
GO
SELECT SCHEMA_NAME(t.schema_id) AS Sch, t.name AS Bang, p.rows AS SoDong,
       t.create_date AS NgayTao, GETDATE() AS NgayXoa
INTO bak2.ManifestBakDaXoa_A09
FROM sys.tables t
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
WHERE SCHEMA_NAME(t.schema_id) = 'bak';
GO
SELECT 'A09 manifest' AS Buoc, COUNT(*) AS SoBang, SUM(SoDong) AS TongSoDong
FROM bak2.ManifestBakDaXoa_A09;
GO
/* --- xoa tung bang bang con tro he thong (29 ten, khong go tay cho sot) -- */
DECLARE @sql nvarchar(max) = N'';
SELECT @sql = @sql + N'DROP TABLE bak.' + QUOTENAME(name) + N';' + CHAR(10)
FROM sys.tables WHERE SCHEMA_NAME(schema_id) = 'bak';
EXEC sp_executesql @sql;
GO
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE SCHEMA_NAME(schema_id) = 'bak')
   AND SCHEMA_ID('bak') IS NOT NULL
    DROP SCHEMA bak;
GO
/* --- tu kiem ------------------------------------------------------------ */
SELECT 'A09 xong' AS Buoc,
       (SELECT COUNT(*) FROM sys.tables WHERE SCHEMA_NAME(schema_id) = 'bak')  AS ConBangBak,
       (SELECT COUNT(*) FROM sys.tables WHERE SCHEMA_NAME(schema_id) = 'bak2') AS BangBak2GiuLai,
       (SELECT COUNT(*) FROM sys.tables WHERE SCHEMA_NAME(schema_id) = 'dbo')  AS SoBangDbo,
       (SELECT COUNT(*) FROM sys.tables)                                        AS TongSoBang;
GO
