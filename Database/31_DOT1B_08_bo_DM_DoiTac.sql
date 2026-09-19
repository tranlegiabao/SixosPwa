/* =============================================================================
   DOT 1B - buoc B08: bo DM_DoiTac (C16 / PA-1)
   ---------------------------------------------------------------------------
   Giu KHAI NIEM "cong ty" bang mot cot phang tren DM_CSKCB, bo han bang.
   Do that: 2 dong DM_DoiTac, chi 1/12 co so co IDCongTy (=2, "Hoang Dung").

   Buoc nay xoa luon LO LO MAT KHAU: DM_DoiTac.MatKhauDoiTac la nguon cua
   data-password in ra HTML o Views/Home/GuiTinNhan.cshtml. Khong con cot thi
   khong con gi de lo.

   DAO ADR 0011 (co so thuoc doi tac mot-nhieu).
   THU TU: chay SAU B07, TRUOC B06.
   ========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF OBJECT_ID('bak3.DM_DoiTac_B01') IS NULL OR OBJECT_ID('bak3.DM_CSKCB_CongTy_B01') IS NULL
    THROW 50080, 'Chua chay B01 snapshot - DUNG LAI.', 1;
GO

/* --- 1. them cot phang -------------------------------------------------- */
IF COL_LENGTH('dbo.DM_CSKCB', 'TenCongTy') IS NULL
    ALTER TABLE dbo.DM_CSKCB ADD TenCongTy nvarchar(200) NULL;
GO

/* --- 2. chep gia tri qua IDCongTy --------------------------------------- */
IF COL_LENGTH('dbo.DM_CSKCB', 'IDCongTy') IS NOT NULL AND OBJECT_ID('dbo.DM_DoiTac') IS NOT NULL
    UPDATE k SET k.TenCongTy = d.TenDT
    FROM dbo.DM_CSKCB k
    JOIN dbo.DM_DoiTac d ON d.ID = k.IDCongTy
    WHERE k.TenCongTy IS NULL;
GO

/* --- 3. CHAN roi PHA, cung mot batch ------------------------------------ */
/* Chot hep dung chieu: hoi "da chep xong chua", khong hoi "bang con khong". */
DECLARE @canChep int = 0, @daChep int = 0;
IF COL_LENGTH('dbo.DM_CSKCB', 'IDCongTy') IS NOT NULL AND OBJECT_ID('dbo.DM_DoiTac') IS NOT NULL
BEGIN
    SELECT @canChep = COUNT(*) FROM dbo.DM_CSKCB k JOIN dbo.DM_DoiTac d ON d.ID = k.IDCongTy;
    SELECT @daChep  = COUNT(*) FROM dbo.DM_CSKCB k JOIN dbo.DM_DoiTac d ON d.ID = k.IDCongTy
                      WHERE k.TenCongTy = d.TenDT;
END

IF OBJECT_ID('dbo.DM_DoiTac') IS NULL
BEGIN
    SELECT 'B08: DM_DoiTac da khong con - bo qua.' AS KetQua;
END
ELSE IF @canChep <> @daChep OR COL_LENGTH('dbo.DM_CSKCB', 'TenCongTy') IS NULL
BEGIN
    RAISERROR('B08 DUNG LAI: can chep %d co so, moi chep duoc %d. KHONG pha bang.',
              16, 1, @canChep, @daChep);
END
ELSE
BEGIN
    /* xoa vai tro DoiTac trong HT_TaiKhoan truoc, keo theo rang buoc */
    DELETE FROM dbo.HT_TaiKhoan WHERE Role = 'DoiTac';

    IF OBJECT_ID('dbo.FK_DM_CSKCB_CongTy') IS NOT NULL
        ALTER TABLE dbo.DM_CSKCB DROP CONSTRAINT FK_DM_CSKCB_CongTy;
    IF COL_LENGTH('dbo.DM_CSKCB', 'IDCongTy') IS NOT NULL
        ALTER TABLE dbo.DM_CSKCB DROP COLUMN IDCongTy;

    DROP TABLE dbo.DM_DoiTac;
    SELECT 'B08: da bo dbo.DM_DoiTac + cot IDCongTy + vai tro DoiTac.' AS KetQua;
END
GO

/* --- 4. bo stored khong con dich ---------------------------------------- */
IF OBJECT_ID('dbo.DM_DoiTac_Save', 'P') IS NOT NULL DROP PROCEDURE dbo.DM_DoiTac_Save;
GO

/* --- 5. tu kiem --------------------------------------------------------- */
SELECT 'B08 bo DM_DoiTac' AS Buoc,
       (SELECT COUNT(*) FROM sys.tables
         WHERE SCHEMA_NAME(schema_id) = 'dbo' AND name = 'DM_DoiTac')          AS ConBang_phai_0,
       (SELECT COUNT(*) FROM sys.columns
         WHERE object_id = OBJECT_ID('dbo.DM_CSKCB') AND name = 'IDCongTy')    AS ConCotIDCongTy_phai_0,
       (SELECT COUNT(*) FROM dbo.DM_CSKCB WHERE TenCongTy IS NOT NULL)         AS CoSo_co_TenCongTy_ky_vong_1,
       (SELECT COUNT(*) FROM dbo.HT_TaiKhoan WHERE Role = 'DoiTac')            AS TaiKhoanDoiTac_phai_0,
       (SELECT COUNT(*) FROM sys.objects WHERE name = 'DM_DoiTac_Save')        AS ConStored_phai_0,
       (SELECT COUNT(*) FROM sys.tables WHERE SCHEMA_NAME(schema_id) = 'dbo')  AS SoBangDbo_ky_vong_14;
GO
