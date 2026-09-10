/* =============================================================================
   15_BO_TRUONG_KIEUAPI.sql -- Loai bo cot KieuApi khoi bang DM_DoiTacApi
   DB: HIS_CSKH. Chay lai duoc (Idempotent).

   MUC DICH:
     - Bo cot KieuApi trong DM_DoiTacApi vi he thong da chuyen sang dung:
       IDCoSo -> IDNhomCS -> MaNhom (nhakhoa, benhvien, pkdk, ...) de xac dinh
       nhom co so, va ket hop cac truong cau hinh (BaseUrl, TrangChu) de chon
       cong tich hop.
     - Xoa Check Constraint CK_DM_DoiTacApi_KieuApi (neu con ton tai).
     - Xoa Default Constraint cua cot KieuApi (neu co).
     - Xoa cot KieuApi khoi bang dbo.DM_DoiTacApi.

   CHU Y:
     File nay duoc tao de luu tru trong repository va chay chu dong khi ha tang
     database duoc bao tri. KHONG chay tu dong khi start ung dung.
   ============================================================================= */

SET NOCOUNT ON;
GO

PRINT N'=== BAT DAU MIGRATION: BO COT KieuApi BANG DM_DoiTacApi ===';

-- 1. Xoa Check Constraint CK_DM_DoiTacApi_KieuApi neu ton tai
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_DM_DoiTacApi_KieuApi')
BEGIN
    PRINT N'-> Dang xoa constraint CK_DM_DoiTacApi_KieuApi...';
    ALTER TABLE dbo.DM_DoiTacApi DROP CONSTRAINT CK_DM_DoiTacApi_KieuApi;
    PRINT N'   Da xoa CK_DM_DoiTacApi_KieuApi thanh cong.';
END
ELSE
BEGIN
    PRINT N'-> Khong tim thay CK_DM_DoiTacApi_KieuApi (da xoa truoc do).';
END
GO

-- 2. Xoa Default Constraint gan voi cot KieuApi neu co
DECLARE @DefaultConstraintName nvarchar(256);
SELECT @DefaultConstraintName = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE dc.parent_object_id = OBJECT_ID(N'dbo.DM_DoiTacApi') AND c.name = 'KieuApi';

IF @DefaultConstraintName IS NOT NULL
BEGIN
    PRINT N'-> Dang xoa Default Constraint: ' + @DefaultConstraintName;
    EXEC(N'ALTER TABLE dbo.DM_DoiTacApi DROP CONSTRAINT ' + QUOTENAME(@DefaultConstraintName));
    PRINT N'   Da xoa Default Constraint thanh cong.';
END
GO

-- 3. Xoa cot KieuApi khoi bang DM_DoiTacApi
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.DM_DoiTacApi') AND name = 'KieuApi'
)
BEGIN
    PRINT N'-> Dang xoa cot KieuApi khoi dbo.DM_DoiTacApi...';
    ALTER TABLE dbo.DM_DoiTacApi DROP COLUMN KieuApi;
    PRINT N'   Da xoa cot KieuApi thanh cong.';
END
ELSE
BEGIN
    PRINT N'-> Cot KieuApi khong ton tai trong dbo.DM_DoiTacApi (da duoc xoa truoc do).';
END
GO

PRINT N'=== HOAN TAT MIGRATION: BO COT KieuApi BANG DM_DoiTacApi ===';
GO

/* =============================================================================
   KICH BAN ROLLBACK (khi can khoi phuc lai cot KieuApi):
   -----------------------------------------------------------------------------
   IF NOT EXISTS (
       SELECT 1 FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.DM_DoiTacApi') AND name = 'KieuApi'
   )
   BEGIN
       ALTER TABLE dbo.DM_DoiTacApi ADD KieuApi varchar(20) NOT NULL CONSTRAINT DF_DM_DoiTacApi_KieuApi DEFAULT ('NONE');
       ALTER TABLE dbo.DM_DoiTacApi ADD CONSTRAINT CK_DM_DoiTacApi_KieuApi CHECK (KieuApi IN ('NONE', 'UB', 'HIS'));

       -- Khoi phuc du lieu mac dinh cho cac co so ung buou va HIS
       UPDATE dbo.DM_DoiTacApi SET KieuApi = 'UB' WHERE TrangChu IS NOT NULL AND TrangChu <> '';
       UPDATE dbo.DM_DoiTacApi SET KieuApi = 'HIS' WHERE KhoaGoiHIS IS NOT NULL AND KhoaGoiHIS <> '';
   END
   ============================================================================= */
