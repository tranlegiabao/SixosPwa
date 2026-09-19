/* =============================================================================
   DOT 1B - LUI buoc B10 (stored) va B11 (go SDT)
   ---------------------------------------------------------------------------
   R11 tra SDT ve tu bak3.SdtDaGo_B11.
   R10 dung lai 30 stored ban cu tu bak3.StoredDinhNghia_B01.

   🔴 R10 chi co nghia khi da chay R02_05 (bang cu da tro lai): stored ban cu
      doc DM_BenhNhanCoSo, dung lai chung khi bang do khong con la vo ich.
      Thu tu lui day du:  R10_11  ->  R06_09  ->  R02_05  ->  chay lai R10.
      (Chay R10 hai lan la co y: lan dau de go stored moi, lan sau de nap ban
       cu khi bang cu da co.)
   ========================================================================== */
SET NOCOUNT ON;
GO

/* ===========================================================================
   R11 - tra SDT da go
   =========================================================================== */
IF OBJECT_ID('bak3.SdtDaGo_B11') IS NOT NULL
BEGIN
    /* Chi tra cho dong VAN DANG trong SDT - khong de len gia tri moi hon
       (HIS co the da day SDT moi ve sau, do la du lieu that hon ban chup). */
    UPDATE b
       SET b.SDT = x.SDT
      FROM dbo.DM_BenhNhan b
      JOIN (SELECT ID, SDT,
                   ROW_NUMBER() OVER (PARTITION BY ID ORDER BY NgayGo DESC) AS hang
              FROM bak3.SdtDaGo_B11) x
        ON x.ID = b.ID AND x.hang = 1
     WHERE b.SDT IS NULL;

    SELECT 'R11 tra SDT' AS Buoc, @@ROWCOUNT AS SoDongDaTra;
END
ELSE
    SELECT 'R11: khong co bak3.SdtDaGo_B11 - bo qua.' AS KetQua;
GO

/* ===========================================================================
   R10 - dung lai 30 stored ban cu
   ---------------------------------------------------------------------------
   Duyet bak3.StoredDinhNghia_B01, doi CREATE PROCEDURE -> CREATE OR ALTER
   PROCEDURE roi EXEC. Dung CREATE OR ALTER de KHONG mat GRANT cua spwa_his.
   =========================================================================== */
IF OBJECT_ID('bak3.StoredDinhNghia_B01') IS NULL
BEGIN
    SELECT 'R10 DUNG LAI: khong con bak3.StoredDinhNghia_B01.' AS KetQua;
END
ELSE
BEGIN
    /* Bo 4 stored TEN MOI do B10b dat ra - ban cu khong co chung. */
    IF OBJECT_ID('dbo.DM_BenhNhan_DoiMa', 'P')         IS NOT NULL DROP PROCEDURE dbo.DM_BenhNhan_DoiMa;
    IF OBJECT_ID('dbo.DM_BenhNhan_DoiMocXemLich', 'P') IS NOT NULL DROP PROCEDURE dbo.DM_BenhNhan_DoiMocXemLich;
    IF OBJECT_ID('dbo.DM_BenhNhan_GoNoi', 'P')         IS NOT NULL DROP PROCEDURE dbo.DM_BenhNhan_GoNoi;
    IF OBJECT_ID('dbo.DM_BenhNhan_TaoTuKhai', 'P')     IS NOT NULL DROP PROCEDURE dbo.DM_BenhNhan_TaoTuKhai;

    DECLARE @ten sysname, @dn nvarchar(max), @sql nvarchar(max);
    DECLARE @loi int = 0, @xong int = 0;

    DECLARE c CURSOR LOCAL FAST_FORWARD FOR
        SELECT Ten, DinhNghia FROM bak3.StoredDinhNghia_B01 WHERE Loai = 'SQL_STORED_PROCEDURE';

    OPEN c;
    FETCH NEXT FROM c INTO @ten, @dn;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        BEGIN TRY
            /* 🔴 CREATE OR ALTER, khong DROP + CREATE: DROP lam bay het GRANT
               da cap cho login spwa_his.
               Doi tu CREATE DAU TIEN thanh CREATE OR ALTER. Dinh nghia lay tu
               OBJECT_DEFINITION nen luon bat dau bang CREATE PROCEDURE (co the
               kem chu thich phia truoc), va CHARINDEX tim dung tu do. */
            SET @sql = @dn;

            IF @sql NOT LIKE '%CREATE OR ALTER%'
                SET @sql = STUFF(@sql, CHARINDEX('CREATE', @sql), 6, 'CREATE OR ALTER');

            EXEC sp_executesql @sql;
            SET @xong = @xong + 1;
        END TRY
        BEGIN CATCH
            SET @loi = @loi + 1;
            PRINT 'R10 HONG: ' + @ten + ' -> ' + ERROR_MESSAGE();
        END CATCH;

        FETCH NEXT FROM c INTO @ten, @dn;
    END
    CLOSE c;
    DEALLOCATE c;

    SELECT 'R10 dung lai stored' AS Buoc, @xong AS DaDungLai, @loi AS Hong;
END
GO

/* --- tu kiem ------------------------------------------------------------ */
SELECT 'R10-11 lui' AS Buoc,
       (SELECT COUNT(*) FROM sys.objects WHERE type = 'P')                  AS TongStored,
       (SELECT COUNT(*) FROM sys.objects WHERE type = 'P'
         AND name LIKE 'DM_BenhNhanCoSo[_]%')                               AS TenCu_ky_vong_5,
       (SELECT COUNT(*) FROM sys.objects WHERE type = 'P'
         AND name LIKE 'DM_BenhNhan[_]Doi%')                                AS TenMoi_phai_0;
GO
